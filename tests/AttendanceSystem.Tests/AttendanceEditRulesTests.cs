using project.Services;

namespace AttendanceSystem.Tests;

public class AttendanceEditRulesTests
{
    private static readonly DateTime Today = new(2026, 11, 13);
    private static readonly TimeSpan Start = new(9, 15, 0);
    private static readonly TimeSpan End = new(17, 0, 0);

    private static AttendanceEditInput Input(
        string? clockIn = "09:00", string? clockOut = "17:30", string? workType = "Office",
        DateTime? date = null, bool absent = false, string? reason = "Forgot to check in", string? tasks = null) => new()
    {
        EmpId = 5, Date = date ?? Today.AddDays(-1), ClockIn = clockIn, ClockOut = clockOut,
        WorkType = workType, Absent = absent, Reason = reason, TaskType = tasks,
    };

    private static (AttendanceEditResult? r, string? e) V(AttendanceEditInput i) =>
        AttendanceEditRules.Validate(i, Today, Start, End);

    [Theory]
    [InlineData("09:00", true, 9, 0)]
    [InlineData("23:59", true, 23, 59)]
    [InlineData("0:5", true, 0, 5)]
    [InlineData("24:00", false, 0, 0)]
    [InlineData("12:60", false, 0, 0)]
    [InlineData("9", false, 0, 0)]
    [InlineData("ab:cd", false, 0, 0)]
    [InlineData("", false, 0, 0)]
    [InlineData(null, false, 0, 0)]
    public void TryParseTime(string? value, bool ok, int h, int m)
    {
        Assert.Equal(ok, AttendanceEditRules.TryParseTime(value, out var t));
        if (ok) Assert.Equal(new TimeSpan(h, m, 0), t);
    }

    [Fact]
    public void Validate_ComputesRegularDay()
    {
        var (r, e) = V(Input());
        Assert.Null(e);
        Assert.Equal(AttendanceRules.Regular, r!.Status);
        Assert.Equal(8, r.WorkingHours);
        Assert.Equal(new TimeSpan(9, 0, 0), r.ClockIn);
        Assert.Equal(new TimeSpan(17, 30, 0), r.ClockOut);
    }

    [Theory]
    [InlineData("10:00", "18:00", "LateIn")]
    [InlineData("09:00", "16:00", "EarlyOut")]
    [InlineData("10:00", "16:00", "LateEarly")]
    public void Validate_UsesShiftRules(string cin, string cout, string expected)
    {
        Assert.Equal(expected, V(Input(cin, cout)).r!.Status);
    }

    [Fact]
    public void Validate_AbsentIgnoresTimes()
    {
        var (r, e) = V(Input(clockIn: null, clockOut: null, workType: null, absent: true));
        Assert.Null(e);
        Assert.Equal(AttendanceRules.Absent, r!.Status);
        Assert.Null(r.ClockIn);
        Assert.Null(r.WorkingHours);
    }

    [Fact]
    public void Validate_TodayWithoutClockOutIsAllowed()
    {
        var (r, e) = V(Input(clockIn: "10:00", clockOut: "", date: Today));
        Assert.Null(e);
        Assert.Equal(AttendanceRules.LateIn, r!.Status);
        Assert.Null(r.ClockOut);
        Assert.Null(r.WorkingHours);
    }

    [Fact]
    public void Validate_PastDayRequiresClockOut()
    {
        Assert.Equal("Clock-out time is required for past days.", V(Input(clockOut: null)).e);
    }

    [Theory]
    [InlineData("17:00", "09:00")]
    [InlineData("09:00", "09:00")]
    public void Validate_ClockOutMustBeAfterClockIn(string cin, string cout)
    {
        Assert.Equal("Clock-out must be after clock-in.", V(Input(cin, cout)).e);
    }

    [Fact]
    public void Validate_RejectsFutureDate()
    {
        Assert.Contains("future", V(Input(date: Today.AddDays(1))).e);
    }

    [Fact]
    public void Validate_RejectsVeryOldDate()
    {
        Assert.Contains("older than one year", V(Input(date: Today.AddYears(-1).AddDays(-1))).e);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Validate_RequiresReason(string? reason)
    {
        Assert.Equal("Please give a reason for the change.", V(Input(reason: reason)).e);
    }

    [Fact]
    public void Validate_RejectsLongReason()
    {
        Assert.Contains("Reason cannot exceed", V(Input(reason: new string('r', 301))).e);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Home")]
    public void Validate_RejectsUnknownWorkType(string? workType)
    {
        Assert.Equal("Work type must be Office, Remote or Field.", V(Input(workType: workType)).e);
    }

    [Fact]
    public void Validate_RejectsLongTasks()
    {
        Assert.Contains("Tasks cannot exceed", V(Input(tasks: new string('t', 101))).e);
    }

    [Fact]
    public void Validate_RequiresEmployee()
    {
        var i = Input(); i.EmpId = 0;
        Assert.Equal("Please choose an employee.", V(i).e);
    }

    [Fact]
    public void Validate_RequiresClockIn()
    {
        Assert.Equal("Clock-in time is required (HH:mm).", V(Input(clockIn: "")).e);
    }

    [Fact]
    public void Describe_FormatsBothKinds()
    {
        Assert.Equal("2026-11-12 09:00-17:30 Office Regular 8h", AttendanceEditRules.Describe(V(Input()).r!));
        Assert.Equal("2026-11-12 Absent", AttendanceEditRules.Describe(V(Input(absent: true)).r!));
        Assert.Equal("2026-11-13 10:00---:-- Office LateIn",
            AttendanceEditRules.Describe(V(Input(clockIn: "10:00", clockOut: null, date: Today)).r!));
    }
}
