using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MVC.Services;
using project.Models;
using project.Services;

namespace project.Controllers
{
    /// <summary>Employee self-service leave: apply, view balance, cancel.</summary>
    public class LeaveController : Controller
    {
        private readonly LeaveService _leave;
        private readonly HolidayService _holidays;
        private readonly AuditService _audit;
        private readonly RabbitMqService _mq;
        private readonly IChatService _chat;

        public LeaveController(LeaveService leave, HolidayService holidays, AuditService audit, RabbitMqService mq, IChatService chat)
        {
            _leave = leave;
            _holidays = holidays;
            _audit = audit;
            _mq = mq;
            _chat = chat;
        }

        private int? SessionEmpId =>
            int.TryParse(HttpContext.Session.GetString("Ids"), out var id) && id > 0 ? id : null;

        public IActionResult Index()
        {
            if (SessionEmpId == null) return RedirectToAction("Index", "Home");
            ViewBag.LeaveTypes = LeaveRules.Types;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Mine()
        {
            if (SessionEmpId is not int empId) return Unauthorized();
            return Json(await _leave.GetForEmployeeAsync(empId));
        }

        [HttpGet]
        public async Task<IActionResult> Balance(int? year)
        {
            if (SessionEmpId is not int empId) return Unauthorized();
            return Json(await _leave.GetBalancesAsync(empId, year ?? DateTime.Today.Year));
        }

        /// <summary>Preview how many working days a range counts as, before submitting.</summary>
        [HttpGet]
        public async Task<IActionResult> CountDays(DateTime from, DateTime to, bool halfDay = false)
        {
            if (SessionEmpId == null) return Unauthorized();
            if ((to - from).TotalDays > 366) return BadRequest();
            return Json(new { days = await _leave.CountDaysAsync(from, to, halfDay) });
        }

        [HttpGet]
        public async Task<IActionResult> UpcomingHolidays()
        {
            var today = DateTime.Today;
            var list = await _holidays.GetInRangeAsync(today, today.AddDays(120));
            return Json(list.Take(10));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply([FromForm] LeaveRequestInput input)
        {
            if (SessionEmpId is not int empId)
                return Json(new { success = false, message = "Session expired. Please log in again." });

            var (id, days, error) = await _leave.ApplyAsync(empId, input, DateTime.Today);
            if (error != null) return Json(new { success = false, message = error });

            var actor = AuditService.ActorFrom(HttpContext);
            var summary = $"{input.LeaveType} {input.FromDate:yyyy-MM-dd} to {input.ToDate:yyyy-MM-dd} ({days:0.#} day(s))";
            await _audit.LogAsync(actor, AuditActions.LeaveRequested, "Leave", id, summary);

            var note = $"Leave request from employee {empId}: {summary}";
            try
            {
                _mq.SendToUser(empId.ToString(), "admin", note);
                _chat.SendToUser(empId.ToString(), "admin", note);
            }
            catch
            {
                // Notifications are best effort; the request is already saved.
            }

            return Json(new { success = true, id, days, message = $"Leave requested for {days:0.#} day(s)." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            if (SessionEmpId is not int empId)
                return Json(new { success = false, message = "Session expired. Please log in again." });

            var error = await _leave.CancelAsync(empId, id, DateTime.Today);
            if (error != null) return Json(new { success = false, message = error });

            await _audit.LogAsync(AuditService.ActorFrom(HttpContext), AuditActions.LeaveCancelled, "Leave", id);
            return Json(new { success = true, message = "Leave cancelled." });
        }
    }
}
