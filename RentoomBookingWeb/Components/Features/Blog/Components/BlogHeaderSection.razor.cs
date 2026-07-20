using Microsoft.AspNetCore.Components;
using RentoomBooking.SharedClasses.Services.Blog;

namespace RentoomBookingWeb.Components.Features.Blog.Components;

public partial class BlogHeaderSection : ComponentBase
{
    [Inject] 
    protected RentoomBookingWeb.Services.Localization.IRouteLocalizationService RouteService { get; set; } = default!;

    [Inject] 
    internal Microsoft.Extensions.Localization.IStringLocalizer<RentoomBookingWeb.Blog> Localizer { get; set; } = default!;

    [Parameter] 
    public required BlogPostListItem Item { get; set; } = null!;

    [Parameter]
    public bool ShowReadMoreButton { get; set; } = true;

    [Parameter]
    public bool UseH1ForTitle { get; set; } = true;

    private string BuildPostUrl(Guid publicId, string slug) 
        => $"{RouteService.GetLocalizedUrl("BlogPost")}/{publicId:D}/{slug}";

    private string GetReadTimeText()
    {
        // Estimate read time (roughly 200 chars per minute, min 3 minutes)
        var charCount = (Item.Excerpt?.Length ?? 0) + Item.Title.Length + 300;
        var minutes = Math.Max(3, (int)Math.Ceiling(charCount / 200.0));
        return string.Format(Localizer["ReadTimeFormat"], minutes);
    }
}