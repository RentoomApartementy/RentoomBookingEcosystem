using System.Globalization;
using Microsoft.AspNetCore.Localization;
using RentoomBooking.SharedFrontend.Localization;

namespace RentoomBookingWeb.Services.Localization
{
    public class LocalizedRoutingMiddleware
    {
        private readonly RequestDelegate _next;

        public LocalizedRoutingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value;
            
            // Bypass for technical routes and files
            if (string.IsNullOrEmpty(path) || path == "/" || IsTechnicalRoute(path))
            {
                await _next(context);
                return;
            }

            // AVOID FILES: Bypass any path that looks like a file.
            if (path.Contains('.') && !path.EndsWith("/"))
            {
                await _next(context);
                return;
            }

            // The culture detection is now primarily handled by the CustomRequestCultureProvider in Program.cs.
            // This middleware can still be used for additional logic if needed, but for now we'll just continue.
            
            await _next(context);
        }

        private bool IsTechnicalRoute(string path)
        {
            var lowPath = path.ToLowerInvariant();
            return lowPath.StartsWith("/_") || 
                   lowPath.StartsWith("/api/") || 
                   lowPath.StartsWith("/swagger") ||
                   lowPath.Contains("/_blazor");
        }
    }
}
