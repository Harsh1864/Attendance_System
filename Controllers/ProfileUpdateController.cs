using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using project.Models;
using try_project_casepoint.Services;

namespace try_project_casepoint.Controllers
{
    public class ProfileUpdateController : Controller
    {
        private readonly ILogger<ProfileUpdateController> _logger;

        private readonly ProfileUpdateService _profileupdateservice;

        public ProfileUpdateController(ILogger<ProfileUpdateController> logger, ProfileUpdateService profileuservice)
        {
            _profileupdateservice = profileuservice;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> UpdateProfile()
        {
            //   int? empId = HttpContext.Session.GetInt32("EmpID");
            //   if (empId == null) return RedirectToAction("Login");

            var sessionEmpId = Convert.ToInt32(HttpContext.Session.GetString("Ids"));
              var user = await _profileupdateservice.GetEmployeeById(sessionEmpId);
            return View(user);
            
        }


        [HttpPost]
        public async Task<IActionResult> UpdateProfile([FromForm] EmployeeModel employee)
        {
            try
            {
                // Only allow editing your own profile; never trust the posted id.
                var sessionId = HttpContext.Session.GetString("Ids");
                if (!int.TryParse(sessionId, out var empId) || empId <= 0)
                    return Json(new { success = false, message = "Session expired. Please log in again." });

                if (string.IsNullOrWhiteSpace(employee.EmpName) || employee.EmpName.Length > 50)
                    return Json(new { success = false, message = "Name is required (max 50 characters)." });

                if (string.IsNullOrWhiteSpace(employee.Gender))
                    return Json(new { success = false, message = "Gender is required." });

                employee.EmpId = empId;

                // Prevent password change
                employee.EmpPassword = null;

                await _profileupdateservice.UpdateProfile(employee);

                return Json(new { success = true, message = "Profile updated successfully!" });
            }
            catch (Exception ex)
            {
                // Send error message back to AJAX
                return Json(new { success = false, message = ex.Message });
            }
        }

        public IActionResult UpdatePassword()
        {
            //if (HttpContext.Session.GetInt32("EmpID") == null) 
              //  return RedirectToAction("Login");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePassword(string oldPassword, string newPassword)
        {
            try
            {
                //int? empId = HttpContext.Session.GetInt32("EmpID");
                //if (empId == null) return RedirectToAction("Login");
                var sessionEmpId = Convert.ToInt32(HttpContext.Session.GetString("Ids"));
                var user = await _profileupdateservice.GetEmployeeById(sessionEmpId); // Use actual empId here

                if (user == null)
                    return Json(new { success = false, message = "Session expired. Please log in again." });

                if (!project.Services.PasswordHasher.Verify(oldPassword, user.EmpPassword))
                {
                    return Json(new { success = false, message = "Old password is incorrect!" });
                }

                if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6 || newPassword.Length > 100)
                    return Json(new { success = false, message = "New password must be 6 to 100 characters long." });

                await _profileupdateservice.UpdatePassword(sessionEmpId, project.Services.PasswordHasher.Hash(newPassword));
                return Json(new { success = true, message = "Password updated successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}