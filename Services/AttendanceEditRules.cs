using System;
using System.Linq;

namespace project.Services
{
    /// <summary>Values an admin submits when adding or correcting an attendance record.</summary>
    public class AttendanceEditInput
    {
        public int? AttendId { get; set; }
        public int EmpId { get; set; }
        public DateTime? Date { get; set; }

        /// <summary>"HH:mm"; empty when marking absent.</summary>
        public string? ClockIn { get; set; }

        /// <summary>"HH:mm"; empty when the employee has not checked out.</summary>
        public string? ClockOut { get; set; }
        public string? WorkType { get; set; }
        public string? TaskType { get; set; }
        public bool Absent { get; set; }

        /// <summary>Why the record is being changed. Required, stored in the audit log.</summary>
        public string? Reason { get; set; }
    }

    /// <summary>A validated record ready to be written.</summary>
    public record AttendanceEditResult(
        DateTime Date,
        TimeSpan? ClockIn,
        TimeSpan? ClockOut,
        string? WorkType,
        string? TaskType,
        int? WorkingHours,
        string Status);

    public static class AttendanceEditRules
    {
        public static readonly string[] WorkTypes = { "Office", "Remote", "Field" };
        public const int MaxTaskLength = 100;
        public const int MaxReasonLength = 300;

        public static bool TryParseTime(string? value, out TimeSpan time)
        {
            time = default;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var parts = value.Trim().Split(':');
            if (parts.Length != 2
                || !int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m)
                || h < 0 || h > 23 || m < 0 || m > 59)
                return false;
            time = new TimeSpan(h, m, 0);
            return true;
        }

        /// <summary>
        /// Validates an edit and works out status and hours with the same rules
        /// as employee check-in/out. Returns (result, null) or (null, error).
        /// </summary>
        public static (AttendanceEditResult? result, string? error) Validate(
            AttendanceEditInput input, DateTime today, TimeSpan shiftStart, TimeSpan shiftEnd)
        {
            if (input.EmpId <= 0) return (null, "Please choose an employee.");
            if (!input.Date.HasValue) return (null, "Date is required.");
            var date = input.Date.Value.Date;
            if (date > today.Date) return (null, "Attendance cannot be recorded for a future date.");
            if (date < today.Date.AddYears(-1)) return (null, "Records older than one year cannot be changed.");

            if (string.IsNullOrWhiteSpace(input.Reason)) return (null, "Please give a reason for the change.");
            if (input.Reason.Trim().Length > MaxReasonLength) return (null, $"Reason cannot exceed {MaxReasonLength} characters.");

            if (input.Absent)
                return (new AttendanceEditResult(date, null, null, null, null, null, AttendanceRules.Absent), null);

            if (!TryParseTime(input.ClockIn, out var clockIn)) return (null, "Clock-in time is required (HH:mm).");

            TimeSpan? clockOut = null;
            if (!string.IsNullOrWhiteSpace(input.ClockOut))
            {
                if (!TryParseTime(input.ClockOut, out var outTime)) return (null, "Clock-out time must be HH:mm.");
                if (outTime <= clockIn) return (null, "Clock-out must be after clock-in.");
                clockOut = outTime;
            }
            else if (date < today.Date)
            {
                return (null, "Clock-out time is required for past days.");
            }

            if (string.IsNullOrWhiteSpace(input.WorkType) || !WorkTypes.Contains(input.WorkType))
                return (null, "Work type must be Office, Remote or Field.");

            var tasks = string.IsNullOrWhiteSpace(input.TaskType) ? null : input.TaskType.Trim();
            if (tasks != null && tasks.Length > MaxTaskLength) return (null, $"Tasks cannot exceed {MaxTaskLength} characters.");

            string status;
            int? hours;
            if (clockOut.HasValue)
            {
                status = AttendanceRules.CheckOutStatus(clockIn, clockOut.Value, shiftStart, shiftEnd);
                hours = AttendanceRules.WorkedHours(clockIn, clockOut.Value);
            }
            else
            {
                status = AttendanceRules.CheckInStatus(clockIn, shiftStart);
                hours = null;
            }

            return (new AttendanceEditResult(date, clockIn, clockOut, input.WorkType, tasks, hours, status), null);
        }

        /// <summary>Human-readable one-line summary for the audit log.</summary>
        public static string Describe(AttendanceEditResult r)
        {
            if (r.Status == AttendanceRules.Absent) return $"{r.Date:yyyy-MM-dd} Absent";
            var outText = r.ClockOut.HasValue ? $"{r.ClockOut.Value:hh\\:mm}" : "--:--";
            return $"{r.Date:yyyy-MM-dd} {r.ClockIn!.Value:hh\\:mm}-{outText} {r.WorkType} {r.Status}" +
                   (r.WorkingHours.HasValue ? $" {r.WorkingHours}h" : "");
        }
    }
}
