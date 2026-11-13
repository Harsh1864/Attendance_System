using System;

namespace project.Services
{
    /// <summary>Shift settings bound from the "Shift" section of appsettings.json.</summary>
    public class ShiftOptions
    {
        /// <summary>Latest on-time check-in, e.g. "09:15".</summary>
        public string Start { get; set; } = "09:15";

        /// <summary>Earliest on-time check-out, e.g. "17:00".</summary>
        public string End { get; set; } = "17:00";

        /// <summary>Time of day the absentee job runs, e.g. "18:00".</summary>
        public string AbsenteeRunTime { get; set; } = "18:00";

        /// <summary>When true, the absentee job does not run on Saturday or Sunday.</summary>
        public bool SkipWeekends { get; set; } = true;

        public TimeSpan StartTime => ParseOr(Start, new TimeSpan(9, 15, 0));
        public TimeSpan EndTime => ParseOr(End, new TimeSpan(17, 0, 0));
        public TimeSpan AbsenteeRunTimeOfDay => ParseOr(AbsenteeRunTime, new TimeSpan(18, 0, 0));

        private static TimeSpan ParseOr(string? value, TimeSpan fallback) =>
            TimeSpan.TryParse(value, out var t) && t >= TimeSpan.Zero && t < TimeSpan.FromDays(1) ? t : fallback;
    }

    /// <summary>
    /// Pure attendance rules (no database access) so they can be unit tested.
    /// Status values match the c_attendstatus values used in the database.
    /// </summary>
    public static class AttendanceRules
    {
        public const string Regular = "Regular";
        public const string LateIn = "LateIn";
        public const string EarlyOut = "EarlyOut";
        public const string LateEarly = "LateEarly";
        public const string Absent = "Absent";

        public static string CheckInStatus(TimeSpan clockIn, TimeSpan shiftStart) =>
            clockIn > shiftStart ? LateIn : Regular;

        public static string CheckOutStatus(TimeSpan? clockIn, TimeSpan clockOut, TimeSpan shiftStart, TimeSpan shiftEnd)
        {
            var late = clockIn.HasValue && clockIn.Value > shiftStart;
            var early = clockOut < shiftEnd;
            return (late, early) switch
            {
                (true, true) => LateEarly,
                (true, false) => LateIn,
                (false, true) => EarlyOut,
                _ => Regular,
            };
        }

        /// <summary>
        /// Whole hours worked between clock-in and clock-out, rounded down.
        /// Always at least 1 because the database requires c_workinghour &gt; 0.
        /// </summary>
        public static int WorkedHours(TimeSpan? clockIn, TimeSpan clockOut)
        {
            if (!clockIn.HasValue) return 1;
            var minutes = (clockOut - clockIn.Value).TotalMinutes;
            var hours = (int)Math.Floor(minutes / 60.0);
            return hours < 1 ? 1 : hours;
        }

        public static bool IsWeekend(DateTime date) =>
            date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

        /// <summary>Time until the next occurrence of <paramref name="runAt"/> after <paramref name="now"/>.</summary>
        public static TimeSpan DelayUntilNext(DateTime now, TimeSpan runAt)
        {
            var next = now.Date + runAt;
            if (next <= now) next = next.AddDays(1);
            return next - now;
        }
    }
}
