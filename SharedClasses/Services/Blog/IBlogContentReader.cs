namespace RentoomBooking.SharedClasses.Services.Blog;

public interface IBlogContentReader
{
    Task<CursorPage<BlogPostListItem>> GetPublishedPostsFeedAsync(
        string culture,
        string? cursor,
        int take,
        CancellationToken cancellationToken = default);

    Task<BlogPostDetails?> GetPublishedPostAsync(
        Guid publicId,
        string slug,
        string culture,
        CancellationToken cancellationToken = default);

    Task<BlogPostDetails?> GetPreviewPostAsync(
        Guid publicId,
        string slug,
        string previewToken,
        string culture,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BlogPostListItem>> GetAllPublishedPostsAsync(
        string culture,
        CancellationToken cancellationToken = default);
}
