using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace project.Middleware
{
    /// <summary>
    /// Session-based access control.
    ///  - Public pages (login, register, static files) are always allowed.
    ///  - Every other page requires a logged-in session.
    ///  - /admin requires the Admin role.
    ///  - Employee pages require an Active account, and an employee may only
    ///    open their own /EmployeeView/index/{id} page.
    /// </summary>
    public class AuthMiddleware
    {
        private static readonly string[] PublicPrefixes =
        {
            "/home/index",
            "/home/logins",
            "/home/register",
            "/home/privacy",
            "/home/error",
            "/health",
        };

        private static readonly string[] StaticExtensions =
        {
            ".css", ".js", ".map", ".png", ".jpg", ".jpeg", ".gif", ".svg",
            ".ico", ".woff", ".woff2", ".ttf", ".eot",
        };

        private static readonly string[] EmployeePrefixes =
        {
            "/employeeview",
            "/profile",
            "/profileupdate",
            "/leave",
            "/myreport",
            "/notifications",
        };

        private readonly RequestDelegate _next;

        public AuthMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = (context.Request.Path.Value ?? "/").ToLowerInvariant();

            if (IsPublic(path))
            {
                await _next(context);
                return;
            }

            var email = context.Session.GetString("Mail");
            var role = context.Session.GetString("Role");
            var status = context.Session.GetString("Statuses");
            var sessionEmpId = context.Session.GetString("Ids");

            if (string.IsNullOrEmpty(email))
            {
                Deny(context);
                return;
            }

            if (path.StartsWith("/admin") && role != "Admin")
            {
                Deny(context);
                return;
            }

            if (StartsWithAny(path, EmployeePrefixes) && role != "Admin"
                && string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase))
            {
                Deny(context);
                return;
            }

            if (path.StartsWith("/employeeview/index/"))
            {
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 3 && int.TryParse(segments[2], out var empIdFromUrl)
                    && empIdFromUrl.ToString() != sessionEmpId)
                {
                    Deny(context);
                    return;
                }
            }

            await _next(context);
        }

        internal static bool IsPublic(string path)
        {
            if (path == "/" || path == "/home") return true;
            if (StartsWithAny(path, PublicPrefixes)) return true;
            foreach (var ext in StaticExtensions)
            {
                if (path.EndsWith(ext)) return true;
            }
            return false;
        }

        private static bool StartsWithAny(string path, string[] prefixes)
        {
            foreach (var p in prefixes)
            {
                if (path.StartsWith(p)) return true;
            }
            return false;
        }

        private static void Deny(HttpContext context)
        {
            var isAjax = context.Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            if (isAjax)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            context.Response.Redirect("/Home/Index");
        }
    }
}
