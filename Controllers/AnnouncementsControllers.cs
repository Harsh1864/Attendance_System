using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using project.Services;

namespace project.Controllers
{
    /// <summary>Announcements visible to any logged-in user.</summary>
    public class AnnouncementsController : Controller
    {
        private readonly AnnouncementService _announcements;

        public AnnouncementsController(AnnouncementService announcements) => _announcements = announcements;

        public IActionResult Index()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("Ids"))) return RedirectToAction("Index", "Home");
            ViewBag.IsAdmin = HttpContext.Session.GetString("Role") == "Admin";
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Active() => Json(await _announcements.ActiveAsync(DateTime.Today));
    }

    /// <summary>Announcement management. Under /Admin so the Admin role is required.</summary>
    [Route("Admin/Announcements")]
    public class AdminAnnouncementsController : Controller
    {
        private readonly AnnouncementService _announcements;
        private readonly AuditService _audit;
        private readonly NotificationService _notifications;

        public AdminAnnouncementsController(AnnouncementService announcements, AuditService audit, NotificationService notifications)
        {
            _announcements = announcements;
            _audit = audit;
            _notifications = notifications;
        }

        [HttpGet("")]
        public IActionResult Index() => View("~/Views/Admin/Announcements.cshtml");

        [HttpGet("List")]
        public async Task<IActionResult> List() => Json(await _announcements.ListAllAsync(DateTime.Today));

        [HttpPost("Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([FromForm] AnnouncementInput input)
        {
            var error = AnnouncementRules.Validate(input, DateTime.Today);
            if (error != null) return Json(new { success = false, message = error });

            var actor = AuditService.ActorFrom(HttpContext);
            var isNew = !input.AnnouncementId.HasValue;
            var (id, saveError) = await _announcements.SaveAsync(input, actor.Id);
            if (saveError != null) return Json(new { success = false, message = saveError });

            await _audit.LogAsync(actor, isNew ? AuditActions.AnnouncementPosted : AuditActions.AnnouncementUpdated,
                "Announcement", id, $"{input.Priority}: {input.Title!.Trim()}");

            // Important notices that are live today also go to everyone's notification bell.
            var liveToday = AnnouncementRules.StateOn(input.StartDate!.Value, input.EndDate, DateTime.Today) == AnnouncementRules.Active;
            if (isNew && input.Priority == AnnouncementRules.Important && liveToday)
                await _notifications.SendToActiveEmployeesAsync(NotificationMessages.Announcement(input.Title!.Trim()));

            return Json(new { success = true, id, message = isNew ? "Announcement posted." : "Announcement updated." });
        }

        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var title = await _announcements.DeleteAsync(id);
            if (title == null) return Json(new { success = false, message = "Announcement not found." });

            await _audit.LogAsync(AuditService.ActorFrom(HttpContext), AuditActions.AnnouncementDeleted, "Announcement", id, title);
            return Json(new { success = true, message = "Announcement deleted." });
        }
    }
}
