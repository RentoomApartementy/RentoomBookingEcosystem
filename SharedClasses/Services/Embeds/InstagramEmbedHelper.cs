using System.Text.RegularExpressions;

namespace RentoomBooking.SharedClasses.Services.Embeds;

public static class InstagramEmbedHelper
{
    public static string? BuildEmbedHtml(string? embedCode)
    {
        if (!string.IsNullOrWhiteSpace(embedCode) && embedCode.Contains("instagram-media", StringComparison.OrdinalIgnoreCase))
        {
            return EmbedHtmlUtils.StripScriptTags(embedCode).Trim();
        }

        var permalink = ExtractPermalink(embedCode);

        if (string.IsNullOrWhiteSpace(permalink))
        {
            return null;
        }

        return $"<blockquote class=\"instagram-media\" data-instgrm-permalink=\"{permalink}\" " +
               "data-instgrm-version=\"14\"></blockquote>";
    }

    private static string? ExtractPermalink(string? embedCode)
    {
        if (string.IsNullOrWhiteSpace(embedCode))
        {
            return null;
        }

        var permalink = EmbedHtmlUtils.ExtractAttributeValue(embedCode, "data-instgrm-permalink");
        if (!string.IsNullOrWhiteSpace(permalink))
        {
            return NormalizePermalink(permalink);
        }

        var hrefMatch = Regex.Match(
            embedCode,
            @"href\s*=\s*[""'](https?://(?:www\.)?instagram\.com/[^""']+)[""']",
            RegexOptions.IgnoreCase);

        return hrefMatch.Success
            ? NormalizePermalink(hrefMatch.Groups[1].Value)
            : null;
    }

    private static string? NormalizePermalink(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl) || !Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!uri.Host.Contains("instagram.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        return $"https://www.instagram.com{path}/";
    }
}
