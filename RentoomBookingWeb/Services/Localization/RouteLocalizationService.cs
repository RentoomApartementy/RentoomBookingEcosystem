using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using System.Collections.Generic;
using System.Linq;
using System.Resources;

namespace RentoomBookingWeb.Services.Localization
{
    public class RouteLocalizationService : IRouteLocalizationService
    {
        private readonly NavigationManager _navigationManager;
        private readonly ResourceManager _resourceManager;

        public RouteLocalizationService(NavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
            _resourceManager = PageRoutes.ResourceManager;
        }

        public string GetLocalizedUrl(string pageKey, string? culture = null)
        {
            culture ??= CultureInfo.CurrentUICulture.Name;
            var displayCulture = GetShortCultureCode(culture);

            // SPECIAL CASE: Home page is always the root of the culture
            if (pageKey == "Home")
            {
                return $"/{displayCulture}";
            }

            var slug = GetSlugFromResources(pageKey, culture);
            
            if (string.IsNullOrEmpty(slug))
            {
                // Fallback to Polish
                slug = GetSlugFromResources(pageKey, "pl-PL");
            }

            if (string.IsNullOrEmpty(slug))
            {
                // Final fallback to key
                slug = pageKey.ToLowerInvariant();
            }

            return $"/{displayCulture}/{slug}";
        }

        public LocalizedUrlBuilder CreateBuilder(string pageKey, string? culture = null)
        {
            return new LocalizedUrlBuilder(GetLocalizedUrl(pageKey, culture));
        }

        public string GetUrlWithCulture(string path, string culture)
        {
            var displayCulture = GetShortCultureCode(culture);
            var trimmedPath = path.TrimStart('/');
            
            // If it's the root path
            if (string.IsNullOrEmpty(trimmedPath))
            {
                return $"/{displayCulture}";
            }

            return $"/{displayCulture}/{trimmedPath}";
        }

        public bool TryGetPageKeyFromSlug(string slug, string culture, out string? pageKey)
        {
            pageKey = null;
            var cultureInfo = new CultureInfo(culture);

            // We need to check all keys in the mapping
            foreach (var key in PageMapping.KeyToComponent.Keys)
            {
                // Home has no slug in the URL (it's handled by segments.Length == 0 in the router)
                if (key == "Home") continue;

                var translatedSlug = _resourceManager.GetString(key, cultureInfo);
                if (string.Equals(translatedSlug, slug, StringComparison.OrdinalIgnoreCase))
                {
                    pageKey = key;
                    return true;
                }
            }

            // Fallback to Polish
            var plCulture = new CultureInfo("pl-PL");
            foreach (var key in PageMapping.KeyToComponent.Keys)
            {
                if (key == "Home") continue;

                var translatedSlug = _resourceManager.GetString(key, plCulture);
                if (string.Equals(translatedSlug, slug, StringComparison.OrdinalIgnoreCase))
                {
                    pageKey = key;
                    return true;
                }
            }

            return false;
        }

        private string? GetSlugFromResources(string key, string culture)
        {
            try
            {
                return _resourceManager.GetString(key, new CultureInfo(culture));
            }
            catch
            {
                return null;
            }
        }

        private string GetShortCultureCode(string culture)
        {
            var parts = culture.Split('-');
            return parts[0].ToLowerInvariant();
        }
    }
}
