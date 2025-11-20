using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace project.Middleware
{
    public class AuthMiddleware
    {
        private readonly RequestDelegate _next;

        public AuthMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.ToString().ToLower();

            // Public paths to skip
            if (path.StartsWith("/home/index") || path.StartsWith("/home/logins")
                || path.Contains(".css") || path.Contains(".js") || path.Contains(".png")
                || path.Contains(".jpg"))
            {
                await _next(context);
                return;
            }

            var email = context.Session.GetString("Mail");
            var role = context.Session.GetString("Role");
            var statuses = context.Session.GetString("Statuses");
        
            // Role-based access
            if (path.StartsWith("/admin") && role != "Admin")
            {
                context.Response.Redirect("/Home/Index");
                return;
            }

            if (path.StartsWith("/EmployeeView") && role != "Employee" && statuses == "Inactive")
            {
                context.Response.Redirect("/Home/Index");
                return;
            }

            if (path.StartsWith("/employeeview/index"))
            {
                var segments = context.Request.Path.Value.Split('/');
                if (segments.Length >= 4 && int.TryParse(segments[3], out int empIdFromUrl))
                {
                    var sessionEmpId = context.Session.GetString("Ids");

                    if (sessionEmpId == null || empIdFromUrl.ToString() != sessionEmpId)
                    {
                        context.Response.Redirect("/Home/Index");
                        return;
                    }
                }
            }
            await _next(context);
        }
    }
}
