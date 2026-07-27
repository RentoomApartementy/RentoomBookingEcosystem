namespace RentoomBooking.SharedClasses.Services.Blog;

public interface IBlogContentReader
{
    /// <summary>
    /// Published posts feed, newest first. When <paramref name="categorySlug"/> is provided (the
    /// URL-safe slug, e.g. "aktualnosci"), only posts whose category slugifies to the same value
    /// are returned; an unrecognized slug yields an empty page rather than an error.
    /// </summary>
    Task<CursorPage<BlogPostListItem>> GetPublishedPostsFeedAsync(
        string culture,
        string? cursor,
        int take,
        string? categorySlug = null,
        CancellationToken cancellationToken = default);

    Task<BlogPostDetails?> GetPublishedPostAsync(
        string category,
        string slug,
        string culture,
        CancellationToken cancellationToken = default);

    Task<BlogPostDetails?> GetPreviewPostAsync(
        string category,
        string slug,
        string previewToken,
        string culture,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BlogPostListItem>> GetAllPublishedPostsAsync(
        string culture,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Published posts that reference the given apartment — either via an inline link inserted
    /// in a Paragraph block (RentoomApp parses this into a structured `links` array in the
    /// block's PropsJson at save time), or via an ApartmentsListing block whose PropsJson int[]
    /// contains the apartment id (or is empty, meaning "all active apartments").
    /// </summary>
    Task<IReadOnlyList<BlogPostListItem>> GetRelatedPostsForApartmentAsync(
        int apartmentId,
        string culture,
        int take = 6,
        CancellationToken cancellationToken = default);
}
