using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using RentoomBooking.SharedFrontend.Localization;
using System.Globalization;
using System.Linq;

namespace RentoomBookingWeb.Services.Localization
{
    public interface IRouteLocalizationService
    {
        /// <summary>
        /// Gets the localized URL for a given page key and culture.
        /// </summary>
        string GetLocalizedUrl(string pageKey, string? culture = null);

        /// <summary>
        /// Tries to identify the page key from a localized slug and culture.
        /// </summary>
        bool TryGetPageKeyFromSlug(string slug, string culture, out string pageKey);

        /// <summary>
        /// Prepends or replaces the culture prefix in a given URL.
        /// </summary>
        string GetUrlWithCulture(string url, string culture);
        
        /// <summary>
        /// Returns the current culture from the URL if present.
        /// </summary>
        string? GetCultureFromUrl(string url);
    }

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
            
            // Normalize culture name (e.g. pl-PL -> pl if that's how it is in the registry)
            var registryCulture = GetRegistryCulture(culture);

            // Special case for Home
            if (pageKey == "Home")
            {
                return $"/{culture}";
            }

            if (LocalizedRouteRegistry.PageSlugs.TryGetValue(pageKey, out var slugs))
            {
                if (slugs.TryGetValue(registryCulture, out var slug))
                {
                    return $"/{culture}/{slug}";
                }
                
                // Fallback to default culture
                if (slugs.TryGetValue(SupportedLanguagesProvider.DefaultCultureName, out var defaultSlug))
                {
                    return $"/{culture}/{defaultSlug}";
                }
            }

            return $"/{culture}/{pageKey.ToLowerInvariant()}";
        }

        public bool TryGetPageKeyFromSlug(string slug, string culture, out string pageKey)
        {
            pageKey = string.Empty;
            var registryCulture = GetRegistryCulture(culture);

            foreach (var page in LocalizedRouteRegistry.PageSlugs)
            {
                if (page.Value.TryGetValue(registryCulture, out var localizedSlug) && 
                    string.Equals(localizedSlug, slug, StringComparison.OrdinalIgnoreCase))
                {
                    pageKey = page.Key;
                    return true;
                }
            }
            return false;
        }

        public string GetUrlWithCulture(string url, string culture)
        {
            if (string.IsNullOrWhiteSpace(url)) return $"/{culture}";
            if (url.StartsWith("http")) return url;

            var trimmedUrl = url.TrimStart('/');
            var parts = trimmedUrl.Split('/');
            
            if (parts.Length > 0 && IsSupportedCulture(parts[0]))
            {
                return $"/{culture}/{string.Join('/', parts.Skip(1))}".TrimEnd('/');
            }

            return $"/{culture}/{trimmedUrl}".TrimEnd('/');
        }

        public string? GetCultureFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            
            var uri = new Uri(_navigationManager.ToAbsoluteUri(url).ToString());
            var path = uri.AbsolutePath.TrimStart('/');
            var parts = path.Split('/');

            if (parts.Length > 0 && IsSupportedCulture(parts[0]))
            {
                return parts[0];
            }

            return null;
        }

        private bool IsSupportedCulture(string culture)
        {
            return SupportedLanguagesProvider.SupportedCultureNames.Any(c => string.Equals(c, culture, StringComparison.OrdinalIgnoreCase));
        }

        private string GetRegistryCulture(string culture)
        {
            // The registry uses both 'pl' and 'pl-pl'. We should check for exact match first.
            if (SupportedLanguagesProvider.SupportedCultureNames.Any(c => string.Equals(c, culture, StringComparison.OrdinalIgnoreCase)))
            {
                return culture;
            }
            
            // Try to match by language part (e.g. pl-PL -> pl)
            var langPart = culture.Split('-')[0];
            return langPart;
        }
    }
}
