using System.Text.RegularExpressions;

namespace RentoomBooking.SharedClasses.Services.Embeds;

internal static class EmbedHtmlUtils
{
    public static string StripScriptTags(string html)
    {
        return Regex.Replace(html, @"<script\b[^>]*>[\s\S]*?</script\s*>", string.Empty, RegexOptions.IgnoreCase);
    }

    public static string? ExtractAttributeValue(string html, string attributeName)
    {
        var match = Regex.Match(html, $@"{Regex.Escape(attributeName)}\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    public static int? ExtractIntAttribute(string html, string attributeName)
    {
        var raw = ExtractAttributeValue(html, attributeName);
        return int.TryParse(raw, out var value) && value > 0 ? value : null;
    }
}
