using project.Services;

namespace AttendanceSystem.Tests;

public class DepartmentRulesTests
{
    [Fact]
    public void Validate_AcceptsNameAndOptionalDescription()
    {
        Assert.Null(DepartmentRules.Validate("Engineering", null));
        Assert.Null(DepartmentRules.Validate("Sales", "Field and inside sales"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RequiresName(string? name)
    {
        Assert.Equal("Department name is required.", DepartmentRules.Validate(name, null));
    }

    [Fact]
    public void Validate_LimitsLengths()
    {
        Assert.NotNull(DepartmentRules.Validate(new string('n', 51), null));
        Assert.Null(DepartmentRules.Validate(new string('n', 50), null));
        Assert.NotNull(DepartmentRules.Validate("X", new string('d', 201)));
    }

    [Fact]
    public void CleanIds_RemovesInvalidAndDuplicates()
    {
        Assert.Equal(new[] { 3, 5 }, DepartmentRules.CleanIds(new[] { 3, 0, -1, 5, 3 }));
        Assert.Empty(DepartmentRules.CleanIds(null));
    }

    [Fact]
    public void EmployeeInfo_DepartmentFlowsIntoReportRow()
    {
        var emp = new MonthlyReportBuilder.EmployeeInfo(1, "Asha", "Active", "Sales");
        var today = new DateTime(2026, 11, 13);
        var days = MonthlyReportBuilder.BuildDays(2026, 11, today, Array.Empty<project.Models.AttendanceModel>(),
            Array.Empty<project.Models.LeaveModel>(), new Dictionary<DateTime, string>(), true);
        var row = MonthlyReportBuilder.Summarise(emp, days, today, Array.Empty<project.Models.LeaveModel>());
        Assert.Equal("Sales", row.Department);
    }
}
