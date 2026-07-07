using Microsoft.AspNetCore.Components;
using RentoomBooking.SharedClasses.Services.Blog;

namespace RentoomBookingWeb.Components.Features.Blog.Pages;

public partial class BlogPostPage : ComponentBase
{
    protected BlogPostDetails? Post;
    protected bool IsLoading;
    protected string? Error;

    protected override async Task OnParametersSetAsync()
    {
        try
        {
            IsLoading = true;
            Error = null;
            Post = await BlogContentReader.GetPublishedPostBySlugAsync(
                Slug,
                System.Globalization.CultureInfo.CurrentUICulture.Name,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            Error = "Could not load blog post.";
            Logger.LogError(ex, "Failed to load blog post {Slug}.", Slug);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [Inject] public ILogger<BlogPostPage> Logger { get; set; } = default!;
}
