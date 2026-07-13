using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using RentoomBooking.SharedClasses.Services.Blog;

namespace RentoomBookingWeb.Components.Features.Blog.Pages;

public partial class BlogPostPage : ComponentBase, IAsyncDisposable
{
    protected BlogPostDetails? Post;
    protected bool IsLoading;
    protected string? Error;
    protected bool IsPreviewMode => Post?.IsPreview == true;
    protected bool HasInstagramEmbeds => Post?.Blocks.Any(x =>
        string.Equals(x.BlockType, "Instagram", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(x.EmbedHtml)) == true;

    private IJSObjectReference? _jsModule;
    private string? _processedInstagramSignature;
    private bool _disposed;

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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || !HasInstagramEmbeds || Post is null)
        {
            return;
        }

        try
        {
            _jsModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/blogInstagramEmbed.js");
            var currentSignature = BuildInstagramSignature(Post);

            if (firstRender || !string.Equals(_processedInstagramSignature, currentSignature, StringComparison.Ordinal))
            {
                await _jsModule.InvokeVoidAsync("processEmbeds");
                _processedInstagramSignature = currentSignature;
            }
        }
        catch (JSDisconnectedException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to process Instagram embeds for blog post {Slug}.", Slug);
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

    protected static string ResolveYoutubeSizeClass(string? displaySize)
    {
        return $"blog-youtube-size-{NormalizeDisplaySize(displaySize)}";
    }

    protected static string ResolveAspectRatioStyle(int? width, int? height)
    {
        if (width is > 0 && height is > 0)
        {
            return $"aspect-ratio: {width.Value} / {height.Value};";
        }

        return "aspect-ratio: 16 / 9;";
    }

    private static string NormalizeDisplaySize(string? displaySize)
    {
        return string.IsNullOrWhiteSpace(displaySize) ? "m" : displaySize.Trim().ToLowerInvariant();
    }

    private static string BuildInstagramSignature(BlogPostDetails post)
    {
        var relevantBlocks = post.Blocks
            .Where(x => string.Equals(x.BlockType, "Instagram", StringComparison.OrdinalIgnoreCase))
            .Select(x => $"{x.BlockKey:N}:{x.EmbedHtml}")
            .ToArray();

        return $"{post.PublicId:N}:{string.Join("|", relevantBlocks)}";
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
            _ => blockType.Trim()
        };
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;

        if (_jsModule is not null)
        {
            try
            {
                await _jsModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to dispose blog post JS module.");
            }
        }
    }

    [Inject] public ILogger<BlogPostPage> Logger { get; set; } = default!;
    [Inject] public IJSRuntime JS { get; set; } = default!;
    [Parameter] public Guid PublicId { get; set; }
    [Parameter] public string Slug { get; set; } = string.Empty;
}
