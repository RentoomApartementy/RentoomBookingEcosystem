using System.Globalization;
using RentoomBooking.SharedFrontend.Localization;

namespace RentoomBookingWeb.Helpers;

public static class SeoCultureHelper
{
    private const string FallbackCulture = "pl-PL";
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;
    private static readonly Lazy<HashSet<string>> SupportedCultures = new(
        () => new HashSet<string>(SupportedLanguagesProvider.SupportedCultureNames, Comparer));

    public static string GetHtmlLang(CultureInfo? culture = null)
    {
        var resolved = CultureInfo.GetCultureInfo(ResolveSupportedCultureName(culture));
        return resolved.TwoLetterISOLanguageName.ToLowerInvariant();
    }

    public static string GetOgLocale(CultureInfo? culture = null)
    {
        var resolved = CultureInfo.GetCultureInfo(ResolveSupportedCultureName(culture));
        var specificCulture = resolved.IsNeutralCulture
            ? CultureInfo.CreateSpecificCulture(resolved.Name)
            : resolved;

        var nameParts = specificCulture.Name.Split('-', 2, StringSplitOptions.RemoveEmptyEntries);
        var language = specificCulture.TwoLetterISOLanguageName.ToLowerInvariant();
        var region = nameParts.Length > 1
            ? nameParts[1].ToUpperInvariant()
            : language.ToUpperInvariant();

        return $"{language}_{region}";
    }

    public static string ResolveSupportedCultureName(CultureInfo? culture = null)
    {
        var candidate = culture?.Name ?? CultureInfo.CurrentUICulture.Name;

        if (!string.IsNullOrWhiteSpace(candidate) && SupportedCultures.Value.Contains(candidate))
        {
            return candidate;
        }

        if (!string.IsNullOrWhiteSpace(candidate))
        {
            var requestedLang = CultureInfo.GetCultureInfo(candidate).TwoLetterISOLanguageName;
            var match = SupportedLanguagesProvider.SupportedCultures
                .FirstOrDefault(c => string.Equals(c.TwoLetterISOLanguageName, requestedLang, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match.Name;
            }
        }

        if (SupportedCultures.Value.Contains(FallbackCulture))
        {
            return FallbackCulture;
        }

        return SupportedLanguagesProvider.DefaultCultureName;
    }
}
