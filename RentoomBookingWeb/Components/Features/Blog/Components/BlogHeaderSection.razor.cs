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
    public BlogPostListItem? Item { get; set; }

    protected override void OnInitialized()
    {
        if (Item is null)
        {
            Item = new BlogPostListItem
            {
                Title = "Toruń na weekend",
                Excerpt = "Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since 1966, when designers at Letraset and James Mosley, the librarian at St Bride ...",
                HeroImageUrl = "https://storagerentoombooking.blob.core.windows.net/blog-assets-prod/blog/2026/07/posts/2/7e2cffee696f493f86fb248e86bfaa23.webp",
                Category = "WAKACJE",
                Tags = new[] { "WAKACJE", "WOLNE" },
                PublishedAtUtc = new System.DateTime(2026, 4, 24)
            };
        }
    }

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