using project.Services;

namespace AttendanceSystem.Tests;

public class AuditServiceTests
{
    [Theory]
    [InlineData(null, 5, null)]
    [InlineData("abc", 5, "abc")]
    [InlineData("abcdef", 5, "abcde")]
    [InlineData("", 5, "")]
    public void Truncate_LimitsLength(string? input, int max, string? expected)
    {
        Assert.Equal(expected, AuditService.Truncate(input, max));
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("50%", "50\\%")]
    [InlineData("a_b", "a\\_b")]
    [InlineData("c:\\path", "c:\\\\path")]
    public void EscapeLike_EscapesWildcards(string input, string expected)
    {
        Assert.Equal(expected, AuditService.EscapeLike(input));
    }
}
