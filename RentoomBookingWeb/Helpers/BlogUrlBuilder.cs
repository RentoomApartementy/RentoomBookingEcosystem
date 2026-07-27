using RentoomBooking.SharedClasses.Services.Blog;
using RentoomBookingWeb.Services.Localization;

namespace RentoomBookingWeb.Helpers;

public static class BlogUrlBuilder
{
    public static string BuildPostUrl(IRouteLocalizationService routeService, string? category, string slug, string? culture = null)
        => $"{routeService.GetLocalizedUrl("BlogPost", culture)}/{BlogRouteHelper.GetCategorySlug(category)}/{slug}";

    /// <summary>
    /// Builds the category list URL, e.g. /blog/aktualnosci. Accepts either a raw category name
    /// (it will be slugified) or an already-slugified value (slugifying is idempotent).
    /// </summary>
    public static string BuildCategoryListUrl(IRouteLocalizationService routeService, string? category, string? culture = null)
        => $"{routeService.GetLocalizedUrl("BlogList", culture)}/{BlogRouteHelper.GetCategorySlug(category)}";
}
