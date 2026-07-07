namespace RentoomBooking.SharedClasses.Services.Blog;

public interface IBlogContentReader
{
    Task<CursorPage<BlogPostListItem>> GetPublishedPostsFeedAsync(
        string culture,
        string? cursor,
        int take,
        CancellationToken cancellationToken = default);

    Task<BlogPostDetails?> GetPublishedPostBySlugAsync(
        string slug,
        string culture,
        CancellationToken cancellationToken = default);

    Task<BlogPostDetails?> GetPreviewPostBySlugAsync(
        string slug,
        string previewToken,
        string culture,
        CancellationToken cancellationToken = default);
}
