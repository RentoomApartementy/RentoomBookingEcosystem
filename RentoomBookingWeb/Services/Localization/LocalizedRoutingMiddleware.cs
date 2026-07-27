using System.Globalization;
using System.Linq;
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

            // --- ROOT REDIRECTION ---
            if ((string.Equals(context.Request.Method, "GET", StringComparison.OrdinalIgnoreCase) || 
                 string.Equals(context.Request.Method, "HEAD", StringComparison.OrdinalIgnoreCase)) && 
                (string.IsNullOrEmpty(path) || path == "/"))
            {
                var cultureInfo = DetermineUserCulture(context);
                var shortCulture = cultureInfo.Split('-')[0].ToLowerInvariant();
                var queryString = context.Request.QueryString.Value ?? string.Empty;
                
                context.Response.Redirect($"/{shortCulture}{queryString}", permanent: false);
                return;
            }
            
            // 1. Bypass technical routes and files
            if (string.IsNullOrEmpty(path) || IsTechnicalRoute(path))
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

        private string DetermineUserCulture(HttpContext context)
        {
            // 1. Check Cookie
            var cookieValue = context.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName];
            var cookieCulture = ParseCultureFromCookie(cookieValue);
            if (!string.IsNullOrEmpty(cookieCulture) && IsSupportedCulture(cookieCulture, out var matchedCookieCulture))
            {
                return matchedCookieCulture!;
            }

            // 2. Check Accept-Language header
            var acceptLanguages = context.Request.Headers["Accept-Language"].ToString();
            if (!string.IsNullOrEmpty(acceptLanguages))
            {
                var languages = acceptLanguages.Split(',')
                    .Select(x => x.Split(';')[0].Trim())
                    .Where(x => !string.IsNullOrEmpty(x));

                foreach (var lang in languages)
                {
                    if (IsSupportedCulture(lang, out var matchedHeaderCulture))
                    {
                        return matchedHeaderCulture!;
                    }
                }
            }

            // 3. Fallback to default culture
            return SupportedLanguagesProvider.DefaultCultureName;
        }

        private string? ParseCultureFromCookie(string? cookieValue)
        {
            if (string.IsNullOrEmpty(cookieValue)) return null;

            // Typically c=pl-PL|uic=pl-PL
            var parts = cookieValue.Split('|');
            foreach (var part in parts)
            {
                if (part.StartsWith("c=", StringComparison.OrdinalIgnoreCase))
                {
                    return part.Substring(2);
                }
            }

            return null;
        }

        private bool IsSupportedCulture(string cultureCode, out string? matchedCulture)
        {
            matchedCulture = null;
            if (string.IsNullOrEmpty(cultureCode))
            {
                return false;
            }

            var supported = SupportedLanguagesProvider.SupportedCultureNames;
            
            // Try exact match first
            var match = supported.FirstOrDefault(c => string.Equals(c, cultureCode, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                matchedCulture = match;
                return true;
            }

            // Try matching prefix (e.g. "pl" from "pl-PL")
            var prefix = cultureCode.Split('-')[0];
            match = supported.FirstOrDefault(c => string.Equals(c.Split('-')[0], prefix, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                matchedCulture = match;
                return true;
            }

            return false;
        }
    }
}
