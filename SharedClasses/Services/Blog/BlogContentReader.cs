using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Blog.Database;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Blog.Models;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Database;
using RentoomBooking.SharedClasses.Services.Descriptions;
using RentoomBooking.SharedClasses.Models.Storage;

namespace RentoomBooking.SharedClasses.Services.Blog;

public sealed class BlogContentReader : IBlogContentReader
{
    public const string BlogStorageOptionsName = "BlogStorage";
    private const string PublishedStatus = "Published";
    private const int DefaultTake = 12;
    private const int MaxTake = 50;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDbContextFactory<RappBlogReadDbContext> _blogDbContextFactory;
    private readonly IDbContextFactory<RappPartnersDBContext> _partnersDbContextFactory;
    private readonly IMemoryCache _cache;
    private readonly StorageOptions _storageOptions;

    public BlogContentReader(
        IDbContextFactory<RappBlogReadDbContext> blogDbContextFactory,
        IDbContextFactory<RappPartnersDBContext> partnersDbContextFactory,
        IMemoryCache cache,
        IOptionsMonitor<StorageOptions> storageOptionsMonitor)
    {
        _blogDbContextFactory = blogDbContextFactory;
        _partnersDbContextFactory = partnersDbContextFactory;
        _cache = cache;
        _storageOptions = storageOptionsMonitor.Get(BlogStorageOptionsName);
    }

    public async Task<CursorPage<BlogPostListItem>> GetPublishedPostsFeedAsync(
        string culture,
        string? cursor,
        int take,
        CancellationToken cancellationToken = default)
    {
        var normalizedCulture = NormalizeSourceLanguage(culture);
        var normalizedTake = Math.Clamp(take <= 0 ? DefaultTake : take, 1, MaxTake);
        var cacheKey = $"blog:feed:{normalizedCulture}:{cursor ?? "first"}:{normalizedTake}";

        if (_cache.TryGetValue(cacheKey, out CursorPage<BlogPostListItem>? cached) && cached is not null)
        {
            return cached;
        }

        await using var dbContext = await _blogDbContextFactory.CreateDbContextAsync(cancellationToken);
        var parsedCursor = DecodeCursor(cursor);

        var query = dbContext.BlogPosts
            .AsNoTracking()
            .Where(x => x.DeletedAt == null)
            .Where(x => x.Status == PublishedStatus)
            .Where(x => x.PublishedAt != null)
            .Where(x => x.PublishedVersionNo != null)
            .Where(x => x.SourceLanguage == normalizedCulture);

        if (parsedCursor is not null)
        {
            query = query.Where(x =>
                x.PublishedAt < parsedCursor.PublishedAtUtc ||
                (x.PublishedAt == parsedCursor.PublishedAtUtc && x.Id < parsedCursor.Id));
        }

        var rows = await query
            .OrderByDescending(x => x.PublishedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new FeedRow
            {
                Id = x.Id,
                PublicId = x.PublicId,
                Slug = x.Slug,
                SourceLanguage = x.SourceLanguage,
                Title = x.Title,
                Subtitle = x.Subtitle,
                AuthorDisplayName = x.AuthorDisplayName,
                Excerpt = x.Excerpt,
                Category = x.Category,
                TagsJson = x.TagsJson,
                PublishedAtUtc = x.PublishedAt!.Value,
                HeroMediaAssetId = x.HeroMediaAssetId,
                HeroWebpMediaAssetId = x.HeroWebpMediaAssetId,
                HeroSelectedVariant = x.HeroSelectedVariant,
                HeroImageUrl = x.HeroImageUrl
            })
            .Take(normalizedTake + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > normalizedTake;
        var pageRows = rows.Take(normalizedTake).ToList();

        var assetUrlMap = await ResolveAssetUrlsAsync(
            pageRows.SelectMany(x => new[] { x.HeroMediaAssetId, x.HeroWebpMediaAssetId }),
            cancellationToken);

        var items = pageRows.Select(x => new BlogPostListItem
        {
            Id = x.Id,
            PublicId = x.PublicId,
            Slug = x.Slug,
            SourceLanguage = x.SourceLanguage,
            Title = x.Title,
            Subtitle = x.Subtitle,
            AuthorDisplayName = x.AuthorDisplayName,
            Excerpt = x.Excerpt,
            Category = x.Category,
            Tags = DeserializeTags(x.TagsJson),
            PublishedAtUtc = x.PublishedAtUtc,
            HeroImageUrl = ResolveImageUrl(
                x.HeroMediaAssetId,
                x.HeroWebpMediaAssetId,
                x.HeroSelectedVariant,
                x.HeroImageUrl,
                assetUrlMap)
        }).ToList();

        string? nextCursor = null;
        if (hasMore && pageRows.Count > 0)
        {
            var last = pageRows[^1];
            nextCursor = EncodeCursor(new BlogCursor(last.PublishedAtUtc, last.Id));
        }

        var result = new CursorPage<BlogPostListItem>
        {
            Items = items,
            NextCursor = nextCursor,
            HasMore = hasMore
        };

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(2));
        return result;
    }

    public async Task<BlogPostDetails?> GetPublishedPostAsync(
        Guid publicId,
        string slug,
        string culture,
        CancellationToken cancellationToken = default)
    {
        var normalizedCulture = NormalizeSourceLanguage(culture);
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var cacheKey = $"blog:post:{normalizedCulture}:{publicId:D}:{normalizedSlug}";

        if (_cache.TryGetValue(cacheKey, out BlogPostDetails? cached) && cached is not null)
        {
            return cached;
        }

        await using var dbContext = await _blogDbContextFactory.CreateDbContextAsync(cancellationToken);

        var post = await dbContext.BlogPosts
            .AsNoTracking()
            .Where(x => x.DeletedAt == null)
            .Where(x => x.Status == PublishedStatus)
            .Where(x => x.PublishedAt != null)
            .Where(x => x.PublishedVersionNo != null)
            .Where(x => x.SourceLanguage == normalizedCulture)
            .Where(x => x.PublicId == publicId)
            .Where(x => x.Slug == normalizedSlug)
            .Select(x => new PostRow
            {
                Id = x.Id,
                PublicId = x.PublicId,
                Slug = x.Slug,
                SourceLanguage = x.SourceLanguage,
                Title = x.Title,
                Subtitle = x.Subtitle,
                AuthorDisplayName = x.AuthorDisplayName,
                MetaTitle = x.MetaTitle,
                MetaDescription = x.MetaDescription,
                Excerpt = x.Excerpt,
                Category = x.Category,
                TagsJson = x.TagsJson,
                PublishedAtUtc = x.PublishedAt!.Value,
                HeroMediaAssetId = x.HeroMediaAssetId,
                HeroWebpMediaAssetId = x.HeroWebpMediaAssetId,
                HeroSelectedVariant = x.HeroSelectedVariant,
                HeroImageUrl = x.HeroImageUrl,
                PublishedVersionNo = x.PublishedVersionNo!.Value
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (post is null)
        {
            return null;
        }

        var version = await dbContext.BlogPostVersions
            .AsNoTracking()
            .Where(x => x.BlogPostId == post.Id && x.VersionNo == post.PublishedVersionNo)
            .Select(x => new VersionRow
            {
                Id = x.Id,
                VersionNo = x.VersionNo
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (version is null)
        {
            return null;
        }

        var blocks = await dbContext.BlogPostBlocks
            .AsNoTracking()
            .Where(x => x.PostVersionId == version.Id)
            .OrderBy(x => x.SortOrder)
            .Select(x => new BlockRow
            {
                BlockKey = x.BlockKey,
                SortOrder = x.SortOrder,
                BlockType = x.BlockType,
                TextContent = x.TextContent,
                MediaAssetId = x.MediaAssetId,
                ExternalUrl = x.ExternalUrl,
                EmbedProvider = x.EmbedProvider,
                AltText = x.AltText,
                Caption = x.Caption,
                PropsJson = x.PropsJson
            })
            .ToListAsync(cancellationToken);

        var adjacent = await GetAdjacentCoreAsync(dbContext, post.Slug, normalizedCulture, post.PublishedAtUtc, post.Id, cancellationToken);

        var assetIds = new List<int?>();
        assetIds.Add(post.HeroMediaAssetId);
        assetIds.Add(post.HeroWebpMediaAssetId);
        foreach (var block in blocks)
        {
            assetIds.Add(block.MediaAssetId);
            assetIds.Add(GetIntProp(block.PropsJson, "webpMediaAssetId"));
        }

        var assetUrlMap = await ResolveAssetUrlsAsync(assetIds, cancellationToken);

        var result = new BlogPostDetails
        {
            Id = post.Id,
            PublicId = post.PublicId,
            Slug = post.Slug,
            SourceLanguage = post.SourceLanguage,
            Title = post.Title,
            Subtitle = post.Subtitle,
            AuthorDisplayName = post.AuthorDisplayName,
            MetaTitle = post.MetaTitle,
            MetaDescription = post.MetaDescription,
            Excerpt = post.Excerpt,
            Category = post.Category,
            Tags = DeserializeTags(post.TagsJson),
            PublishedAtUtc = post.PublishedAtUtc,
            HeroImageUrl = ResolveImageUrl(post.HeroMediaAssetId, post.HeroWebpMediaAssetId, post.HeroSelectedVariant, post.HeroImageUrl, assetUrlMap),
            PublishedVersionNo = post.PublishedVersionNo,
            Blocks = blocks.Select(x => MapBlock(x, assetUrlMap)).ToList(),
            PreviousPost = adjacent.PreviousPost,
            NextPost = adjacent.NextPost
        };

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(4));
        return result;
    }

    public async Task<BlogPostDetails?> GetPreviewPostAsync(
        Guid publicId,
        string slug,
        string previewToken,
        string culture,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(previewToken))
        {
            return null;
        }

        var normalizedCulture = NormalizeSourceLanguage(culture);
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var normalizedToken = previewToken.Trim();
        var cacheKey = $"blog:preview:{normalizedCulture}:{publicId:D}:{normalizedSlug}:{normalizedToken}";

        if (_cache.TryGetValue(cacheKey, out BlogPostDetails? cached) && cached is not null)
        {
            return cached;
        }

        await using var dbContext = await _blogDbContextFactory.CreateDbContextAsync(cancellationToken);
        var tokenHash = ComputeSha256(normalizedToken);
        var utcNow = DateTime.UtcNow;

        var post = await dbContext.BlogPosts
            .AsNoTracking()
            .Where(x => x.DeletedAt == null)
            .Where(x => x.SourceLanguage == normalizedCulture)
            .Where(x => x.PublicId == publicId)
            .Where(x => x.Slug == normalizedSlug)
            .Where(x => x.PreviewTokenHash != null)
            .Where(x => x.PreviewTokenExpiresAt != null)
            .Where(x => x.PreviewTokenExpiresAt > utcNow)
            .Where(x => x.PreviewTokenHash == tokenHash)
            .Select(x => new PreviewPostRow
            {
                Id = x.Id,
                PublicId = x.PublicId,
                Slug = x.Slug,
                SourceLanguage = x.SourceLanguage,
                Title = x.Title,
                Subtitle = x.Subtitle,
                AuthorDisplayName = x.AuthorDisplayName,
                MetaTitle = x.MetaTitle,
                MetaDescription = x.MetaDescription,
                Excerpt = x.Excerpt,
                Category = x.Category,
                TagsJson = x.TagsJson,
                PublishedAtUtc = x.PublishedAt,
                HeroMediaAssetId = x.HeroMediaAssetId,
                HeroWebpMediaAssetId = x.HeroWebpMediaAssetId,
                HeroSelectedVariant = x.HeroSelectedVariant,
                HeroImageUrl = x.HeroImageUrl,
                CurrentDraftVersionNo = x.CurrentDraftVersionNo,
                PreviewExpiresAtUtc = x.PreviewTokenExpiresAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (post is null)
        {
            return null;
        }

        var version = await dbContext.BlogPostVersions
            .AsNoTracking()
            .Where(x => x.BlogPostId == post.Id && x.VersionNo == post.CurrentDraftVersionNo)
            .Select(x => new VersionRow
            {
                Id = x.Id,
                VersionNo = x.VersionNo
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (version is null)
        {
            return null;
        }

        var blocks = await dbContext.BlogPostBlocks
            .AsNoTracking()
            .Where(x => x.PostVersionId == version.Id)
            .OrderBy(x => x.SortOrder)
            .Select(x => new BlockRow
            {
                BlockKey = x.BlockKey,
                SortOrder = x.SortOrder,
                BlockType = x.BlockType,
                TextContent = x.TextContent,
                MediaAssetId = x.MediaAssetId,
                ExternalUrl = x.ExternalUrl,
                EmbedProvider = x.EmbedProvider,
                AltText = x.AltText,
                Caption = x.Caption,
                PropsJson = x.PropsJson
            })
            .ToListAsync(cancellationToken);

        var assetIds = new List<int?>();
        assetIds.Add(post.HeroMediaAssetId);
        assetIds.Add(post.HeroWebpMediaAssetId);
        foreach (var block in blocks)
        {
            assetIds.Add(block.MediaAssetId);
            assetIds.Add(GetIntProp(block.PropsJson, "webpMediaAssetId"));
        }

        var assetUrlMap = await ResolveAssetUrlsAsync(assetIds, cancellationToken);

        var result = new BlogPostDetails
        {
            Id = post.Id,
            PublicId = post.PublicId,
            Slug = post.Slug,
            SourceLanguage = post.SourceLanguage,
            Title = post.Title,
            Subtitle = post.Subtitle,
            AuthorDisplayName = post.AuthorDisplayName,
            MetaTitle = post.MetaTitle,
            MetaDescription = post.MetaDescription,
            Excerpt = post.Excerpt,
            Category = post.Category,
            Tags = DeserializeTags(post.TagsJson),
            PublishedAtUtc = post.PublishedAtUtc ?? DateTime.MinValue,
            HeroImageUrl = ResolveImageUrl(post.HeroMediaAssetId, post.HeroWebpMediaAssetId, post.HeroSelectedVariant, post.HeroImageUrl, assetUrlMap),
            PublishedVersionNo = post.CurrentDraftVersionNo,
            IsPreview = true,
            PreviewExpiresAtUtc = post.PreviewExpiresAtUtc,
            Blocks = blocks.Select(x => MapBlock(x, assetUrlMap)).ToList(),
            PreviousPost = null,
            NextPost = null
        };

        _cache.Set(cacheKey, result, TimeSpan.FromSeconds(30));
        return result;
    }

    private static async Task<(BlogAdjacentPostLink? PreviousPost, BlogAdjacentPostLink? NextPost)> GetAdjacentCoreAsync(
        RappBlogReadDbContext dbContext,
        string slug,
        string culture,
        DateTime publishedAtUtc,
        int id,
        CancellationToken cancellationToken)
    {
        var baseQuery = dbContext.BlogPosts
            .AsNoTracking()
            .Where(x => x.DeletedAt == null)
            .Where(x => x.Status == PublishedStatus)
            .Where(x => x.PublishedAt != null)
            .Where(x => x.PublishedVersionNo != null)
            .Where(x => x.SourceLanguage == culture)
            .Where(x => x.Slug != slug);

        var next = await baseQuery
            .Where(x => x.PublishedAt > publishedAtUtc || (x.PublishedAt == publishedAtUtc && x.Id > id))
            .OrderBy(x => x.PublishedAt)
            .ThenBy(x => x.Id)
            .Select(x => new BlogAdjacentPostLink
            {
                PublicId = x.PublicId,
                Slug = x.Slug,
                Title = x.Title,
                PublishedAtUtc = x.PublishedAt!.Value
            })
            .FirstOrDefaultAsync(cancellationToken);

        var previous = await baseQuery
            .Where(x => x.PublishedAt < publishedAtUtc || (x.PublishedAt == publishedAtUtc && x.Id < id))
            .OrderByDescending(x => x.PublishedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new BlogAdjacentPostLink
            {
                PublicId = x.PublicId,
                Slug = x.Slug,
                Title = x.Title,
                PublishedAtUtc = x.PublishedAt!.Value
            })
            .FirstOrDefaultAsync(cancellationToken);

        return (previous, next);
    }

    private BlogBlock MapBlock(BlockRow row, IReadOnlyDictionary<int, string> assetUrlMap)
    {
        var blockType = row.BlockType ?? string.Empty;
        var htmlContent = string.Equals(blockType, "Paragraph", StringComparison.OrdinalIgnoreCase)
            ? SanitizeHtml(row.TextContent)
            : null;
        var faqItems = string.Equals(blockType, "Faq", StringComparison.OrdinalIgnoreCase)
            ? GetFaqItems(row.PropsJson)
            : Array.Empty<FaqItemDto>();

        return new BlogBlock
        {
            BlockKey = row.BlockKey,
            SortOrder = row.SortOrder,
            BlockType = blockType,
            TextContent = row.TextContent,
            HtmlContent = htmlContent,
            ImageUrl = string.Equals(blockType, "Image", StringComparison.OrdinalIgnoreCase)
                ? ResolveImageUrl(
                    row.MediaAssetId,
                    GetIntProp(row.PropsJson, "webpMediaAssetId"),
                    GetStringProp(row.PropsJson, "selectedVariant"),
                    null,
                    assetUrlMap)
                : null,
            AltText = row.AltText,
            Caption = row.Caption,
            EmbedUrl = BuildEmbedUrl(blockType, row.ExternalUrl),
            HeadingLevel = string.Equals(blockType, "Heading", StringComparison.OrdinalIgnoreCase)
                ? GetStringProp(row.PropsJson, "headingLevel") ?? "H2"
                : null,
            QuoteAuthor = string.Equals(blockType, "Quote", StringComparison.OrdinalIgnoreCase)
                ? GetStringProp(row.PropsJson, "quoteAuthor")
                : null,
            DisplaySize = string.Equals(blockType, "Image", StringComparison.OrdinalIgnoreCase)
                ? GetStringProp(row.PropsJson, "displaySize")
                : null,
            FaqItems = faqItems
        };
    }

    private async Task<Dictionary<int, string>> ResolveAssetUrlsAsync(IEnumerable<int?> assetIds, CancellationToken cancellationToken)
    {
        var ids = assetIds.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        await using var partnersDb = await _partnersDbContextFactory.CreateDbContextAsync(cancellationToken);
        var assets = await partnersDb.MediaAssets
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.StorageKey })
            .ToListAsync(cancellationToken);

        return assets.ToDictionary(x => x.Id, x => BuildStorageUrl(x.StorageKey));
    }

    private string BuildStorageUrl(string storageKey)
    {
        if (!string.IsNullOrWhiteSpace(_storageOptions.AccountName) && !string.IsNullOrWhiteSpace(_storageOptions.Container))
        {
            return $"https://{_storageOptions.AccountName}.blob.core.windows.net/{_storageOptions.Container}/{storageKey}";
        }

        return storageKey;
    }

    private static string NormalizeSourceLanguage(string culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return "pl";
        }

        var trimmed = culture.Trim();
        var parts = trimmed.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return parts[0].ToLowerInvariant();
    }

    private static IReadOnlyList<string> DeserializeTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            var tags = JsonSerializer.Deserialize<List<string>>(tagsJson, JsonOptions);
            if (tags is null)
            {
                return Array.Empty<string>();
            }

            return tags
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static string? ResolveImageUrl(
        int? originalAssetId,
        int? webpAssetId,
        string? selectedVariant,
        string? fallbackUrl,
        IReadOnlyDictionary<int, string> assetUrlMap)
    {
        if (string.Equals(selectedVariant, "webp", StringComparison.OrdinalIgnoreCase)
            && webpAssetId.HasValue
            && assetUrlMap.TryGetValue(webpAssetId.Value, out var webpUrl))
        {
            return webpUrl;
        }

        if (originalAssetId.HasValue && assetUrlMap.TryGetValue(originalAssetId.Value, out var originalUrl))
        {
            return originalUrl;
        }

        return fallbackUrl;
    }

    private static string SanitizeHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var sanitized = Regex.Replace(html, "<script.*?</script>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        sanitized = Regex.Replace(sanitized, "<style.*?</style>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        sanitized = Regex.Replace(sanitized, "\\son\\w+\\s*=\\s*(['\"]).*?\\1", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        sanitized = Regex.Replace(sanitized, "\\s(href|src)\\s*=\\s*(['\"])javascript:.*?\\2", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return sanitized;
    }

    private static string? BuildEmbedUrl(string blockType, string? externalUrl)
    {
        if (string.IsNullOrWhiteSpace(externalUrl))
        {
            return null;
        }

        if (string.Equals(blockType, "YouTube", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeYoutubeUrl(externalUrl);
        }

        if (string.Equals(blockType, "Instagram", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeInstagramUrl(externalUrl);
        }

        return null;
    }

    private static string? NormalizeYoutubeUrl(string externalUrl)
    {
        var input = externalUrl.Trim();
        var iframeMatch = Regex.Match(input, "src\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
        if (iframeMatch.Success)
        {
            input = iframeMatch.Groups[1].Value;
        }

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            return null;
        }

        string? videoId = null;
        if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            videoId = uri.AbsolutePath.Trim('/').Split('/').FirstOrDefault();
        }
        else if (uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            if (uri.AbsolutePath.StartsWith("/watch", StringComparison.OrdinalIgnoreCase))
            {
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
                videoId = query.TryGetValue("v", out var value) ? value.ToString() : null;
            }
            else if (uri.AbsolutePath.StartsWith("/embed/", StringComparison.OrdinalIgnoreCase))
            {
                videoId = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault();
            }
        }

        return string.IsNullOrWhiteSpace(videoId)
            ? null
            : $"https://www.youtube-nocookie.com/embed/{videoId}";
    }

    private static string? NormalizeInstagramUrl(string externalUrl)
    {
        if (!Uri.TryCreate(externalUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!uri.Host.Contains("instagram.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return null;
        }

        if (!string.Equals(segments[0], "p", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(segments[0], "reel", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"https://www.instagram.com/{segments[0]}/{segments[1]}/embed";
    }

    private static string? GetStringProp(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<FaqItemDto> GetFaqItems(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<FaqItemDto>();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("faq", out var faqElement) || faqElement.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<FaqItemDto>();
            }

            if (!faqElement.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<FaqItemDto>();
            }

            var items = new List<FaqItemDto>();
            foreach (var itemElement in itemsElement.EnumerateArray())
            {
                if (itemElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var question = GetStringValue(itemElement, "question");
                var answer = GetStringValue(itemElement, "answer");

                if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
                {
                    continue;
                }

                items.Add(new FaqItemDto
                {
                    Question = question.Trim(),
                    Answer = SanitizeHtml(answer)
                });
            }

            return items;
        }
        catch (JsonException)
        {
            return Array.Empty<FaqItemDto>();
        }
        catch (InvalidOperationException)
        {
            return Array.Empty<FaqItemDto>();
        }
    }

    private static string? GetStringValue(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Null => null,
            _ => null
        };
    }

    private static int? GetIntProp(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.ValueKind switch
            {
                JsonValueKind.Number when property.TryGetInt32(out var numberValue) => numberValue,
                JsonValueKind.String when int.TryParse(property.GetString(), out var stringValue) => stringValue,
                JsonValueKind.Null => null,
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string EncodeCursor(BlogCursor cursor)
    {
        var json = JsonSerializer.Serialize(cursor, JsonOptions);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static BlogCursor? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var normalized = cursor.Replace('-', '+').Replace('_', '/');
            var padding = normalized.Length % 4;
            if (padding > 0)
            {
                normalized = normalized.PadRight(normalized.Length + (4 - padding), '=');
            }

            var bytes = Convert.FromBase64String(normalized);
            return JsonSerializer.Deserialize<BlogCursor>(bytes, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private sealed record BlogCursor(DateTime PublishedAtUtc, int Id);

    private sealed class FeedRow
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
        public string? TagsJson { get; init; }
        public DateTime PublishedAtUtc { get; init; }
        public int? HeroMediaAssetId { get; init; }
        public int? HeroWebpMediaAssetId { get; init; }
        public string? HeroSelectedVariant { get; init; }
        public string? HeroImageUrl { get; init; }
    }

    private sealed class PostRow
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
        public string? TagsJson { get; init; }
        public DateTime PublishedAtUtc { get; init; }
        public int? HeroMediaAssetId { get; init; }
        public int? HeroWebpMediaAssetId { get; init; }
        public string? HeroSelectedVariant { get; init; }
        public string? HeroImageUrl { get; init; }
        public int PublishedVersionNo { get; init; }
    }

    private sealed class PreviewPostRow
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
        public string? TagsJson { get; init; }
        public DateTime? PublishedAtUtc { get; init; }
        public int? HeroMediaAssetId { get; init; }
        public int? HeroWebpMediaAssetId { get; init; }
        public string? HeroSelectedVariant { get; init; }
        public string? HeroImageUrl { get; init; }
        public int CurrentDraftVersionNo { get; init; }
        public DateTime? PreviewExpiresAtUtc { get; init; }
    }

    private sealed class VersionRow
    {
        public int Id { get; init; }
        public int VersionNo { get; init; }
    }

    private sealed class BlockRow
    {
        public Guid BlockKey { get; init; }
        public int SortOrder { get; init; }
        public string BlockType { get; init; } = string.Empty;
        public string? TextContent { get; init; }
        public int? MediaAssetId { get; init; }
        public string? ExternalUrl { get; init; }
        public string? EmbedProvider { get; init; }
        public string? AltText { get; init; }
        public string? Caption { get; init; }
        public string? PropsJson { get; init; }
    }

    private static string ComputeSha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
    }
}
