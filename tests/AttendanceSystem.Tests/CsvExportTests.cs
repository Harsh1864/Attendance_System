using project.Models;
using project.Services;

namespace AttendanceSystem.Tests;

public class CsvExportTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData(" padded ", "\" padded \"")]
    public void Escape_QuotesWhenNeeded(string? input, string expected)
    {
        Assert.Equal(expected, CsvExport.Escape(input));
    }

    [Theory]
    [InlineData("=SUM(A1:A2)", "'=SUM(A1:A2)")]
    [InlineData("+1", "'+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@cmd", "'@cmd")]
    public void Escape_NeutralisesFormulas(string input, string expected)
    {
        Assert.Equal(expected, CsvExport.Escape(input));
    }

    [Fact]
    public void BuildAttendanceCsv_WritesHeaderAndRows()
    {
        var rows = new[]
        {
            new AttendanceModel
            {
                AttendDate = new DateTime(2026, 11, 13), EmpId = 7, EmpName = "Asha, K",
                ClockInHour = 9, ClockInMin = 5, ClockOutHour = 17, ClockOutMin = 30,
                WorkType = "Office", TaskType = "Coding,Review", WorkingHour = 8, AttendStatus = "Regular",
            },
        };

        var lines = CsvExport.BuildAttendanceCsv(rows).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.Equal("Date,Employee ID,Employee Name,Clock In,Clock Out,Work Type,Tasks,Working Hours,Status", lines[0]);
        Assert.Equal("2026-11-13,7,\"Asha, K\",09:05,17:30,Office,\"Coding,Review\",8,Regular", lines[1]);
    }

    [Fact]
    public void BuildAttendanceCsv_LeavesTimesBlankForAbsent()
    {
        var rows = new[]
        {
            new AttendanceModel
            {
                AttendDate = new DateTime(2026, 11, 12), EmpId = 3, EmpName = "Ravi",
                WorkType = "", TaskType = null, AttendStatus = "Absent",
            },
        };

        var line = CsvExport.BuildAttendanceCsv(rows).Split("\r\n")[1];
        Assert.Equal("2026-11-12,3,Ravi,,,,,0,Absent", line);
    }

    [Fact]
    public void BuildAttendanceCsv_LeavesClockOutBlankWhenNotCheckedOut()
    {
        var rows = new[]
        {
            new AttendanceModel
            {
                AttendDate = new DateTime(2026, 11, 13), EmpId = 4, EmpName = "Neha",
                ClockInHour = 9, ClockInMin = 0, WorkType = "Remote", AttendStatus = "Regular",
            },
        };

        var line = CsvExport.BuildAttendanceCsv(rows).Split("\r\n")[1];
        Assert.Equal("2026-11-13,4,Neha,09:00,,Remote,,0,Regular", line);
    }
}
