using project.Services;

namespace AttendanceSystem.Tests;

public class CorrectionRulesTests
{
    private static readonly DateTime Today = new(2026, 11, 13);
    private static readonly TimeSpan Start = new(9, 15, 0);
    private static readonly TimeSpan End = new(17, 0, 0);

    private static AttendanceEditInput Input(DateTime date, bool absent = false) => new()
    {
        EmpId = 3, Date = date, ClockIn = "09:00", ClockOut = "17:00", WorkType = "Office",
        Reason = "Forgot to check out", Absent = absent,
    };

    [Fact]
    public void Validate_AcceptsWithinWindow()
    {
        var (r, e) = CorrectionRules.Validate(Input(Today.AddDays(-CorrectionRules.MaxDaysBack)), Today, Start, End);
        Assert.Null(e);
        Assert.Equal(AttendanceRules.Regular, r!.Status);
    }

    [Fact]
    public void Validate_RejectsOlderThanWindow()
    {
        var (_, e) = CorrectionRules.Validate(Input(Today.AddDays(-CorrectionRules.MaxDaysBack - 1)), Today, Start, End);
        Assert.Equal("Corrections can only be requested for the last 30 days.", e);
    }

    [Fact]
    public void Validate_EmployeesCannotRequestAbsent()
    {
        var input = Input(Today.AddDays(-1), absent: true);
        var (r, e) = CorrectionRules.Validate(input, Today, Start, End);
        Assert.Null(e);
        Assert.NotEqual(AttendanceRules.Absent, r!.Status);
        Assert.False(input.Absent);
    }

    [Fact]
    public void Validate_UsesEditRules()
    {
        var input = Input(Today.AddDays(-1));
        input.ClockOut = "08:00";
        Assert.Equal("Clock-out must be after clock-in.", CorrectionRules.Validate(input, Today, Start, End).error);
    }

    [Theory]
    [InlineData(CorrectionRules.Pending, true)]
    [InlineData(CorrectionRules.Approved, false)]
    [InlineData(CorrectionRules.Rejected, false)]
    [InlineData(CorrectionRules.Cancelled, false)]
    public void OnlyPendingCanBeCancelledOrReviewed(string status, bool expected)
    {
        Assert.Equal(expected, CorrectionRules.CanCancel(status));
        Assert.Equal(expected, CorrectionRules.CanReview(status));
    }
}
