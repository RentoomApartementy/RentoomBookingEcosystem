using RentoomBooking.SharedClasses.Services.Blog;
using RentoomBookingWeb.Services.Localization;

namespace RentoomBookingWeb.Helpers;

public static class BlogUrlBuilder
{
    public static string BuildPostUrl(IRouteLocalizationService routeService, string? category, string slug, string? culture = null)
        => $"{routeService.GetLocalizedUrl("BlogPost", culture)}/{BlogRouteHelper.GetCategorySlug(category)}/{slug}";
}
