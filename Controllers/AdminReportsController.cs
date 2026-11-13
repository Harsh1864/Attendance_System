using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using project.Services;

namespace project.Controllers
{
    /// <summary>Monthly reports for admins. Lives under /Admin so AuthMiddleware requires the Admin role.</summary>
    [Route("Admin/Reports")]
    public class AdminReportsController : Controller
    {
        private readonly ReportService _reports;

        public AdminReportsController(ReportService reports) => _reports = reports;

        [HttpGet("")]
        public IActionResult Index() => View("~/Views/Admin/Reports.cshtml");

        [HttpGet("Monthly")]
        public async Task<IActionResult> Monthly(int? year, int? month)
        {
            var (y, m) = Resolve(year, month);
            var error = ReportService.ValidateMonth(y, m, DateTime.Today);
            if (error != null) return BadRequest(error);
            return Json(await _reports.GetMonthlyAsync(y, m, DateTime.Today));
        }

        [HttpGet("Employee")]
        public async Task<IActionResult> Employee(int empId, int? year, int? month)
        {
            var (y, m) = Resolve(year, month);
            var error = ReportService.ValidateMonth(y, m, DateTime.Today);
            if (error != null) return BadRequest(error);

            var data = await _reports.GetEmployeeMonthAsync(empId, y, m, DateTime.Today);
            return data == null ? NotFound() : Json(data);
        }

        [HttpGet("MonthlyCsv")]
        public async Task<IActionResult> MonthlyCsv(int? year, int? month)
        {
            var (y, m) = Resolve(year, month);
            var error = ReportService.ValidateMonth(y, m, DateTime.Today);
            if (error != null) return BadRequest(error);

            var report = await _reports.GetMonthlyAsync(y, m, DateTime.Today);
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(MonthlyReportBuilder.ToCsv(report))).ToArray();
            return File(bytes, "text/csv", $"monthly_report_{y:0000}_{m:00}.csv");
        }

        private static (int year, int month) Resolve(int? year, int? month) =>
            (year ?? DateTime.Today.Year, month ?? DateTime.Today.Month);
    }
}
