using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using RentoomBooking.SharedFrontend.Localization;

namespace RentoomBookingWeb.Services.Localization
{
    public class RouteLocalizationService : IRouteLocalizationService
    {
        private static readonly Dictionary<string, string> AlternativeSlugs = new(StringComparer.OrdinalIgnoreCase)
        {
            ["polozenie-torunia"] = "AboutCity",
            ["torun"] = "AboutCity"
        };

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

            var slug = GetSlug(pageKey, culture);
            
            if (string.IsNullOrEmpty(slug))
            {
                // Fallback to Polish
                slug = GetSlug(pageKey, "pl-PL");
            }

            if (string.IsNullOrEmpty(slug))
            {
                // Final fallback to key
                slug = pageKey.ToLowerInvariant();
            }

            return $"/{displayCulture}/{slug}";
        }

        public string? GetSlug(string pageKey, string culture)
        {
            try
            {
                var fullCulture = ResolveFullCulture(culture);
                var resourceKey = GetResourceKey(pageKey);
                return _resourceManager.GetString(resourceKey, new CultureInfo(fullCulture));
            }
            catch
            {
                return null;
            }
        }

        public string ResolveFullCulture(string cultureCode)
        {
            var matched = SupportedLanguagesProvider.SupportedCultureNames.FirstOrDefault(c => 
                string.Equals(c, cultureCode, StringComparison.OrdinalIgnoreCase) || 
                string.Equals(c.Split('-')[0], cultureCode, StringComparison.OrdinalIgnoreCase));
            
            return matched ?? "pl-PL";
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

        public IEnumerable<string> GetPageKeysFromSlug(string slug, string culture)
        {
            var results = new HashSet<string>();
            var fullCulture = ResolveFullCulture(culture);
            var cultureInfo = new CultureInfo(fullCulture);
            var plCulture = new CultureInfo("pl-PL");

            // Check current culture and fallback to Polish
            foreach (var key in PageMapping.KeyToComponent.Keys)
            {
                if (key == "Home") continue;

                var resourceKey = GetResourceKey(key);
                var translatedSlug = _resourceManager.GetString(resourceKey, cultureInfo);
                if (string.Equals(translatedSlug, slug, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(key);
                }

                var plSlug = _resourceManager.GetString(resourceKey, plCulture);
                if (string.Equals(plSlug, slug, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(key);
                }
            }

            return results;
        }

        public bool TryGetPageKeyFromSlug(string slug, string culture, out string? pageKey)
        {
            var keys = GetPageKeysFromSlug(slug, culture);
            pageKey = keys.FirstOrDefault();
            return pageKey != null;
        }

        private string GetShortCultureCode(string culture)
        {
            var parts = culture.Split('-');
            return parts[0].ToLowerInvariant();
        }

        public bool TryFindPageKeyAnyCulture(string slug, out string? pageKey)
        {
            if (string.IsNullOrEmpty(slug))
            {
                pageKey = null;
                return false;
            }

            // 1. Sprawdzamy historyczne / alternatywne aliasy
            if (AlternativeSlugs.TryGetValue(slug, out pageKey))
            {
                return true;
            }

            // 2. Szukamy dynamicznie we wszystkich językach wspieranych przez system
            foreach (var culture in SupportedLanguagesProvider.SupportedCultureNames)
            {
                var keys = GetPageKeysFromSlug(slug, culture);
                pageKey = keys.FirstOrDefault();
                if (pageKey != null)
                {
                    return true;
                }
            }

            pageKey = null;
            return false;
        }

        private string GetResourceKey(string key)
        {
            if (key == "ApartmentDetailWithToken")
            {
                return "ApartmentDetail";
            }
            return key;
        }
    }
}
