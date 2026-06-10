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
                
                // --- ENTERPRISE COOKIE SYNC ---
                // We check if the current cookie matches the URL culture.
                // If not, we append the cookie to the response so subsequent SignalR requests (which don't have the URL prefix) 
                // will pick up the correct culture from the cookie.
                var currentCookie = context.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName];
                var expectedCookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(cultureInfo));

                if (currentCookie != expectedCookieValue)
                {
                    context.Response.Cookies.Append(
                        CookieRequestCultureProvider.DefaultCookieName,
                        expectedCookieValue,
                        new CookieOptions 
                        { 
                            Expires = DateTimeOffset.UtcNow.AddYears(1), 
                            HttpOnly = true, 
                            SameSite = SameSiteMode.Lax,
                            Path = "/" 
                        }
                    );
                }

                // FORCE: Set the culture for the current request thread (for SSR)
                CultureInfo.CurrentCulture = cultureInfo;
                CultureInfo.CurrentUICulture = cultureInfo;
                
                // SYNC: Set the feature so subsequent localization middleware (and Blazor) respects this
                context.Features.Set<IRequestCultureFeature>(new RequestCultureFeature(new RequestCulture(cultureInfo), null));
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
