using project.Services;

namespace AttendanceSystem.Tests;

public class AttendanceRulesTests
{
    private static readonly TimeSpan Start = new(9, 15, 0);
    private static readonly TimeSpan End = new(17, 0, 0);

    private static TimeSpan T(int h, int m) => new(h, m, 0);

    [Theory]
    [InlineData(8, 30, AttendanceRules.Regular)]
    [InlineData(9, 15, AttendanceRules.Regular)]
    [InlineData(9, 16, AttendanceRules.LateIn)]
    [InlineData(13, 0, AttendanceRules.LateIn)]
    public void CheckInStatus_UsesShiftStart(int h, int m, string expected)
    {
        Assert.Equal(expected, AttendanceRules.CheckInStatus(T(h, m), Start));
    }

    [Theory]
    [InlineData(9, 0, 17, 30, AttendanceRules.Regular)]
    [InlineData(9, 0, 17, 0, AttendanceRules.Regular)]
    [InlineData(9, 0, 16, 59, AttendanceRules.EarlyOut)]
    [InlineData(10, 0, 18, 0, AttendanceRules.LateIn)]
    [InlineData(10, 0, 15, 0, AttendanceRules.LateEarly)]
    public void CheckOutStatus_CombinesLateAndEarly(int inH, int inM, int outH, int outM, string expected)
    {
        Assert.Equal(expected, AttendanceRules.CheckOutStatus(T(inH, inM), T(outH, outM), Start, End));
    }

    [Fact]
    public void CheckOutStatus_WithoutClockIn_IsNeverLate()
    {
        Assert.Equal(AttendanceRules.EarlyOut, AttendanceRules.CheckOutStatus(null, T(12, 0), Start, End));
        Assert.Equal(AttendanceRules.Regular, AttendanceRules.CheckOutStatus(null, T(18, 0), Start, End));
    }

    [Theory]
    [InlineData(9, 0, 17, 0, 8)]
    [InlineData(9, 0, 17, 59, 8)]
    [InlineData(9, 30, 10, 0, 1)]   // under an hour is still 1 (DB requires > 0)
    [InlineData(18, 0, 9, 0, 1)]    // clock-out before clock-in clamps to 1
    public void WorkedHours_FloorsAndClampsToOne(int inH, int inM, int outH, int outM, int expected)
    {
        Assert.Equal(expected, AttendanceRules.WorkedHours(T(inH, inM), T(outH, outM)));
    }

    [Fact]
    public void WorkedHours_WithoutClockIn_IsOne()
    {
        Assert.Equal(1, AttendanceRules.WorkedHours(null, T(17, 0)));
    }

    [Fact]
    public void DelayUntilNext_SameDay_WhenTimeNotYetPassed()
    {
        var now = new DateTime(2026, 11, 13, 10, 0, 0);
        Assert.Equal(TimeSpan.FromHours(8), AttendanceRules.DelayUntilNext(now, T(18, 0)));
    }

    [Fact]
    public void DelayUntilNext_NextDay_WhenTimeAlreadyPassed()
    {
        var now = new DateTime(2026, 11, 13, 19, 0, 0);
        Assert.Equal(TimeSpan.FromHours(23), AttendanceRules.DelayUntilNext(now, T(18, 0)));
    }

    [Fact]
    public void DelayUntilNext_NextDay_WhenExactlyAtRunTime()
    {
        var now = new DateTime(2026, 11, 13, 18, 0, 0);
        Assert.Equal(TimeSpan.FromDays(1), AttendanceRules.DelayUntilNext(now, T(18, 0)));
    }

    [Theory]
    [InlineData(2026, 11, 14, true)]   // Saturday
    [InlineData(2026, 11, 15, true)]   // Sunday
    [InlineData(2026, 11, 13, false)]  // Friday
    public void IsWeekend(int y, int m, int d, bool expected)
    {
        Assert.Equal(expected, AttendanceRules.IsWeekend(new DateTime(y, m, d)));
    }

    [Theory]
    [InlineData("08:45", 8, 45)]
    [InlineData("bad", 9, 15)]
    [InlineData("25:00", 9, 15)]
    public void ShiftOptions_ParsesStart_WithFallback(string value, int h, int m)
    {
        var o = new ShiftOptions { Start = value };
        Assert.Equal(T(h, m), o.StartTime);
    }
}
