using Microsoft.AspNetCore.WebUtilities;

namespace RentoomBooking.SharedClasses.Services.Embeds;

public static class YoutubeEmbedHelper
{
    public static string? BuildEmbedUrl(
        string? embedCode,
        bool? autoplay,
        bool? mute,
        bool? controls,
        bool? modestBranding,
        bool? loop)
    {
        var source = ParseSource(embedCode);
        if (source is null)
        {
            return null;
        }

        var queryParams = new List<string>();

        if (autoplay == true)
        {
            queryParams.Add("autoplay=1");
        }

        if (mute == true)
        {
            queryParams.Add("mute=1");
        }

        if (controls == false)
        {
            queryParams.Add("controls=0");
        }

        if (modestBranding == true)
        {
            queryParams.Add("modestbranding=1");
        }

        if (loop == true)
        {
            queryParams.Add("loop=1");
            queryParams.Add($"playlist={Uri.EscapeDataString(source.VideoId)}");
        }

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
        return $"https://www.youtube.com/embed/{source.VideoId}{query}";
    }

    public static (int? Width, int? Height) ResolveDimensions(int? configuredWidth, int? configuredHeight, string? embedCode)
    {
        if (configuredWidth is > 0 && configuredHeight is > 0)
        {
            return (configuredWidth, configuredHeight);
        }

        var source = ParseSource(embedCode);
        return (source?.Width, source?.Height);
    }

    public static string ResolveSizeClass(string? displaySize)
    {
        return $"embed-youtube-size-{NormalizeDisplaySize(displaySize)}";
    }

    public static string ResolveAspectRatioStyle(int? width, int? height)
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

    private static YoutubeSource? ParseSource(string? embedCode)
    {
        if (string.IsNullOrWhiteSpace(embedCode))
        {
            return null;
        }

        var src = EmbedHtmlUtils.ExtractAttributeValue(embedCode, "src");
        if (string.IsNullOrWhiteSpace(src) || !Uri.TryCreate(src.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        var videoId = TryExtractVideoId(uri);
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return null;
        }

        return new YoutubeSource(
            videoId,
            EmbedHtmlUtils.ExtractIntAttribute(embedCode, "width"),
            EmbedHtmlUtils.ExtractIntAttribute(embedCode, "height"));
    }

    private static string? TryExtractVideoId(Uri uri)
    {
        if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            return uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }

        if (!uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (uri.AbsolutePath.StartsWith("/watch", StringComparison.OrdinalIgnoreCase))
        {
            var query = QueryHelpers.ParseQuery(uri.Query);
            return query.TryGetValue("v", out var value) ? value.ToString() : null;
        }

        if (uri.AbsolutePath.StartsWith("/embed/", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.StartsWith("/shorts/", StringComparison.OrdinalIgnoreCase))
        {
            return uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault();
        }

        return null;
    }

    private sealed record YoutubeSource(string VideoId, int? Width, int? Height);
}
