using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using project.Services;

namespace project.Controllers
{
    /// <summary>Holiday calendar management. Lives under /Admin so AuthMiddleware requires the Admin role.</summary>
    [Route("Admin/Holidays")]
    public class AdminHolidaysController : Controller
    {
        private readonly HolidayService _holidays;
        private readonly AuditService _audit;

        public AdminHolidaysController(HolidayService holidays, AuditService audit)
        {
            _holidays = holidays;
            _audit = audit;
        }

        [HttpGet("")]
        public IActionResult Index() => View("~/Views/Admin/Holidays.cshtml");

        [HttpGet("List")]
        public async Task<IActionResult> List(int? year)
        {
            var y = year ?? DateTime.Today.Year;
            if (y < 2000 || y > 2100) return BadRequest("Invalid year.");
            return Json(await _holidays.GetYearAsync(y));
        }

        [HttpPost("Add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(DateTime? date, string? name)
        {
            var error = HolidayRules.Validate(date, name, DateTime.Today);
            if (error != null) return Json(new { success = false, message = error });

            var actor = AuditService.ActorFrom(HttpContext);
            var (id, addError) = await _holidays.AddAsync(date!.Value, name!, actor.Id);
            if (addError != null) return Json(new { success = false, message = addError });

            await _audit.LogAsync(actor, AuditActions.HolidayAdded, "Holiday", id, $"{date:yyyy-MM-dd} {name!.Trim()}");
            return Json(new { success = true, id, message = "Holiday added." });
        }

        [HttpPost("Remove")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int id)
        {
            var removed = await _holidays.RemoveAsync(id);
            if (removed == null) return Json(new { success = false, message = "Holiday not found." });

            await _audit.LogAsync(AuditService.ActorFrom(HttpContext), AuditActions.HolidayRemoved, "Holiday", id,
                $"{removed.Date:yyyy-MM-dd} {removed.Name}");
            return Json(new { success = true, message = "Holiday removed." });
        }
    }
}
