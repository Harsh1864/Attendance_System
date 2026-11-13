using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using project.Services;

namespace project.Controllers
{
    /// <summary>Department management. Under /Admin so the Admin role is required.</summary>
    [Route("Admin/Departments")]
    public class AdminDepartmentsController : Controller
    {
        private readonly DepartmentService _departments;
        private readonly AuditService _audit;

        public AdminDepartmentsController(DepartmentService departments, AuditService audit)
        {
            _departments = departments;
            _audit = audit;
        }

        [HttpGet("")]
        public IActionResult Index() => View("~/Views/Admin/Departments.cshtml");

        [HttpGet("List")]
        public async Task<IActionResult> List() => Json(await _departments.ListAsync());

        [HttpGet("Employees")]
        public async Task<IActionResult> Employees() => Json(await _departments.EmployeesAsync());

        [HttpPost("Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int? id, string? name, string? description)
        {
            var error = DepartmentRules.Validate(name, description);
            if (error != null) return Json(new { success = false, message = error });

            var (savedId, saveError) = await _departments.SaveAsync(id, name!, description);
            if (saveError != null) return Json(new { success = false, message = saveError });

            await _audit.LogAsync(AuditService.ActorFrom(HttpContext),
                id.HasValue ? AuditActions.DepartmentUpdated : AuditActions.DepartmentCreated,
                "Department", savedId, name!.Trim());
            return Json(new { success = true, id = savedId, message = id.HasValue ? "Department updated." : "Department created." });
        }

        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var name = await _departments.DeleteAsync(id);
            if (name == null) return Json(new { success = false, message = "Department not found." });

            await _audit.LogAsync(AuditService.ActorFrom(HttpContext), AuditActions.DepartmentDeleted, "Department", id, name);
            return Json(new { success = true, message = "Department deleted. Its employees are now unassigned." });
        }

        [HttpPost("Assign")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(int[]? empIds, int? departmentId)
        {
            var ids = DepartmentRules.CleanIds(empIds);
            if (ids.Length == 0) return Json(new { success = false, message = "Choose at least one employee." });

            var changed = await _departments.AssignAsync(ids, departmentId);
            if (changed == 0) return Json(new { success = false, message = "No employees were changed." });

            await _audit.LogAsync(AuditService.ActorFrom(HttpContext), AuditActions.DepartmentMembersChanged,
                "Department", departmentId,
                (departmentId.HasValue ? "Assigned" : "Unassigned") + $" employees {string.Join(", ", ids)}");
            return Json(new { success = true, changed, message = $"{changed} employee(s) updated." });
        }
    }
}
