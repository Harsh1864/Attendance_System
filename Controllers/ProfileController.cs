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
    public class ProfileController : Controller
    {
        private readonly ProfileService _profileService;
        private readonly IWebHostEnvironment _env;

        private static readonly HashSet<string> AllowedImageExtensions = new() { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private const long MaxImageBytes = 2 * 1024 * 1024;

        public ProfileController(ProfileService profileService, IWebHostEnvironment env)
        {
            _profileService = profileService;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            var sessionEmpId = Convert.ToInt32(HttpContext.Session.GetString("Ids"));
            var emp = await _profileService.GetProfileByID(sessionEmpId);
            return View(emp);
        }
        [HttpPost]
        public async Task<IActionResult> Index2([FromForm] EmployeeModel emp)
        {
            try
            {
                // Only allow changing your own picture; never trust the posted email.
                var sessionMail = HttpContext.Session.GetString("Mail");
                if (string.IsNullOrEmpty(sessionMail))
                    return Unauthorized(new { success = false, message = "Session expired. Please log in again." });
                emp.EmpMail = sessionMail;

                if (emp.ContactPicture != null && emp.ContactPicture.Length > 0)
                {
                    var ext = Path.GetExtension(emp.ContactPicture.FileName).ToLowerInvariant();
                    if (!AllowedImageExtensions.Contains(ext))
                        return BadRequest(new { success = false, message = "Only JPG, PNG, GIF or WEBP images are allowed." });

                    if (emp.ContactPicture.Length > MaxImageBytes)
                        return BadRequest(new { success = false, message = "Image must be 2 MB or smaller." });

                    var profileDir = Path.Combine(_env.WebRootPath, "profiles");
                    if (!Directory.Exists(profileDir))
                        Directory.CreateDirectory(profileDir);

                    var fileName = emp.EmpMail + Path.GetExtension(emp.ContactPicture.FileName);
                    var profilePath = Path.Combine(profileDir, fileName);

                    using (var stream = new FileStream(profilePath, FileMode.Create))
                    {
                        await emp.ContactPicture.CopyToAsync(stream);
                    }

                    emp.Profile = fileName;

                    var updated = await _profileService.UpdateProfileImage(emp.EmpMail, fileName);

                    if (updated)
                        return Ok(new { success = true, message = "Profile image updated successfully!" });
                    else
                        return BadRequest(new { success = false, message = "Image saved but database not updated." });
                }
                else
                {
                    return BadRequest(new { success = false, message = "No file selected." });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }


    }
}