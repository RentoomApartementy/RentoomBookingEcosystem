using System.Globalization;
using Microsoft.AspNetCore.Localization;
using RentoomBooking.SharedFrontend.Localization;

namespace RentoomBookingWeb.Services.Localization
{
    public class LocalizedRoutingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<LocalizedRoutingMiddleware> _logger;

        public LocalizedRoutingMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<LocalizedRoutingMiddleware>();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value;
            
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

            var parts = path.TrimStart('/').Split('/');
            var potentialCulture = parts[0];
            var supportedCultures = SupportedLanguagesProvider.SupportedCultureNames;

            // 1. Detect culture from URL prefix (full or short)
            var matchedCulture = supportedCultures.FirstOrDefault(c => 
                string.Equals(c, potentialCulture, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Split('-')[0], potentialCulture, StringComparison.OrdinalIgnoreCase));

            if (matchedCulture != null)
            {
                var cultureInfo = new CultureInfo(matchedCulture);
                
                // Set the culture for the current request
                CultureInfo.CurrentCulture = cultureInfo;
                CultureInfo.CurrentUICulture = cultureInfo;
                
                // Set the feature so subsequent localization middleware respects this
                context.Features.Set<IRequestCultureFeature>(new RequestCultureFeature(new RequestCulture(cultureInfo), null));

                // SYNC COOKIE: Update the standard .AspNetCore.Culture cookie so preference persists
                var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(cultureInfo));
                context.Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    cookieValue,
                    new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), HttpOnly = true, SameSite = SameSiteMode.Lax }
                );
            }

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
