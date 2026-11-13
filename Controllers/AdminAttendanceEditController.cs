using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using project.Services;

namespace project.Controllers
{
    /// <summary>
    /// Lets admins add, correct and delete attendance records. Every change
    /// needs a reason and is written to the audit log with before/after values.
    /// Lives under /Admin so AuthMiddleware requires the Admin role.
    /// </summary>
    [Route("Admin/AttendanceEdit")]
    public class AdminAttendanceEditController : Controller
    {
        private readonly AttendanceEditService _edit;
        private readonly AuditService _audit;
        private readonly ShiftOptions _shift;
        private readonly NotificationService _notifications;

        public AdminAttendanceEditController(AttendanceEditService edit, AuditService audit, IOptions<ShiftOptions> shift,
            NotificationService notifications)
        {
            _notifications = notifications;
            _edit = edit;
            _audit = audit;
            _shift = shift.Value;
        }

        [HttpGet("")]
        public IActionResult Index()
        {
            ViewBag.WorkTypes = AttendanceEditRules.WorkTypes;
            return View("~/Views/Admin/AttendanceEdit.cshtml");
        }

        [HttpGet("Search")]
        public async Task<IActionResult> Search(int? empId, DateTime? from, DateTime? to)
        {
            var end = (to ?? DateTime.Today).Date;
            var start = (from ?? end.AddDays(-13)).Date;
            if (start > end) return BadRequest("'from' must be on or before 'to'.");
            if ((end - start).TotalDays > 366) return BadRequest("Date range cannot exceed one year.");
            return Json(await _edit.SearchAsync(empId, start, end));
        }

        [HttpPost("Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([FromForm] AttendanceEditInput input)
        {
            var (result, error) = AttendanceEditRules.Validate(input, DateTime.Today, _shift.StartTime, _shift.EndTime);
            if (error != null) return Json(new { success = false, message = error });

            if (!await _edit.IsEditableEmployeeAsync(input.EmpId))
                return Json(new { success = false, message = "Employee not found." });

            var (id, before, saveError) = await _edit.SaveAsync(input.AttendId, input.EmpId, result!);
            if (saveError != null) return Json(new { success = false, message = saveError });

            var after = AttendanceEditRules.Describe(result!);
            var details = before == null
                ? $"Employee {input.EmpId}: {after}; Reason: {input.Reason!.Trim()}"
                : $"Employee {input.EmpId}: {AttendanceEditService.Describe(before)} -> {after}; Reason: {input.Reason!.Trim()}";

            await _audit.LogAsync(AuditService.ActorFrom(HttpContext),
                before == null ? AuditActions.AttendanceCreated : AuditActions.AttendanceEdited,
                "Attendance", id, details);

            await _notifications.SendAsync(input.EmpId, NotificationMessages.AttendanceChanged(after, input.Reason!));

            return Json(new
            {
                success = true,
                id,
                status = result!.Status,
                hours = result.WorkingHours,
                message = before == null ? "Record added." : "Record updated.",
            });
        }

        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return Json(new { success = false, message = "Please give a reason for deleting this record." });
            if (reason.Trim().Length > AttendanceEditRules.MaxReasonLength)
                return Json(new { success = false, message = $"Reason cannot exceed {AttendanceEditRules.MaxReasonLength} characters." });

            var deleted = await _edit.DeleteAsync(id);
            if (deleted == null) return Json(new { success = false, message = "Attendance record not found." });

            await _audit.LogAsync(AuditService.ActorFrom(HttpContext), AuditActions.AttendanceDeleted, "Attendance", id,
                $"{deleted.EmpName} (employee {deleted.EmpId}): {AttendanceEditService.Describe(deleted)}; Reason: {reason.Trim()}");

            await _notifications.SendAsync(deleted.EmpId,
                NotificationMessages.AttendanceChanged($"record for {deleted.Date:dd MMM yyyy} removed", reason));

            return Json(new { success = true, message = "Record deleted." });
        }
    }
}
