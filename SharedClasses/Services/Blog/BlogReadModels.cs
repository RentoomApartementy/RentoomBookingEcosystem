using RentoomBooking.SharedClasses.Services.Descriptions;

namespace RentoomBooking.SharedClasses.Services.Blog;

public sealed class CursorPage<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
}

public sealed class BlogPostListItem
{
    public int Id { get; init; }
    public Guid PublicId { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string SourceLanguage { get; init; } = "pl";
    public string Title { get; init; } = string.Empty;
    public string? Subtitle { get; init; }
    public string AuthorDisplayName { get; init; } = string.Empty;
    public string? Excerpt { get; init; }
    public string? Category { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public DateTime PublishedAtUtc { get; init; }
    public string? HeroImageUrl { get; init; }
}

public sealed class BlogPostDetails
{
    public int Id { get; init; }
    public Guid PublicId { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string SourceLanguage { get; init; } = "pl";
    public string Title { get; init; } = string.Empty;
    public string? Subtitle { get; init; }
    public string AuthorDisplayName { get; init; } = string.Empty;
    public string? MetaTitle { get; init; }
    public string? MetaDescription { get; init; }
    public string? Excerpt { get; init; }
    public string? Category { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public DateTime PublishedAtUtc { get; init; }
    public string? HeroImageUrl { get; init; }
    public int PublishedVersionNo { get; init; }
    public bool IsPreview { get; init; }
    public DateTime? PreviewExpiresAtUtc { get; init; }
    public IReadOnlyList<BlogBlock> Blocks { get; init; } = Array.Empty<BlogBlock>();
    public BlogAdjacentPostLink? PreviousPost { get; init; }
    public BlogAdjacentPostLink? NextPost { get; init; }
}

public sealed class BlogAdjacentPostLink
{
    public Guid PublicId { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTime PublishedAtUtc { get; init; }
}

public sealed class BlogBlock
{
    public Guid BlockKey { get; init; }
    public int SortOrder { get; init; }
    public string BlockType { get; init; } = string.Empty;
    public string? TextContent { get; init; }
    public string? HtmlContent { get; init; }
    public string? ImageUrl { get; init; }
    public string? AltText { get; init; }
    public string? Caption { get; init; }
    public string? EmbedUrl { get; init; }
    public string? HeadingLevel { get; init; }
    public string? QuoteAuthor { get; init; }
    public string? DisplaySize { get; init; }
    public IReadOnlyList<FaqItemDto> FaqItems { get; init; } = Array.Empty<FaqItemDto>();
}
