using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MVC.Services;
using try_project_casepoint.Services;

namespace project.Controllers
{
    public class AdminController : Controller
    {
        private readonly ILogger<AdminController> _logger;
        private readonly AdminService _adminService;

        private readonly RabbitMqService _mq;
        private readonly IChatService _chat;

        public AdminController(ILogger<AdminController> logger, AdminService adminService, RabbitMqService mq, IChatService chat)
        {
            _logger = logger;
            _adminService = adminService;
            _mq = mq;
            _chat = chat;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult AdminDashboard() => View();

        public IActionResult AdminAttendance() => View();

        public IActionResult AdminEmployee() => View();

        // [HttpPost]
        // public async Task<IActionResult> MarkAbsentees()
        // {
        //     await _adminService.MarkAbsenteesAsync();
        //     return RedirectToAction("AdminDashboard");
        // }

        [HttpPost]
        public async Task<IActionResult> RunAttendance()
        {
            try
            {
                // Call your existing attendance function
                await _adminService.MarkAbsenteesAsync();

                return Ok(new { message = "Attendance executed successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }


        [HttpGet("absent")]
        public async Task<IActionResult> GetAbsentByDate(DateTime? date)
        {
            if (!date.HasValue)
                return BadRequest("Please provide a valid date parameter. Example: /admin/absent?date=2025-10-09");

            var absentees = await _adminService.GetAbsentEmployeesByDate(date.Value);
            return View("AbsentByDate", absentees); // or return Json(absentees);
        }

        [HttpGet]
        public async Task<IActionResult> GetAttendance(DateTime? date)
        {
            var attendanceList = await _adminService.GetAllAttendance();
            if (date.HasValue)
                attendanceList = attendanceList.FindAll(a => a.AttendDate.Date == date.Value.Date);

            return Json(attendanceList);
        }

        /// <summary>
        /// Downloads attendance as CSV. Defaults to the last 30 days.
        /// Example: /Admin/ExportAttendanceCsv?from=2026-11-01&amp;to=2026-11-30
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportAttendanceCsv(DateTime? from, DateTime? to)
        {
            var end = (to ?? DateTime.Today).Date;
            var start = (from ?? end.AddDays(-29)).Date;

            if (start > end)
                return BadRequest("'from' must be on or before 'to'.");
            if ((end - start).TotalDays > 366)
                return BadRequest("Date range cannot exceed one year.");

            var rows = await _adminService.GetAttendanceInRange(start, end);
            var csv = project.Services.CsvExport.BuildAttendanceCsv(rows);

            // UTF-8 with BOM so Excel shows non-ASCII names correctly.
            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray();
            var fileName = $"attendance_{start:yyyyMMdd}_{end:yyyyMMdd}.csv";
            return File(bytes, "text/csv", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardSummary(DateTime? date)
        {
            var data = await _adminService.GetDashboardSummary(date);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllEmployeesDetails()
        {
            var employees = await _adminService.GetAllEmployeesDetails();
            return Json(employees);
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployeeDetail(int empId)
        {
            var employee = await _adminService.GetEmployeeDetail(empId);
            return Json(employee);
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployeesByStatus(string status)
        {
            var employees = await _adminService.GetEmployeesByStatus(status);
            return Json(employees);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateEmployeeStatus(int empId, string status)
        {
            if (string.IsNullOrEmpty(status) || (status != "Active" && status != "Inactive"))
                return Json(new { success = false, message = "Invalid status" });

            var result = await _adminService.UpdateEmployeeStatus(empId, status);
            return Json(new { success = result });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEmployeeDetails(int empId, string empName, string gender, string status, IFormFile? profile)
        {
            try
            {
                if (empId <= 0)
                    return Json(new { success = false, message = "Invalid Employee ID" });

                if (string.IsNullOrWhiteSpace(empName))
                    return Json(new { success = false, message = "Employee name is required" });

                if (string.IsNullOrWhiteSpace(gender))
                    return Json(new { success = false, message = "Gender is required" });

                if (string.IsNullOrWhiteSpace(status) || (status != "Active" && status != "Inactive"))
                    return Json(new { success = false, message = "Invalid status value" });

                string? profileFileName = null;

                if (profile != null && profile.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "employee_images");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    profileFileName = $"{Guid.NewGuid()}_{Path.GetFileName(profile.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, profileFileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await profile.CopyToAsync(stream);
                    }
                }

                var result = await _adminService.UpdateEmployeeDetails(empId, empName, gender, status, profileFileName);
                return Json(new { success = result, message = result ? "Employee updated successfully" : "No changes made" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating employee details");
                return Json(new { success = false, message = "Error updating employee details" });
            }
        }

        public IActionResult GetMessages()
        {
            var msgs = new List<string>();
            string? msg;
            while ((msg = _mq.ReceiveOneForUser("admin")) != null)
            {
                msgs.Add("[RabbitMQ] " + msg);
            }

            // Also get from Redis queue
            string? redisMsg;
            while ((redisMsg = _chat.ReceiveOneForUser("admin")) != null)
            {
                msgs.Add("[Redis] " + redisMsg);
            }

            return Json(new { success = true, messages = msgs });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}