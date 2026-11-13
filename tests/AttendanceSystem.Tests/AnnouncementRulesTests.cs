using project.Services;

namespace AttendanceSystem.Tests;

public class AnnouncementRulesTests
{
    private static readonly DateTime Today = new(2026, 11, 13);

    private static AnnouncementInput Input(string? title = "Office closed", string? body = "Closed for maintenance.",
        string? priority = AnnouncementRules.Normal, DateTime? start = null, DateTime? end = null, int? id = null) => new()
    {
        AnnouncementId = id, Title = title, Body = body, Priority = priority, StartDate = start ?? Today, EndDate = end,
    };

    [Fact]
    public void Validate_AcceptsNormalAnnouncement()
    {
        Assert.Null(AnnouncementRules.Validate(Input(), Today));
        Assert.Null(AnnouncementRules.Validate(Input(end: Today.AddDays(3), priority: AnnouncementRules.Important), Today));
    }

    [Theory]
    [InlineData(null, "b", "Title is required.")]
    [InlineData("t", "  ", "Message is required.")]
    public void Validate_RequiresTitleAndBody(string? title, string? body, string expected)
    {
        Assert.Equal(expected, AnnouncementRules.Validate(Input(title, body), Today));
    }

    [Fact]
    public void Validate_LimitsLengths()
    {
        Assert.Contains("Title cannot exceed", AnnouncementRules.Validate(Input(title: new string('t', 121)), Today));
        Assert.Contains("Message cannot exceed", AnnouncementRules.Validate(Input(body: new string('b', 2001)), Today));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Urgent")]
    public void Validate_RejectsUnknownPriority(string? priority)
    {
        Assert.Equal("Priority must be Normal or Important.", AnnouncementRules.Validate(Input(priority: priority), Today));
    }

    [Fact]
    public void Validate_EndMustNotBeBeforeStart()
    {
        Assert.Contains("on or after the start", AnnouncementRules.Validate(Input(start: Today.AddDays(2), end: Today.AddDays(1)), Today));
    }

    [Fact]
    public void Validate_NewAnnouncementCannotEndInPast_ButEditsCan()
    {
        var past = Input(start: Today.AddDays(-5), end: Today.AddDays(-1));
        Assert.Contains("cannot end in the past", AnnouncementRules.Validate(past, Today));
        past.AnnouncementId = 4;
        Assert.Null(AnnouncementRules.Validate(past, Today));
    }

    [Theory]
    [InlineData(1, null, AnnouncementRules.Upcoming)]
    [InlineData(0, null, AnnouncementRules.Active)]
    [InlineData(-3, 0, AnnouncementRules.Active)]
    [InlineData(-3, -1, AnnouncementRules.Expired)]
    public void StateOn(int startOffset, int? endOffset, string expected)
    {
        DateTime? end = endOffset.HasValue ? Today.AddDays(endOffset.Value) : null;
        Assert.Equal(expected, AnnouncementRules.StateOn(Today.AddDays(startOffset), end, Today));
    }

    [Fact]
    public void OrderForDisplay_ImportantFirstThenNewest()
    {
        var items = new[]
        {
            new AnnouncementModel { AnnouncementId = 1, Priority = AnnouncementRules.Normal, StartDate = Today },
            new AnnouncementModel { AnnouncementId = 2, Priority = AnnouncementRules.Important, StartDate = Today.AddDays(-5) },
            new AnnouncementModel { AnnouncementId = 3, Priority = AnnouncementRules.Normal, StartDate = Today.AddDays(-1) },
            new AnnouncementModel { AnnouncementId = 4, Priority = AnnouncementRules.Important, StartDate = Today },
        };
        Assert.Equal(new[] { 4, 2, 1, 3 }, AnnouncementRules.OrderForDisplay(items).Select(a => a.AnnouncementId));
    }
}
