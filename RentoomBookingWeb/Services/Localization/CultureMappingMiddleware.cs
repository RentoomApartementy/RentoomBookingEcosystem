using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RentoomBooking.SharedFrontend.Localization;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace RentoomBookingWeb.Services.Localization
{
    public class CultureMappingMiddleware
    {
        private readonly RequestDelegate _next;

        public CultureMappingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IRouteLocalizationService routeService)
        {
            var path = context.Request.Path.Value;
            
            // CRITICAL: Immediately skip if it looks like a file or framework route
            if (string.IsNullOrWhiteSpace(path) || IsTechnicalRoute(path))
            {
                await _next(context);
                return;
            }

            var parts = path.TrimStart('/').Split('/');
            if (parts.Length == 0) 
            {
                await _next(context);
                return;
            }
            
            var potentialCulture = parts[0];
            var supportedCultures = SupportedLanguagesProvider.SupportedCultureNames;

            if (supportedCultures.Any(c => string.Equals(c, potentialCulture, StringComparison.OrdinalIgnoreCase)))
            {
                // Normalize to the exact case from supported list
                var culture = supportedCultures.First(c => string.Equals(c, potentialCulture, StringComparison.OrdinalIgnoreCase));

                // Set the culture for the current request
                var cultureInfo = new CultureInfo(culture);
                CultureInfo.CurrentCulture = cultureInfo;
                CultureInfo.CurrentUICulture = cultureInfo;

                // We don't rewrite the path here anymore because Blazor's Router 
                // needs to see the full URL to match localized routes in CultureDispatcher.razor.
                // We only add culture to query string for standard UseRequestLocalization middleware.
                AddCultureToQueryString(context, culture);
            }
            else
            {
                // REDIRECT logic for legacy URLs
                if (context.Request.Method == "GET")
                {
                    var currentCulture = CultureInfo.CurrentUICulture.Name;
                    var pageKey = GetPageKeyFromCanonicalPath(path);
                    
                    if (pageKey != null)
                    {
                        var localizedUrl = routeService.GetLocalizedUrl(pageKey, currentCulture);
                        context.Response.Redirect(localizedUrl, permanent: true);
                        return;
                    }
                    else if (path == "/" || path == "")
                    {
                        context.Response.Redirect($"/{currentCulture}", permanent: true);
                        return;
                    }
                    // For other non-prefixed but non-technical routes, we might want to redirect with prefix
                    else if (!path.StartsWith("/_") && !path.StartsWith("/api/") && !path.Contains("."))
                    {
                         var localizedUrl = $"/{currentCulture}{path}";
                         context.Response.Redirect(localizedUrl, permanent: true);
                         return;
                    }
                }
            }

            await _next(context);
        }

        private void AddCultureToQueryString(HttpContext context, string culture)
        {
            var queryString = context.Request.QueryString.Value;
            var cultureQuery = $"culture={culture}&ui-culture={culture}";
            if (string.IsNullOrEmpty(queryString))
            {
                context.Request.QueryString = new QueryString("?" + cultureQuery);
            }
            else if (!queryString.Contains("culture="))
            {
                context.Request.QueryString = new QueryString(queryString + "&" + cultureQuery);
            }
        }

        private bool IsTechnicalRoute(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            // Normalize path for check
            var lowPath = path.ToLowerInvariant();

            // Framework and API routes
            if (lowPath.StartsWith("/_") || 
                lowPath.StartsWith("/api/") || 
                lowPath.StartsWith("/oplac/") || 
                lowPath.StartsWith("/rezerwuj/") || 
                lowPath.StartsWith("/status/") || 
                lowPath.StartsWith("/404") || 
                lowPath.StartsWith("/error") ||
                lowPath.StartsWith("/swagger") ||
                lowPath.Contains("/_blazor"))
            {
                return true;
            }

            // Static files (anything with a dot that isn't a directory-like path)
            if (path.Contains('.') && !path.EndsWith("/"))
            {
                return true;
            }

            return false;
        }

        private string? GetPageKeyFromCanonicalPath(string path)
        {
            var trimmedPath = path.Trim('/');
            return trimmedPath switch
            {
                "apartamenty" => "Apartments",
                "kontakt" => "Contact",
                "wspolpraca" => "Cooperation",
                "regulaminy" => "Statute",
                _ => null
            };
        }
    }
}
