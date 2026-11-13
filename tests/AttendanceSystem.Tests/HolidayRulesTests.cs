using project.Services;

namespace AttendanceSystem.Tests;

public class HolidayRulesTests
{
    private static readonly DateTime Today = new(2026, 11, 13);

    [Fact]
    public void Validate_AcceptsReasonableHoliday()
    {
        Assert.Null(HolidayRules.Validate(new DateTime(2026, 12, 25), "Christmas", Today));
    }

    [Fact]
    public void Validate_RequiresDate()
    {
        Assert.Equal("Date is required.", HolidayRules.Validate(null, "X", Today));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RequiresName(string? name)
    {
        Assert.Equal("Name is required.", HolidayRules.Validate(Today, name, Today));
    }

    [Fact]
    public void Validate_RejectsLongName()
    {
        Assert.NotNull(HolidayRules.Validate(Today, new string('a', 101), Today));
        Assert.Null(HolidayRules.Validate(Today, new string('a', 100), Today));
    }

    [Theory]
    [InlineData(2025, 11, 12, false)]  // just over a year ago
    [InlineData(2025, 11, 13, true)]   // exactly a year ago
    [InlineData(2028, 11, 13, true)]   // exactly two years ahead
    [InlineData(2028, 11, 14, false)]  // just past two years
    public void Validate_LimitsDateRange(int y, int m, int d, bool ok)
    {
        var error = HolidayRules.Validate(new DateTime(y, m, d), "X", Today);
        Assert.Equal(ok, error == null);
    }
}
