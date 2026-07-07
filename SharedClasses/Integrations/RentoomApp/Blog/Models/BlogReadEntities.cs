using System.ComponentModel.DataAnnotations.Schema;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.Blog.Models;

[Table("blog_posts", Schema = "blog")]
public class BlogPostReadEntity
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = "pl";
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? Excerpt { get; set; }
    public string? Category { get; set; }
    public string? TagsJson { get; set; }
    public int? HeroMediaAssetId { get; set; }
    public int? HeroWebpMediaAssetId { get; set; }
    public string? HeroSelectedVariant { get; set; }
    public string? HeroImageUrl { get; set; }
    public string TemplateVersion { get; set; } = string.Empty;
    public int CurrentDraftVersionNo { get; set; }
    public int? PublishedVersionNo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

[Table("blog_post_versions", Schema = "blog")]
public class BlogPostVersionReadEntity
{
    public int Id { get; set; }
    public int BlogPostId { get; set; }
    public int VersionNo { get; set; }
    public string VersionState { get; set; } = string.Empty;
    public bool IsCurrentDraft { get; set; }
    public string? ChangeSummary { get; set; }
    public string? ContentChecksum { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
}

[Table("blog_post_blocks", Schema = "blog")]
public class BlogPostBlockReadEntity
{
    public int Id { get; set; }
    public int PostVersionId { get; set; }
    public Guid BlockKey { get; set; }
    public int SortOrder { get; set; }
    public string BlockType { get; set; } = string.Empty;
    public string? TextContent { get; set; }
    public int? MediaAssetId { get; set; }
    public string? ExternalUrl { get; set; }
    public string? EmbedProvider { get; set; }
    public string? EmbedId { get; set; }
    public string? AltText { get; set; }
    public string? Caption { get; set; }
    public string? PropsJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
