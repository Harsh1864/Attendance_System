using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using project.Services;

namespace project.Controllers
{
    /// <summary>Admin review of employee correction requests. Under /Admin so the Admin role is required.</summary>
    [Route("Admin/Corrections")]
    public class AdminCorrectionsController : Controller
    {
        private readonly CorrectionService _corrections;
        private readonly AuditService _audit;
        private readonly NotificationService _notifications;
        private readonly ShiftOptions _shift;

        public AdminCorrectionsController(CorrectionService corrections, AuditService audit, NotificationService notifications,
            IOptions<ShiftOptions> shift)
        {
            _corrections = corrections;
            _audit = audit;
            _notifications = notifications;
            _shift = shift.Value;
        }

        [HttpGet("")]
        public IActionResult Index() => View("~/Views/Admin/Corrections.cshtml");

        [HttpGet("List")]
        public async Task<IActionResult> List(string? status, DateTime? from, DateTime? to)
        {
            var end = (to ?? DateTime.Today).Date;
            var start = (from ?? end.AddDays(-CorrectionRules.MaxDaysBack)).Date;
            if (start > end) return BadRequest("'from' must be on or before 'to'.");
            return Json(await _corrections.SearchAsync(status, start, end));
        }

        [HttpGet("PendingCount")]
        public async Task<IActionResult> PendingCount() => Json(new { count = await _corrections.CountPendingAsync() });

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
            var (c, applied, error) = await _corrections.ReviewAsync(
                id, approve, actor.Id, note, DateTime.Today, _shift.StartTime, _shift.EndTime);
            if (error != null) return Json(new { success = false, message = error });

            var details = $"{c!.EmpName}: {c.Date:yyyy-MM-dd}";
            if (applied != null) details += $"; Applied {AttendanceEditRules.Describe(applied)} (was {c.CurrentRecord ?? "no record"})";
            if (!string.IsNullOrWhiteSpace(note)) details += $"; Note: {note.Trim()}";

            await _audit.LogAsync(actor, approve ? AuditActions.CorrectionApproved : AuditActions.CorrectionRejected,
                "Correction", id, details);
            await _notifications.SendAsync(c.EmpId, NotificationMessages.CorrectionDecision(c.Date, approve, note));

            return Json(new { success = true, message = approve ? "Correction approved and applied." : "Correction rejected." });
        }
    }
}
