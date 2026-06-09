using System.Globalization;

namespace RentoomBookingWeb.Services.Localization
{
    public interface IRouteLocalizationService
    {
        string GetLocalizedUrl(string pageKey, string? culture = null);
        string? GetSlug(string pageKey, string culture);
        string ResolveFullCulture(string cultureCode);
        string GetUrlWithCulture(string path, string culture);
        IEnumerable<string> GetPageKeysFromSlug(string slug, string culture);
        bool TryGetPageKeyFromSlug(string slug, string culture, out string? pageKey);
        LocalizedUrlBuilder CreateBuilder(string pageKey, string? culture = null);
    }
}
