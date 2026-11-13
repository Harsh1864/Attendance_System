using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using project.Services;

namespace project.Controllers
{
    /// <summary>An employee's own monthly attendance calendar and totals.</summary>
    public class MyReportController : Controller
    {
        private readonly ReportService _reports;

        public MyReportController(ReportService reports) => _reports = reports;

        private int? SessionEmpId =>
            int.TryParse(HttpContext.Session.GetString("Ids"), out var id) && id > 0 ? id : null;

        public IActionResult Index()
        {
            if (SessionEmpId == null) return RedirectToAction("Index", "Home");
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Month(int? year, int? month)
        {
            if (SessionEmpId is not int empId) return Unauthorized();

            var y = year ?? DateTime.Today.Year;
            var m = month ?? DateTime.Today.Month;
            var error = ReportService.ValidateMonth(y, m, DateTime.Today);
            if (error != null) return BadRequest(error);

            var data = await _reports.GetEmployeeMonthAsync(empId, y, m, DateTime.Today);
            return data == null ? NotFound() : Json(data);
        }
    }
}
