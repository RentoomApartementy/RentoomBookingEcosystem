using Microsoft.AspNetCore.Components;
using RentoomBooking.SharedClasses.Services.Blog;

namespace RentoomBookingWeb.Components.Features.Blog.Pages;

public partial class BlogPostPage : ComponentBase
{
    protected BlogPostDetails? Post;
    protected bool IsLoading;
    protected string? Error;
    protected bool IsPreviewMode => Post?.IsPreview == true;
    protected bool HasInstagramEmbeds => Post?.Blocks.Any(x =>
        string.Equals(x.BlockType, "Instagram", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(x.EmbedHtml)) == true;

    [SupplyParameterFromQuery(Name = "preview")]
    public string? PreviewToken { get; set; }

    protected override async Task OnParametersSetAsync()
    {

        try
        {
            IsLoading = true;
            Error = null;
            var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;

            Post = string.IsNullOrWhiteSpace(PreviewToken)
                ? await BlogContentReader.GetPublishedPostAsync(
                    PublicId,
                    Slug,
                    culture,
                    CancellationToken.None)
                : await BlogContentReader.GetPreviewPostAsync(
                    PublicId,
                    Slug,
                    PreviewToken,
                    culture,
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

    protected string BuildPostUrl(Guid publicId, string slug) => $"{RouteService.GetLocalizedUrl("BlogPost")}/{publicId:D}/{slug}";

    protected MarkupString GetJsonLd()
    {
        if (Post is null) return new MarkupString(string.Empty);

        var canonicalUrl = $"{NavManager.BaseUri.TrimEnd('/')}{BuildPostUrl(Post.PublicId, Post.Slug)}";
        var title = System.Text.Json.JsonSerializer.Serialize(Post.Title);
        var excerpt = System.Text.Json.JsonSerializer.Serialize(Post.Excerpt ?? Post.MetaDescription ?? Post.Title);
        var author = System.Text.Json.JsonSerializer.Serialize(Post.AuthorDisplayName);
        var imageUrl = System.Text.Json.JsonSerializer.Serialize(Post.HeroImageUrl ?? string.Empty);
        var dateIso = Post.PublishedAtUtc.ToString("o");

        var json = $$"""
        {
          "@context": "https://schema.org",
          "@type": "BlogPosting",
          "headline": {{title}},
          "image": {{imageUrl}},
          "datePublished": "{{dateIso}}",
          "dateModified": "{{dateIso}}",
          "author": {
            "@type": "Person",
            "name": {{author}}
          },
          "description": {{excerpt}},
          "mainEntityOfPage": {
            "@type": "WebPage",
            "@id": "{{canonicalUrl}}"
          }
        }
        """;

        return new MarkupString(json);
    }

    protected BlogPostListItem? MapToListItem(BlogPostDetails? details)
    {
        if (details is null) return null;

        return new BlogPostListItem
        {
            Id = details.Id,
            PublicId = details.PublicId,
            Slug = details.Slug,
            SourceLanguage = details.SourceLanguage,
            Title = details.Title,
            Subtitle = details.Subtitle,
            AuthorDisplayName = details.AuthorDisplayName,
            Excerpt = details.Excerpt,
            Category = details.Category,
            Tags = details.Tags,
            PublishedAtUtc = details.PublishedAtUtc,
            HeroImageUrl = details.HeroImageUrl
        };
    }

    protected static string ResolveImageSizeClass(string? displaySize)
    {
        return $"blog-image-size-{NormalizeDisplaySize(displaySize)}";
    }

    private static string NormalizeDisplaySize(string? displaySize)
    {
        return string.IsNullOrWhiteSpace(displaySize) ? "m" : displaySize.Trim().ToLowerInvariant();
    }

    protected static string NormalizeHeading(string? headingLevel)
    {
        return headingLevel?.ToUpperInvariant() switch
        {
            "H1" => "h1",
            "H2" => "h2",
            "H3" => "h3",
            "H4" => "h4",
            "H5" => "h5",
            "H6" => "h6",
            _ => "h2"
        };
    }

    protected static string NormalizeBlockType(string? blockType)
    {
        if (string.IsNullOrWhiteSpace(blockType))
        {
            return string.Empty;
        }

        return blockType.Trim().ToLowerInvariant() switch
        {
            "heading" => "Heading",
            "paragraph" => "Paragraph",
            "quote" => "Quote",
            "image" => "Image",
            "faq" => "Faq",
            "youtube" => "YouTube",
            "instagram" => "Instagram",
            "apartmentslisting" => "ApartmentsListing",
            _ => blockType.Trim()
        };
    }

    [Inject] public ILogger<BlogPostPage> Logger { get; set; } = default!;
    [Parameter] public Guid PublicId { get; set; }
    [Parameter] public string Slug { get; set; } = string.Empty;
}
