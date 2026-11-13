using System;
using System.Collections.Generic;

namespace project.Models
{
    /// <summary>One employee's totals for a month.</summary>
    public class MonthlyReportRow
    {
        public int EmpId { get; set; }
        public string EmpName { get; set; } = "";
        public string? Department { get; set; }
        public string Status { get; set; } = "";

        /// <summary>Working days so far this month (excludes weekends, holidays and future days).</summary>
        public int WorkingDays { get; set; }
        public int PresentDays { get; set; }
        public int LateDays { get; set; }
        public int EarlyOutDays { get; set; }
        public int AbsentDays { get; set; }
        public decimal LeaveDays { get; set; }

        /// <summary>Past working days with no attendance row and no leave.</summary>
        public int UnrecordedDays { get; set; }
        public int TotalHours { get; set; }
        public decimal AverageHours { get; set; }

        /// <summary>Present days as a percentage of working days not on leave. Null when there were none.</summary>
        public decimal? AttendancePercent { get; set; }
    }

    public class MonthlyReportModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = "";
        public int WorkingDays { get; set; }
        public int Holidays { get; set; }
        public List<MonthlyReportRow> Rows { get; set; } = new();
    }

    /// <summary>What happened on one day for one employee.</summary>
    public class DailyStatusModel
    {
        public DateTime Date { get; set; }
        public string Kind { get; set; } = "";       // Present, Late, EarlyOut, LateEarly, Absent, Leave, Holiday, Weekend, NoRecord, Future
        public string? Detail { get; set; }           // holiday name, leave type, task list
        public string? ClockIn { get; set; }
        public string? ClockOut { get; set; }
        public int Hours { get; set; }
        public string? WorkType { get; set; }
    }

    public class EmployeeMonthModel
    {
        public MonthlyReportRow Summary { get; set; } = new();
        public List<DailyStatusModel> Days { get; set; } = new();
    }
}
