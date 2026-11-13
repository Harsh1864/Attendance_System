using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using project.Services;

namespace project.Controllers
{
    /// <summary>Employee requests to correct their own attendance.</summary>
    public class CorrectionsController : Controller
    {
        private readonly CorrectionService _corrections;
        private readonly AuditService _audit;
        private readonly NotificationService _notifications;
        private readonly ShiftOptions _shift;

        public CorrectionsController(CorrectionService corrections, AuditService audit, NotificationService notifications,
            IOptions<ShiftOptions> shift)
        {
            _corrections = corrections;
            _audit = audit;
            _notifications = notifications;
            _shift = shift.Value;
        }

        private int? SessionEmpId =>
            int.TryParse(HttpContext.Session.GetString("Ids"), out var id) && id > 0 ? id : null;

        public IActionResult Index()
        {
            if (SessionEmpId == null) return RedirectToAction("Index", "Home");
            ViewBag.WorkTypes = AttendanceEditRules.WorkTypes;
            ViewBag.MaxDaysBack = CorrectionRules.MaxDaysBack;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Mine()
        {
            if (SessionEmpId is not int empId) return Unauthorized();
            return Json(await _corrections.ListForEmployeeAsync(empId));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit([FromForm] AttendanceEditInput input)
        {
            if (SessionEmpId is not int empId)
                return Json(new { success = false, message = "Session expired. Please log in again." });

            input.EmpId = empId;
            var (result, error) = CorrectionRules.Validate(input, DateTime.Today, _shift.StartTime, _shift.EndTime);
            if (error != null) return Json(new { success = false, message = error });

            var (id, createError) = await _corrections.CreateAsync(empId, input, result!);
            if (createError != null) return Json(new { success = false, message = createError });

            var actor = AuditService.ActorFrom(HttpContext);
            await _audit.LogAsync(actor, AuditActions.CorrectionRequested, "Correction", id,
                $"{AttendanceEditRules.Describe(result!)}; Reason: {input.Reason!.Trim()}");

            var name = (await _corrections.GetAsync(id))?.EmpName ?? $"Employee {empId}";
            await _notifications.SendToAdminsAsync(NotificationMessages.CorrectionRequested(name, result!.Date));

            return Json(new { success = true, message = "Correction request sent to the admin." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            if (SessionEmpId is not int empId)
                return Json(new { success = false, message = "Session expired. Please log in again." });

            var error = await _corrections.CancelAsync(empId, id);
            if (error != null) return Json(new { success = false, message = error });

            await _audit.LogAsync(AuditService.ActorFrom(HttpContext), AuditActions.CorrectionCancelled, "Correction", id);
            return Json(new { success = true, message = "Request cancelled." });
        }
    }
}
