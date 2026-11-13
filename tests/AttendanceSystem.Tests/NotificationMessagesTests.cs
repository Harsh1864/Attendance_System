using project.Models;
using project.Services;

namespace AttendanceSystem.Tests;

public class NotificationMessagesTests
{
    private static LeaveModel Leave(DateTime from, DateTime to) =>
        new() { FromDate = from, ToDate = to, LeaveType = "Casual" };

    [Fact]
    public void LeaveDecision_Approved_SingleDay()
    {
        var n = NotificationMessages.LeaveDecision(Leave(new(2026, 11, 16), new(2026, 11, 16)), true, null);
        Assert.Equal("Leave approved", n.Title);
        Assert.Equal("Your Casual leave for 16 Nov 2026 was approved.", n.Message);
        Assert.Equal("/Leave/Index", n.Link);
    }

    [Fact]
    public void LeaveDecision_Rejected_RangeWithNote()
    {
        var n = NotificationMessages.LeaveDecision(Leave(new(2026, 11, 16), new(2026, 11, 18)), false, "  Busy week ");
        Assert.Equal("Leave rejected", n.Title);
        Assert.Equal("Your Casual leave for 16 Nov to 18 Nov 2026 was rejected. Note: Busy week", n.Message);
    }

    [Fact]
    public void LeaveRequested_LinksToAdminPage()
    {
        var n = NotificationMessages.LeaveRequested("Asha", "Sick 2026-11-16 to 2026-11-16 (1 day(s))");
        Assert.Equal("Asha requested Sick 2026-11-16 to 2026-11-16 (1 day(s)).", n.Message);
        Assert.Equal("/Admin/Leave", n.Link);
    }

    [Fact]
    public void AttendanceChanged_IncludesReason()
    {
        var n = NotificationMessages.AttendanceChanged("2026-11-12 09:00-17:00 Office Regular 8h", " forgot ");
        Assert.Equal("An admin updated your attendance: 2026-11-12 09:00-17:00 Office Regular 8h. Reason: forgot", n.Message);
    }

    [Fact]
    public void CorrectionMessages()
    {
        var date = new DateTime(2026, 11, 12);
        Assert.Equal("Asha asked to correct attendance for 12 Nov 2026.",
            NotificationMessages.CorrectionRequested("Asha", date).Message);
        Assert.Equal("Your attendance correction for 12 Nov 2026 was approved.",
            NotificationMessages.CorrectionDecision(date, true, "").Message);
        Assert.Equal("Correction rejected", NotificationMessages.CorrectionDecision(date, false, "No proof").Title);
    }
}
