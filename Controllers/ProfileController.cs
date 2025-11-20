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
                if (emp.ContactPicture != null && emp.ContactPicture.Length > 0)
                {
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