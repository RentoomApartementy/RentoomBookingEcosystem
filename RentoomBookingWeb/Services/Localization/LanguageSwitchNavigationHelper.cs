using System.Globalization;
using RentoomBooking.SharedFrontend.Localization;

namespace RentoomBookingWeb.Services.Localization;

public static class LanguageSwitchNavigationHelper
{
    public static string BuildTargetPath(
        string currentRelativePath,
        string currentQuery,
        string currentCulture,
        string selectedCulture,
        IRouteLocalizationService routeService)
    {
        var relativePath = Uri.UnescapeDataString(currentRelativePath ?? string.Empty);

        var parts = relativePath.Split('/', StringSplitOptions.None);
        string? currentCultureInUrl = null;
        string? firstSlug = null;
        var remainingPath = string.Empty;

        if (parts.Length > 0 && HasCulturePrefix(parts[0]))
        {
            currentCultureInUrl = parts[0];
            firstSlug = parts.Length > 1 ? parts[1].Split('?')[0] : null;
            if (parts.Length > 2)
            {
                remainingPath = "/" + string.Join("/", parts.Skip(2));
            }
        }
        else
        {
            firstSlug = parts[0].Split('?')[0];
            if (parts.Length > 1)
            {
                remainingPath = "/" + string.Join("/", parts.Skip(1));
            }
        }

        string newUri;
        if (!string.IsNullOrEmpty(firstSlug) &&
            routeService.TryGetPageKeyFromSlug(firstSlug, currentCultureInUrl ?? currentCulture, out var pageKey))
        {
            newUri = routeService.GetLocalizedUrl(pageKey!, selectedCulture) + remainingPath;
        }
        else if (string.IsNullOrEmpty(firstSlug) || firstSlug == "/")
        {
            newUri = $"/{GetShortCode(selectedCulture)}";
        }
        else
        {
            if (routeService.TryFindPageKeyAnyCulture(firstSlug, out var pageKeyFromAny))
            {
                newUri = routeService.GetLocalizedUrl(pageKeyFromAny!, selectedCulture) + remainingPath;
            }
            else if (currentCultureInUrl is null)
            {
                newUri = "/" + firstSlug + remainingPath;
            }
            else
            {
                newUri = $"/{GetShortCode(selectedCulture)}/{firstSlug}{remainingPath}";
            }
        }

        if (!string.IsNullOrEmpty(currentQuery))
        {
            newUri += currentQuery;
        }

        return newUri;
    }

    private static bool HasCulturePrefix(string value)
    {
        return SupportedLanguagesProvider.SupportedCultureNames.Any(c =>
            string.Equals(c.Split('-')[0], value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c, value, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetShortCode(string culture)
    {
        return culture.Split('-')[0].ToLowerInvariant();
    }
}
