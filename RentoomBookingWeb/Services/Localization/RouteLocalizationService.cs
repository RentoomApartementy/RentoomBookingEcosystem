using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace RentoomBookingWeb.Services.Localization
{
    public class RouteLocalizationService : IRouteLocalizationService
    {
        private readonly NavigationManager _navigationManager;

        public RouteLocalizationService(NavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
        }

        public string GetLocalizedUrl(string pageKey, string? culture = null)
        {
            culture ??= CultureInfo.CurrentUICulture.Name;
            
            // Normalize to short code if it's one of our main ones, or use the provided one
            var displayCulture = GetShortCultureCode(culture);
            var cultureKey = culture.ToLowerInvariant();

            if (LocalizedRouteRegistry.PageSlugs.TryGetValue(pageKey, out var slugs))
            {
                if (slugs.TryGetValue(cultureKey, out var slug))
                {
                    return $"/{displayCulture}/{slug}";
                }
                
                // Fallback to 2-letter code for lookup
                var shortCulture = cultureKey.Split('-')[0];
                if (slugs.TryGetValue(shortCulture, out var shortSlug))
                {
                    return $"/{displayCulture}/{shortSlug}";
                }
                
                // Final fallback to Polish slug
                if (slugs.TryGetValue("pl", out var plSlug))
                {
                    return $"/{displayCulture}/{plSlug}";
                }
            }

            return $"/{displayCulture}";
        }

        public string GetUrlWithCulture(string path, string culture)
        {
            var displayCulture = GetShortCultureCode(culture);
            var trimmedPath = path.TrimStart('/');
            return $"/{displayCulture}/{trimmedPath}";
        }

        private string GetShortCultureCode(string culture)
        {
            var parts = culture.Split('-');
            return parts[0].ToLowerInvariant();
        }

        public bool TryGetPageKeyFromSlug(string slug, string culture, out string? pageKey)
        {
            pageKey = null;
            var cultureKey = culture.ToLowerInvariant();
            var shortCulture = cultureKey.Split('-')[0];

            foreach (var entry in LocalizedRouteRegistry.PageSlugs)
            {
                var slugs = entry.Value;
                if (slugs.TryGetValue(cultureKey, out var s) && string.Equals(s, slug, StringComparison.OrdinalIgnoreCase))
                {
                    pageKey = entry.Key;
                    return true;
                }
                if (slugs.TryGetValue(shortCulture, out var s2) && string.Equals(s2, slug, StringComparison.OrdinalIgnoreCase))
                {
                    pageKey = entry.Key;
                    return true;
                }
            }

            return false;
        }
    }
}
