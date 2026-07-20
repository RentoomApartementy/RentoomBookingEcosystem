using RentoomBookingWeb.Services.Localization;

namespace SharedClasses.Tests.Localization;

internal sealed class FakeRouteLocalizationService : IRouteLocalizationService
{
    private readonly Dictionary<(string Slug, string Culture), string> _pageKeysBySlug = new();
    private readonly Dictionary<(string PageKey, string Culture), string> _localizedUrls = new();
    private readonly Dictionary<string, string> _pageKeysByAnySlug = new(StringComparer.OrdinalIgnoreCase);

    public FakeRouteLocalizationService MapSlug(string slug, string culture, string pageKey)
    {
        _pageKeysBySlug[(slug, culture)] = pageKey;
        return this;
    }

    public FakeRouteLocalizationService MapAnySlug(string slug, string pageKey)
    {
        _pageKeysByAnySlug[slug] = pageKey;
        return this;
    }

    public FakeRouteLocalizationService MapUrl(string pageKey, string culture, string url)
    {
        _localizedUrls[(pageKey, culture)] = url;
        return this;
    }

    public string GetLocalizedUrl(string pageKey, string? culture = null)
    {
        var resolvedCulture = culture ?? "pl-PL";
        if (_localizedUrls.TryGetValue((pageKey, resolvedCulture), out var url))
        {
            return url;
        }

        throw new InvalidOperationException($"Missing localized URL mapping for page '{pageKey}' and culture '{resolvedCulture}'.");
    }

    public string? GetSlug(string pageKey, string culture) => throw new NotSupportedException();

    public string ResolveFullCulture(string cultureCode) => cultureCode;

    public string GetUrlWithCulture(string path, string culture) => throw new NotSupportedException();

    public IEnumerable<string> GetPageKeysFromSlug(string slug, string culture) => throw new NotSupportedException();

    public bool TryGetPageKeyFromSlug(string slug, string culture, out string? pageKey)
    {
        if (_pageKeysBySlug.TryGetValue((slug, culture), out var match))
        {
            pageKey = match;
            return true;
        }

        pageKey = null;
        return false;
    }

    public bool TryFindPageKeyAnyCulture(string slug, out string? pageKey)
    {
        if (_pageKeysByAnySlug.TryGetValue(slug, out var match))
        {
            pageKey = match;
            return true;
        }

        pageKey = null;
        return false;
    }

    public LocalizedUrlBuilder CreateBuilder(string pageKey, string? culture = null) => throw new NotSupportedException();
}
