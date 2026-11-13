using project.Models;
using project.Services;

namespace AttendanceSystem.Tests;

public class LeaveRulesTests
{
    // Friday 13 Nov 2026
    private static readonly DateTime Today = new(2026, 11, 13);
    private static readonly HashSet<DateTime> NoHolidays = new();
    private static readonly LeaveOptions Options = new();

    private static DateTime D(int month, int day) => new(2026, month, day);

    private static LeaveRequestInput Req(DateTime from, DateTime to, string type = LeaveRules.Casual, bool half = false, string? reason = null) =>
        new() { FromDate = from, ToDate = to, LeaveType = type, HalfDay = half, Reason = reason };

    private static List<LeaveBalanceModel> Balances(params LeaveModel[] leaves) =>
        LeaveRules.Balances(leaves, 2026, Options);

    private static LeaveModel Leave(DateTime from, DateTime to, string status, string type = LeaveRules.Casual, decimal days = 1) =>
        new() { FromDate = from, ToDate = to, Status = status, LeaveType = type, Days = days };

    // ---- CountDays -------------------------------------------------------

    [Fact]
    public void CountDays_SkipsWeekends()
    {
        // Mon 16 to Sun 22 Nov = 5 working days
        Assert.Equal(5m, LeaveRules.CountDays(D(11, 16), D(11, 22), NoHolidays, skipWeekends: true, halfDay: false));
    }

    [Fact]
    public void CountDays_CountsWeekendsWhenConfigured()
    {
        Assert.Equal(7m, LeaveRules.CountDays(D(11, 16), D(11, 22), NoHolidays, skipWeekends: false, halfDay: false));
    }

    [Fact]
    public void CountDays_SkipsHolidays()
    {
        var holidays = new HashSet<DateTime> { D(11, 18) };
        Assert.Equal(4m, LeaveRules.CountDays(D(11, 16), D(11, 20), holidays, true, false));
    }

    [Fact]
    public void CountDays_HalfDayIsHalf()
    {
        Assert.Equal(0.5m, LeaveRules.CountDays(D(11, 16), D(11, 16), NoHolidays, true, halfDay: true));
    }

    [Fact]
    public void CountDays_HalfDayOnWeekendIsZero()
    {
        Assert.Equal(0m, LeaveRules.CountDays(D(11, 14), D(11, 14), NoHolidays, true, halfDay: true));
    }

    [Fact]
    public void CountDays_ReversedRangeIsZero()
    {
        Assert.Equal(0m, LeaveRules.CountDays(D(11, 20), D(11, 16), NoHolidays, true, false));
    }

    // ---- Overlaps / status helpers --------------------------------------

    [Theory]
    [InlineData(16, 18, 18, 20, true)]
    [InlineData(16, 18, 19, 20, false)]
    [InlineData(16, 20, 17, 18, true)]
    [InlineData(19, 20, 16, 18, false)]
    public void Overlaps_IsInclusive(int a1, int a2, int b1, int b2, bool expected)
    {
        Assert.Equal(expected, LeaveRules.Overlaps(D(11, a1), D(11, a2), D(11, b1), D(11, b2)));
    }

    [Theory]
    [InlineData(LeaveRules.Pending, 10, true)]
    [InlineData(LeaveRules.Approved, 20, true)]
    [InlineData(LeaveRules.Approved, 13, false)]
    [InlineData(LeaveRules.Approved, 10, false)]
    [InlineData(LeaveRules.Rejected, 20, false)]
    [InlineData(LeaveRules.Cancelled, 20, false)]
    public void CanCancel(string status, int fromDay, bool expected)
    {
        Assert.Equal(expected, LeaveRules.CanCancel(status, D(11, fromDay), Today));
    }

    [Fact]
    public void CanReview_OnlyPending()
    {
        Assert.True(LeaveRules.CanReview(LeaveRules.Pending));
        Assert.False(LeaveRules.CanReview(LeaveRules.Approved));
        Assert.False(LeaveRules.CanReview(LeaveRules.Rejected));
        Assert.False(LeaveRules.CanReview(LeaveRules.Cancelled));
    }

    // ---- Balances --------------------------------------------------------

    [Fact]
    public void Balances_SubtractApprovedAndPending()
    {
        var b = Balances(
            Leave(D(3, 2), D(3, 3), LeaveRules.Approved, days: 2),
            Leave(D(12, 1), D(12, 1), LeaveRules.Pending, days: 1),
            Leave(D(4, 1), D(4, 1), LeaveRules.Rejected, days: 1),
            Leave(D(5, 1), D(5, 1), LeaveRules.Cancelled, days: 1));

        var casual = b.Single(x => x.LeaveType == LeaveRules.Casual);
        Assert.Equal(2m, casual.Approved);
        Assert.Equal(1m, casual.Pending);
        Assert.Equal(9m, casual.Remaining);
    }

    [Fact]
    public void Balances_IgnoreOtherYears()
    {
        var b = Balances(Leave(new DateTime(2025, 12, 1), new DateTime(2025, 12, 1), LeaveRules.Approved, days: 5));
        Assert.Equal(12m, b.Single(x => x.LeaveType == LeaveRules.Casual).Remaining);
    }

    [Fact]
    public void Balances_UnpaidIsUnlimited()
    {
        var unpaid = Balances().Single(x => x.LeaveType == LeaveRules.Unpaid);
        Assert.Null(unpaid.Allowance);
        Assert.Null(unpaid.Remaining);
    }

    // ---- Validate --------------------------------------------------------

    private static string? Validate(LeaveRequestInput input, decimal days = 1, params LeaveModel[] existing) =>
        LeaveRules.Validate(input, days, Today, existing, Balances(existing), Options);

    [Fact]
    public void Validate_AcceptsNormalRequest()
    {
        Assert.Null(Validate(Req(D(11, 16), D(11, 17)), days: 2));
    }

    [Fact]
    public void Validate_RequiresDates()
    {
        Assert.NotNull(Validate(new LeaveRequestInput { LeaveType = LeaveRules.Casual }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Vacation")]
    public void Validate_RejectsUnknownType(string? type)
    {
        Assert.Equal("Please choose a valid leave type.", Validate(Req(D(11, 16), D(11, 16), type!)));
    }

    [Fact]
    public void Validate_RejectsReversedDates()
    {
        Assert.Contains("on or after", Validate(Req(D(11, 18), D(11, 16))));
    }

    [Fact]
    public void Validate_RejectsTooLongSpan()
    {
        Assert.Contains("cannot be longer", Validate(Req(D(11, 16), D(12, 31)), days: 30));
    }

    [Fact]
    public void Validate_HalfDayMustBeSingleDate()
    {
        Assert.Contains("single date", Validate(Req(D(11, 16), D(11, 17), half: true)));
    }

    [Fact]
    public void Validate_RejectsPastStartForCasual()
    {
        Assert.Equal("Leave cannot start in the past.", Validate(Req(D(11, 12), D(11, 12))));
    }

    [Fact]
    public void Validate_AllowsBackdatedSickWithinWindow()
    {
        Assert.Null(Validate(Req(D(11, 6), D(11, 6), LeaveRules.Sick)));
        Assert.Contains("at most 7 days", Validate(Req(D(11, 5), D(11, 5), LeaveRules.Sick)));
    }

    [Fact]
    public void Validate_RejectsMoreThanAYearAhead()
    {
        Assert.Contains("more than a year", Validate(Req(new DateTime(2027, 11, 16), new DateTime(2027, 11, 16))));
    }

    [Fact]
    public void Validate_RejectsLongReason()
    {
        Assert.Contains("Reason cannot exceed", Validate(Req(D(11, 16), D(11, 16), reason: new string('x', 501))));
    }

    [Fact]
    public void Validate_RejectsZeroDays()
    {
        Assert.Contains("weekends or holidays", Validate(Req(D(11, 14), D(11, 15)), days: 0));
    }

    [Fact]
    public void Validate_RejectsOverlapWithActiveLeave()
    {
        var error = Validate(Req(D(11, 17), D(11, 18)), 2, Leave(D(11, 18), D(11, 19), LeaveRules.Pending));
        Assert.Contains("already have pending leave", error);
    }

    [Fact]
    public void Validate_IgnoresOverlapWithCancelledOrRejected()
    {
        Assert.Null(Validate(Req(D(11, 17), D(11, 18)), 2,
            Leave(D(11, 18), D(11, 19), LeaveRules.Cancelled),
            Leave(D(11, 17), D(11, 17), LeaveRules.Rejected)));
    }

    [Fact]
    public void Validate_RejectsWhenBalanceTooLow()
    {
        var used = Leave(D(2, 2), D(2, 13), LeaveRules.Approved, LeaveRules.Casual, days: 11);
        var error = Validate(Req(D(11, 16), D(11, 17)), 2, used);
        Assert.Contains("Not enough Casual leave: 1 day(s) left, 2 requested.", error);
    }

    [Fact]
    public void Validate_UnpaidHasNoBalanceLimit()
    {
        Assert.Null(Validate(Req(D(11, 16), D(12, 11), LeaveRules.Unpaid), days: 20));
    }
}
