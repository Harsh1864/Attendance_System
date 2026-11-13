using project.Models;
using project.Services;
using B = project.Services.MonthlyReportBuilder;

namespace AttendanceSystem.Tests;

public class MonthlyReportBuilderTests
{
    // November 2026: 1st is a Sunday, 30 days, 21 weekdays.
    private const int Year = 2026, Month = 11;
    private static readonly DateTime EndOfMonth = new(2026, 11, 30);
    private static readonly Dictionary<DateTime, string> NoHolidays = new();

    private static DateTime D(int day) => new(Year, Month, day);

    private static AttendanceModel Att(int day, string status = "Regular", int hours = 8, int empId = 1) => new()
    {
        EmpId = empId, EmpName = "", AttendDate = D(day), AttendStatus = status, WorkingHour = hours,
        ClockInHour = 9, ClockInMin = 0, ClockOutHour = 17, ClockOutMin = 0, WorkType = "Office",
    };

    private static LeaveModel Lv(int from, int to, bool half = false, int empId = 1, string status = LeaveRules.Approved) => new()
    {
        EmpId = empId, FromDate = D(from), ToDate = D(to), HalfDay = half, Status = status, LeaveType = LeaveRules.Casual,
    };

    private static List<DailyStatusModel> Days(DateTime today, IEnumerable<AttendanceModel> att, IEnumerable<LeaveModel>? leave = null,
        Dictionary<DateTime, string>? holidays = null) =>
        B.BuildDays(Year, Month, today, att, leave ?? Array.Empty<LeaveModel>(), holidays ?? NoHolidays, skipWeekends: true);

    [Fact]
    public void MonthRange_CoversWholeMonth()
    {
        var (s, e) = B.MonthRange(2026, 2);
        Assert.Equal(new DateTime(2026, 2, 1), s);
        Assert.Equal(new DateTime(2026, 2, 28), e);
    }

    [Fact]
    public void BuildDays_ReturnsOneEntryPerDay()
    {
        Assert.Equal(30, Days(EndOfMonth, Array.Empty<AttendanceModel>()).Count);
    }

    [Fact]
    public void BuildDays_ClassifiesEachKind()
    {
        var holidays = new Dictionary<DateTime, string> { [D(4)] = "Festival" };
        var att = new[] { Att(2), Att(3, "LateIn"), Att(5, "Absent"), Att(9, "EarlyOut"), Att(10, "LateEarly") };
        var leave = new[] { Lv(6, 6) };
        var today = D(12);

        var days = Days(today, att, leave, holidays).ToDictionary(d => d.Date.Day, d => d.Kind);

        Assert.Equal(B.Weekend, days[1]);
        Assert.Equal(B.Present, days[2]);
        Assert.Equal(B.Late, days[3]);
        Assert.Equal(B.Holiday, days[4]);
        Assert.Equal(B.Absent, days[5]);
        Assert.Equal(B.Leave, days[6]);
        Assert.Equal(B.Weekend, days[7]);
        Assert.Equal(B.EarlyOut, days[9]);
        Assert.Equal(B.LateEarly, days[10]);
        Assert.Equal(B.NoRecord, days[11]);
        Assert.Equal(B.NoRecord, days[12]);
        Assert.Equal(B.Future, days[13]);
    }

    [Fact]
    public void BuildDays_AttendanceWinsOverLeaveAndHoliday()
    {
        var holidays = new Dictionary<DateTime, string> { [D(2)] = "X" };
        var days = Days(EndOfMonth, new[] { Att(2), Att(3) }, new[] { Lv(3, 3) }, holidays);
        Assert.Equal(B.Present, days[1].Kind);
        Assert.Equal(B.Present, days[2].Kind);
    }

    [Fact]
    public void BuildDays_AbsentRowOnLeaveDayCountsAsLeave()
    {
        var days = Days(EndOfMonth, new[] { Att(3, "Absent") }, new[] { Lv(3, 3) });
        Assert.Equal(B.Leave, days[2].Kind);
    }

    [Fact]
    public void BuildDays_IgnoresNonApprovedLeave()
    {
        var days = Days(D(5), Array.Empty<AttendanceModel>(), new[] { Lv(3, 3, status: LeaveRules.Pending) });
        Assert.Equal(B.NoRecord, days[2].Kind);
    }

    [Fact]
    public void BuildDays_FillsTimesAndTasks()
    {
        var a = Att(2); a.TaskType = "Coding"; a.ClockInHour = 9; a.ClockInMin = 5; a.ClockOutHour = 0; a.ClockOutMin = 0;
        var day = Days(EndOfMonth, new[] { a })[1];
        Assert.Equal("09:05", day.ClockIn);
        Assert.Null(day.ClockOut);
        Assert.Equal("Coding", day.Detail);
    }

    [Fact]
    public void Summarise_CountsEverything()
    {
        var holidays = new Dictionary<DateTime, string> { [D(4)] = "Festival" };
        var att = new[] { Att(2, hours: 8), Att(3, "LateIn", 7), Att(5, "Absent", 0), Att(9, "LateEarly", 5) };
        var leave = new[] { Lv(6, 6), Lv(10, 10, half: true) };
        var today = D(11);

        var days = Days(today, att, leave, holidays);
        var row = B.Summarise(new B.EmployeeInfo(1, "Asha", "Active"), days, today, leave);

        // Weekdays 2..11 = 8, minus holiday on 4th = 7 working days.
        Assert.Equal(7, row.WorkingDays);
        Assert.Equal(3, row.PresentDays);
        Assert.Equal(2, row.LateDays);
        Assert.Equal(1, row.EarlyOutDays);
        Assert.Equal(1, row.AbsentDays);
        Assert.Equal(1.5m, row.LeaveDays);
        Assert.Equal(1, row.UnrecordedDays);   // 11th
        Assert.Equal(20, row.TotalHours);
        Assert.Equal(6.7m, row.AverageHours);
        // 3 present / (7 - 1.5) = 54.5%
        Assert.Equal(54.5m, row.AttendancePercent);
    }

    [Fact]
    public void Summarise_NoWorkingDaysGivesNullPercent()
    {
        var today = D(1); // Sunday, nothing has happened yet
        var days = Days(today, Array.Empty<AttendanceModel>());
        var row = B.Summarise(new B.EmployeeInfo(1, "A", "Active"), days, today, Array.Empty<LeaveModel>());
        Assert.Equal(0, row.WorkingDays);
        Assert.Null(row.AttendancePercent);
        Assert.Equal(0, row.AverageHours);
    }

    [Fact]
    public void Summarise_PercentIsCappedAt100()
    {
        // Worked on a weekend as well as the only weekday so far.
        var today = D(2);
        var days = Days(today, new[] { Att(1), Att(2) });
        var row = B.Summarise(new B.EmployeeInfo(1, "A", "Active"), days, today, Array.Empty<LeaveModel>());
        Assert.Equal(100m, row.AttendancePercent);
    }

    [Fact]
    public void Build_IncludesActiveAndSkipsIdleInactive()
    {
        var employees = new[]
        {
            new B.EmployeeInfo(1, "Zed", "Active"),
            new B.EmployeeInfo(2, "amy", "Active"),
            new B.EmployeeInfo(3, "Old", "Inactive"),
            new B.EmployeeInfo(4, "Left", "Inactive"),
        };
        var att = new[] { Att(2, empId: 1), Att(2, empId: 4) };

        var report = B.Build(Year, Month, EndOfMonth, employees, att, Array.Empty<LeaveModel>(), NoHolidays, true);

        Assert.Equal(new[] { "amy", "Left", "Zed" }, report.Rows.Select(r => r.EmpName));
        Assert.Equal(21, report.WorkingDays);
        Assert.Equal("November 2026", report.MonthName);
    }

    [Fact]
    public void Build_CountsHolidaysInMonth()
    {
        var holidays = new Dictionary<DateTime, string> { [D(4)] = "A", [D(25)] = "B", [new DateTime(2026, 12, 25)] = "C" };
        var report = B.Build(Year, Month, EndOfMonth, Array.Empty<B.EmployeeInfo>(), Array.Empty<AttendanceModel>(),
            Array.Empty<LeaveModel>(), holidays, true);
        Assert.Equal(2, report.Holidays);
        Assert.Equal(19, report.WorkingDays);
    }

    [Fact]
    public void ToCsv_WritesHeaderAndRows()
    {
        var report = new MonthlyReportModel
        {
            MonthName = "November 2026", WorkingDays = 21,
            Rows =
            {
                new MonthlyReportRow
                {
                    EmpId = 7, EmpName = "Asha", Department = "Sales", Status = "Active", WorkingDays = 21, PresentDays = 20,
                    LateDays = 2, LeaveDays = 1, TotalHours = 160, AverageHours = 8, AttendancePercent = 100,
                },
            },
        };

        var lines = B.ToCsv(report).Split("\r\n");
        Assert.Equal("Month,November 2026", lines[0]);
        Assert.Equal("Working days,21", lines[1]);
        Assert.Equal("", lines[2]);
        Assert.StartsWith("Employee ID,Employee,Department,Status", lines[3]);
        Assert.Equal("7,Asha,Sales,Active,21,20,2,0,0,1,0,160,8.0,100.0", lines[4]);
    }

    [Theory]
    [InlineData(2026, 11, null)]
    [InlineData(2026, 12, "Choose a month that has started.")]
    [InlineData(2026, 13, "Month must be between 1 and 12.")]
    [InlineData(1999, 5, "Choose a month that has started.")]
    public void ValidateMonth(int y, int m, string? expected)
    {
        Assert.Equal(expected, ReportService.ValidateMonth(y, m, new DateTime(2026, 11, 13)));
    }
}
