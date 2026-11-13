using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using project.Models;

namespace project.Services
{
    /// <summary>
    /// Builds monthly attendance reports from raw rows. Pure (no database
    /// access) so the counting rules can be unit tested.
    /// </summary>
    public static class MonthlyReportBuilder
    {
        public const string Present = "Present";
        public const string Late = "Late";
        public const string EarlyOut = "EarlyOut";
        public const string LateEarly = "LateEarly";
        public const string Absent = "Absent";
        public const string Leave = "Leave";
        public const string Holiday = "Holiday";
        public const string Weekend = "Weekend";
        public const string NoRecord = "NoRecord";
        public const string Future = "Future";

        public record EmployeeInfo(int EmpId, string Name, string Status);

        public static (DateTime start, DateTime end) MonthRange(int year, int month)
        {
            var start = new DateTime(year, month, 1);
            return (start, start.AddMonths(1).AddDays(-1));
        }

        /// <summary>Day-by-day statuses for one employee.</summary>
        public static List<DailyStatusModel> BuildDays(
            int year, int month, DateTime today,
            IEnumerable<AttendanceModel> attendance,
            IEnumerable<LeaveModel> approvedLeave,
            IReadOnlyDictionary<DateTime, string> holidays,
            bool skipWeekends)
        {
            var (start, end) = MonthRange(year, month);
            var byDate = attendance
                .GroupBy(a => a.AttendDate.Date)
                .ToDictionary(g => g.Key, g => g.First());
            var leaves = approvedLeave.Where(l => l.Status == LeaveRules.Approved).ToList();

            var days = new List<DailyStatusModel>();
            for (var d = start; d <= end; d = d.AddDays(1))
            {
                var day = new DailyStatusModel { Date = d };
                byDate.TryGetValue(d, out var row);
                var leave = leaves.FirstOrDefault(l => d >= l.FromDate.Date && d <= l.ToDate.Date);

                if (row != null && !IsAbsent(row))
                {
                    // An attendance row wins: the employee worked that day.
                    day.Kind = KindFor(row.AttendStatus);
                    day.ClockIn = CsvExport.FormatTime(row.ClockInHour, row.ClockInMin);
                    day.ClockOut = row.ClockOutHour == 0 && row.ClockOutMin == 0
                        ? null
                        : CsvExport.FormatTime(row.ClockOutHour, row.ClockOutMin);
                    day.Hours = row.WorkingHour;
                    day.WorkType = row.WorkType;
                    day.Detail = string.IsNullOrWhiteSpace(row.TaskType) || row.TaskType == "N/A" ? null : row.TaskType;
                }
                else if (holidays.TryGetValue(d, out var holidayName))
                {
                    day.Kind = Holiday;
                    day.Detail = holidayName;
                }
                else if (skipWeekends && AttendanceRules.IsWeekend(d))
                {
                    day.Kind = Weekend;
                }
                else if (leave != null)
                {
                    day.Kind = Leave;
                    day.Detail = leave.LeaveType + (leave.HalfDay ? " (half day)" : "");
                }
                else if (row != null)
                {
                    day.Kind = Absent;
                }
                else
                {
                    day.Kind = d > today.Date ? Future : NoRecord;
                }

                days.Add(day);
            }
            return days;
        }

        /// <summary>Totals for one employee from their daily statuses.</summary>
        public static MonthlyReportRow Summarise(EmployeeInfo emp, IReadOnlyList<DailyStatusModel> days, DateTime today, IEnumerable<LeaveModel> approvedLeave)
        {
            var worked = days.Where(d => d.Kind is Present or Late or EarlyOut or LateEarly).ToList();
            var leaveByDate = approvedLeave.Where(l => l.Status == LeaveRules.Approved).ToList();

            decimal leaveDays = 0;
            foreach (var d in days.Where(x => x.Kind == Leave))
            {
                var l = leaveByDate.FirstOrDefault(x => d.Date >= x.FromDate.Date && d.Date <= x.ToDate.Date);
                leaveDays += l != null && l.HalfDay ? 0.5m : 1m;
            }

            // Working days = days that were not weekends, holidays or in the future.
            var workingDays = days.Count(d => d.Date <= today.Date && d.Kind is not (Weekend or Holiday or Future));
            var expected = workingDays - leaveDays;

            return new MonthlyReportRow
            {
                EmpId = emp.EmpId,
                EmpName = emp.Name,
                Status = emp.Status,
                WorkingDays = workingDays,
                PresentDays = worked.Count,
                LateDays = worked.Count(d => d.Kind is Late or LateEarly),
                EarlyOutDays = worked.Count(d => d.Kind is EarlyOut or LateEarly),
                AbsentDays = days.Count(d => d.Kind == Absent),
                LeaveDays = leaveDays,
                UnrecordedDays = days.Count(d => d.Kind == NoRecord),
                TotalHours = worked.Sum(d => d.Hours),
                AverageHours = worked.Count == 0 ? 0 : Math.Round((decimal)worked.Sum(d => d.Hours) / worked.Count, 1),
                AttendancePercent = expected <= 0 ? null : Math.Round(Math.Min(100m, worked.Count / expected * 100m), 1),
            };
        }

        /// <summary>Full report for every employee.</summary>
        public static MonthlyReportModel Build(
            int year, int month, DateTime today,
            IEnumerable<EmployeeInfo> employees,
            IEnumerable<AttendanceModel> attendance,
            IEnumerable<LeaveModel> approvedLeave,
            IReadOnlyDictionary<DateTime, string> holidays,
            bool skipWeekends)
        {
            var attByEmp = attendance.GroupBy(a => a.EmpId).ToDictionary(g => g.Key, g => g.ToList());
            var leaveByEmp = approvedLeave.GroupBy(l => l.EmpId).ToDictionary(g => g.Key, g => g.ToList());

            var report = new MonthlyReportModel
            {
                Year = year,
                Month = month,
                MonthName = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            };

            var (start, end) = MonthRange(year, month);
            report.Holidays = holidays.Keys.Count(d => d >= start && d <= end);

            foreach (var emp in employees.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            {
                var att = attByEmp.TryGetValue(emp.EmpId, out var a) ? a : new List<AttendanceModel>();
                var lv = leaveByEmp.TryGetValue(emp.EmpId, out var l) ? l : new List<LeaveModel>();

                // Inactive employees with no activity this month are left out.
                if (!string.Equals(emp.Status, "Active", StringComparison.OrdinalIgnoreCase) && att.Count == 0 && lv.Count == 0)
                    continue;

                var days = BuildDays(year, month, today, att, lv, holidays, skipWeekends);
                report.Rows.Add(Summarise(emp, days, today, lv));
            }

            var calendar = BuildDays(year, month, today, Array.Empty<AttendanceModel>(), Array.Empty<LeaveModel>(), holidays, skipWeekends);
            report.WorkingDays = calendar.Count(d => d.Date <= today.Date && d.Kind is not (Weekend or Holiday or Future));
            return report;
        }

        public static string ToCsv(MonthlyReportModel report)
        {
            var sb = new System.Text.StringBuilder();
            void Row(params string?[] values) => sb.Append(string.Join(",", values.Select(CsvExport.Escape))).Append("\r\n");

            Row("Month", report.MonthName);
            Row("Working days", report.WorkingDays.ToString(CultureInfo.InvariantCulture));
            Row();
            Row("Employee ID", "Employee", "Status", "Working Days", "Present", "Late", "Early Out", "Absent",
                "Leave", "No Record", "Total Hours", "Avg Hours", "Attendance %");
            foreach (var r in report.Rows)
            {
                Row(r.EmpId.ToString(CultureInfo.InvariantCulture), r.EmpName, r.Status,
                    r.WorkingDays.ToString(CultureInfo.InvariantCulture),
                    r.PresentDays.ToString(CultureInfo.InvariantCulture),
                    r.LateDays.ToString(CultureInfo.InvariantCulture),
                    r.EarlyOutDays.ToString(CultureInfo.InvariantCulture),
                    r.AbsentDays.ToString(CultureInfo.InvariantCulture),
                    r.LeaveDays.ToString("0.#", CultureInfo.InvariantCulture),
                    r.UnrecordedDays.ToString(CultureInfo.InvariantCulture),
                    r.TotalHours.ToString(CultureInfo.InvariantCulture),
                    r.AverageHours.ToString("0.0", CultureInfo.InvariantCulture),
                    r.AttendancePercent?.ToString("0.0", CultureInfo.InvariantCulture) ?? "");
            }
            return sb.ToString();
        }

        private static bool IsAbsent(AttendanceModel row) =>
            string.Equals(row.AttendStatus, AttendanceRules.Absent, StringComparison.OrdinalIgnoreCase);

        private static string KindFor(string? status) => status switch
        {
            AttendanceRules.LateIn => Late,
            AttendanceRules.EarlyOut => EarlyOut,
            AttendanceRules.LateEarly => LateEarly,
            _ => Present,
        };
    }
}
