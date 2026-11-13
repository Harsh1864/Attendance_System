using project.Middleware;

namespace AttendanceSystem.Tests;

public class AuthMiddlewareTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/home/index")]
    [InlineData("/home/logins")]
    [InlineData("/home/register")]
    [InlineData("/lib/kendo/kendo.all.min.js")]
    [InlineData("/css/site.css")]
    [InlineData("/images/logo.png")]
    public void IsPublic_AllowsLoginPagesAndStaticFiles(string path)
    {
        Assert.True(AuthMiddleware.IsPublic(path));
    }

    [Theory]
    [InlineData("/admin/admindashboard")]
    [InlineData("/employeeview/index/5")]
    [InlineData("/profile")]
    [InlineData("/profileupdate/updatepassword")]
    [InlineData("/home/logout")]
    public void IsPublic_RejectsProtectedPages(string path)
    {
        Assert.False(AuthMiddleware.IsPublic(path));
    }
}
