using System.Globalization;

namespace RentoomBookingWeb.Services.Localization
{
    public interface IRouteLocalizationService
    {
        string GetLocalizedUrl(string pageKey, string? culture = null);
        string GetUrlWithCulture(string path, string culture);
        bool TryGetPageKeyFromSlug(string slug, string culture, out string? pageKey);
    }
}
