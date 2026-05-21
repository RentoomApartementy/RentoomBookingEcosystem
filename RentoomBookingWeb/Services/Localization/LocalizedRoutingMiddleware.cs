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
            
            // 1. Bypass technical routes and files
            if (string.IsNullOrEmpty(path) || path == "/" || IsTechnicalRoute(path))
            {
                await _next(context);
                return;
            }

            if (path.Contains('.') && !path.EndsWith("/"))
            {
                await _next(context);
                return;
            }

            // 2. Detect culture from URL prefix
            var parts = path.TrimStart('/').Split('/');
            var potentialCulture = parts[0];
            var supportedCultures = SupportedLanguagesProvider.SupportedCultureNames;

            var matchedCulture = supportedCultures.FirstOrDefault(c => 
                string.Equals(c, potentialCulture, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Split('-')[0], potentialCulture, StringComparison.OrdinalIgnoreCase));

            if (matchedCulture != null)
            {
                var cultureInfo = new CultureInfo(matchedCulture);
                
                // FORCE: Set the culture for the current request thread
                CultureInfo.CurrentCulture = cultureInfo;
                CultureInfo.CurrentUICulture = cultureInfo;
                
                // SYNC: Set the feature so subsequent localization middleware (and Blazor) respects this
                context.Features.Set<IRequestCultureFeature>(new RequestCultureFeature(new RequestCulture(cultureInfo), null));

                // PERSIST: Update the cookie so Blazor Server circuit initialization picks it up
                var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(cultureInfo));
                context.Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    cookieValue,
                    new CookieOptions 
                    { 
                        Expires = DateTimeOffset.UtcNow.AddYears(1), 
                        HttpOnly = true, 
                        SameSite = SameSiteMode.Lax,
                        Path = "/" 
                    }
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
