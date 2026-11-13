using System;
using System.Collections.Generic;
using System.Linq;
using project.Models;

namespace project.Services
{
    /// <summary>Leave settings bound from the "Leave" section of appsettings.json.</summary>
    public class LeaveOptions
    {
        /// <summary>Yearly days per leave type. Types missing here (e.g. Unpaid) are unlimited.</summary>
        public Dictionary<string, decimal> AnnualAllowance { get; set; } = new()
        {
            [LeaveRules.Casual] = 12,
            [LeaveRules.Sick] = 10,
            [LeaveRules.Earned] = 15,
        };

        /// <summary>How many days in the past a Sick leave may start (for reporting sickness afterwards).</summary>
        public int SickBackdateDays { get; set; } = 7;

        /// <summary>Longest single request, in calendar days.</summary>
        public int MaxSpanDays { get; set; } = 30;

        /// <summary>Weekends are not counted as leave days when true.</summary>
        public bool SkipWeekends { get; set; } = true;
    }

    /// <summary>Pure leave rules (no database access) so they can be unit tested.</summary>
    public static class LeaveRules
    {
        public const string Casual = "Casual";
        public const string Sick = "Sick";
        public const string Earned = "Earned";
        public const string Unpaid = "Unpaid";
        public static readonly string[] Types = { Casual, Sick, Earned, Unpaid };

        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";

        public const int MaxReasonLength = 500;

        /// <summary>
        /// Working days between two dates (inclusive), skipping weekends (optional)
        /// and holidays. A half-day single-date request counts as 0.5.
        /// </summary>
        public static decimal CountDays(DateTime from, DateTime to, ISet<DateTime> holidays, bool skipWeekends, bool halfDay)
        {
            if (to.Date < from.Date) return 0;

            decimal days = 0;
            for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
            {
                if (skipWeekends && AttendanceRules.IsWeekend(d)) continue;
                if (holidays.Contains(d)) continue;
                days++;
            }

            if (halfDay && from.Date == to.Date && days == 1) return 0.5m;
            return days;
        }

        /// <summary>True when the two inclusive date ranges share at least one day.</summary>
        public static bool Overlaps(DateTime aFrom, DateTime aTo, DateTime bFrom, DateTime bTo) =>
            aFrom.Date <= bTo.Date && bFrom.Date <= aTo.Date;

        /// <summary>Requests that still hold days (count towards balance and block overlaps).</summary>
        public static bool IsActive(string status) => status == Pending || status == Approved;

        /// <summary>
        /// Checks a request. Returns an error message, or null when it can be saved.
        /// <paramref name="days"/> is the result of <see cref="CountDays"/>.
        /// </summary>
        public static string? Validate(
            LeaveRequestInput input,
            decimal days,
            DateTime today,
            IEnumerable<LeaveModel> existing,
            IEnumerable<LeaveBalanceModel> balances,
            LeaveOptions options)
        {
            if (!input.FromDate.HasValue || !input.ToDate.HasValue) return "From and To dates are required.";
            var from = input.FromDate.Value.Date;
            var to = input.ToDate.Value.Date;

            if (string.IsNullOrWhiteSpace(input.LeaveType) || !Types.Contains(input.LeaveType))
                return "Please choose a valid leave type.";
            if (to < from) return "'To' date must be on or after 'From' date.";
            if ((to - from).TotalDays + 1 > options.MaxSpanDays)
                return $"A single request cannot be longer than {options.MaxSpanDays} days.";
            if (input.HalfDay && from != to) return "Half-day leave must be for a single date.";

            var earliest = input.LeaveType == Sick ? today.Date.AddDays(-options.SickBackdateDays) : today.Date;
            if (from < earliest)
                return input.LeaveType == Sick
                    ? $"Sick leave can start at most {options.SickBackdateDays} days in the past."
                    : "Leave cannot start in the past.";
            if (from > today.Date.AddYears(1)) return "Leave cannot be requested more than a year ahead.";

            if (input.Reason != null && input.Reason.Trim().Length > MaxReasonLength)
                return $"Reason cannot exceed {MaxReasonLength} characters.";

            if (days <= 0) return "The selected dates are all weekends or holidays.";

            var clash = existing.FirstOrDefault(l => IsActive(l.Status) && Overlaps(from, to, l.FromDate, l.ToDate));
            if (clash != null)
                return $"You already have {clash.Status.ToLowerInvariant()} leave from {clash.FromDate:dd-MMM} to {clash.ToDate:dd-MMM}.";

            var balance = balances.FirstOrDefault(b => b.LeaveType == input.LeaveType);
            if (balance?.Remaining is decimal remaining && days > remaining)
                return $"Not enough {input.LeaveType} leave: {remaining:0.#} day(s) left, {days:0.#} requested.";

            return null;
        }

        /// <summary>Pending leave can always be cancelled; approved leave only before it starts.</summary>
        public static bool CanCancel(string status, DateTime fromDate, DateTime today) =>
            status == Pending || (status == Approved && fromDate.Date > today.Date);

        public static bool CanReview(string status) => status == Pending;

        /// <summary>Builds balances for a year from the employee's requests.</summary>
        public static List<LeaveBalanceModel> Balances(IEnumerable<LeaveModel> leaves, int year, LeaveOptions options)
        {
            var inYear = leaves.Where(l => l.FromDate.Year == year).ToList();
            return Types.Select(t => new LeaveBalanceModel
            {
                LeaveType = t,
                Allowance = options.AnnualAllowance.TryGetValue(t, out var a) ? a : null,
                Approved = inYear.Where(l => l.LeaveType == t && l.Status == Approved).Sum(l => l.Days),
                Pending = inYear.Where(l => l.LeaveType == t && l.Status == Pending).Sum(l => l.Days),
            }).ToList();
        }
    }
}
