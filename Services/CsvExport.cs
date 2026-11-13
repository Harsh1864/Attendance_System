using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using project.Models;

namespace project.Services
{
    /// <summary>Builds RFC 4180 CSV files for attendance exports.</summary>
    public static class CsvExport
    {
        public static readonly string[] AttendanceHeader =
        {
            "Date", "Employee ID", "Employee Name", "Clock In", "Clock Out",
            "Work Type", "Tasks", "Working Hours", "Status",
        };

        public static string BuildAttendanceCsv(IEnumerable<AttendanceModel> rows)
        {
            var sb = new StringBuilder();
            AppendRow(sb, AttendanceHeader);

            foreach (var r in rows)
            {
                var absent = string.Equals(r.AttendStatus, AttendanceRules.Absent, StringComparison.OrdinalIgnoreCase);
                AppendRow(sb, new[]
                {
                    r.AttendDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    r.EmpId.ToString(CultureInfo.InvariantCulture),
                    r.EmpName,
                    absent ? "" : FormatTime(r.ClockInHour, r.ClockInMin),
                    absent || (r.ClockOutHour == 0 && r.ClockOutMin == 0) ? "" : FormatTime(r.ClockOutHour, r.ClockOutMin),
                    r.WorkType,
                    r.TaskType,
                    r.WorkingHour.ToString(CultureInfo.InvariantCulture),
                    r.AttendStatus,
                });
            }

            return sb.ToString();
        }

        public static string FormatTime(int hour, int minute) =>
            $"{hour:00}:{minute:00}";

        /// <summary>
        /// Quotes a value when needed and neutralises spreadsheet formulas
        /// (values starting with = + - @ are prefixed with an apostrophe).
        /// </summary>
        public static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";

            if ("=+-@\t\r".IndexOf(value[0]) >= 0)
                value = "'" + value;

            var needsQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
                              || value[0] == ' ' || value[^1] == ' ';
            return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        private static void AppendRow(StringBuilder sb, IEnumerable<string?> values)
        {
            var first = true;
            foreach (var v in values)
            {
                if (!first) sb.Append(',');
                sb.Append(Escape(v));
                first = false;
            }
            sb.Append("\r\n");
        }
    }
}
