using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using project.Services;

namespace project.Controllers
{
    /// <summary>The logged-in user's notifications (employees and admins).</summary>
    public class NotificationsController : Controller
    {
        private readonly NotificationService _notifications;

        public NotificationsController(NotificationService notifications) => _notifications = notifications;

        private int? SessionEmpId =>
            int.TryParse(HttpContext.Session.GetString("Ids"), out var id) && id > 0 ? id : null;

        public IActionResult Index()
        {
            if (SessionEmpId == null) return RedirectToAction("Index", "Home");
            ViewBag.IsAdmin = HttpContext.Session.GetString("Role") == "Admin";
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            if (SessionEmpId is not int empId) return Unauthorized();
            return Json(await _notifications.ListAsync(empId));
        }

        [HttpGet]
        public async Task<IActionResult> UnreadCount()
        {
            if (SessionEmpId is not int empId) return Unauthorized();
            return Json(new { count = await _notifications.UnreadCountAsync(empId) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int? id)
        {
            if (SessionEmpId is not int empId) return Unauthorized();
            var changed = await _notifications.MarkReadAsync(empId, id);
            return Json(new { success = true, changed });
        }
    }
}
