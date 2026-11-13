using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using project.Services;

namespace project.Controllers
{
    /// <summary>Leave review for admins. Lives under /Admin so AuthMiddleware requires the Admin role.</summary>
    [Route("Admin/Leave")]
    public class AdminLeaveController : Controller
    {
        private readonly LeaveService _leave;
        private readonly AuditService _audit;
        private readonly NotificationService _notifications;

        public AdminLeaveController(LeaveService leave, AuditService audit, NotificationService notifications)
        {
            _leave = leave;
            _audit = audit;
            _notifications = notifications;
        }

        [HttpGet("")]
        public IActionResult Index() => View("~/Views/Admin/Leave.cshtml");

        [HttpGet("List")]
        public async Task<IActionResult> List(DateTime? from, DateTime? to, string? status, int? empId)
        {
            var start = (from ?? DateTime.Today.AddMonths(-1)).Date;
            var end = (to ?? DateTime.Today.AddMonths(3)).Date;
            if (start > end) return BadRequest("'from' must be on or before 'to'.");
            if ((end - start).TotalDays > 731) return BadRequest("Date range cannot exceed two years.");

            return Json(await _leave.SearchAsync(start, end, status, empId));
        }

        [HttpGet("PendingCount")]
        public async Task<IActionResult> PendingCount() => Json(new { count = await _leave.CountPendingAsync() });

        [HttpPost("Review")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(int id, string decision, string? note)
        {
            bool approve;
            if (string.Equals(decision, "approve", StringComparison.OrdinalIgnoreCase)) approve = true;
            else if (string.Equals(decision, "reject", StringComparison.OrdinalIgnoreCase)) approve = false;
            else return Json(new { success = false, message = "Decision must be approve or reject." });

            if (!approve && string.IsNullOrWhiteSpace(note))
                return Json(new { success = false, message = "Please give a reason when rejecting." });

            var actor = AuditService.ActorFrom(HttpContext);
            var (leave, error) = await _leave.ReviewAsync(id, approve, actor.Id, note);
            if (error != null) return Json(new { success = false, message = error });

            await _audit.LogAsync(actor, approve ? AuditActions.LeaveApproved : AuditActions.LeaveRejected, "Leave", id,
                $"{leave!.EmpName}: {leave.LeaveType} {leave.FromDate:yyyy-MM-dd} to {leave.ToDate:yyyy-MM-dd}" +
                (string.IsNullOrWhiteSpace(note) ? "" : $"; Note: {note.Trim()}"));

            await _notifications.SendAsync(leave.EmpId, NotificationMessages.LeaveDecision(leave, approve, note));

            return Json(new { success = true, message = approve ? "Leave approved." : "Leave rejected." });
        }
    }
}
