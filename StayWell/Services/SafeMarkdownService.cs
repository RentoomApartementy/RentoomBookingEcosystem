using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace RentoomBooking.StayWell.Services;

public sealed class SafeMarkdownService
{
    private static readonly Regex BoldRegex = new("\\*\\*(.+?)\\*\\*", RegexOptions.Compiled);
    private static readonly Regex ItalicRegex = new("(?<!\\*)\\*(?!\\*)(.+?)(?<!\\*)\\*(?!\\*)", RegexOptions.Compiled);
    private static readonly Regex CodeRegex = new("`(.+?)`", RegexOptions.Compiled);
    private static readonly Regex OrderedListRegex = new("^\\d+\\.\\s+", RegexOptions.Compiled);
    private static readonly Regex InlineOrderedMarkerRegex = new("(?<!\\n)(\\d+\\.\\s*)", RegexOptions.Compiled);

    public string ToSafeHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var normalizedMarkdown = NormalizeLayout(markdown);

        var encoded = WebUtility.HtmlEncode(normalizedMarkdown)
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");

        var lines = encoded.Split('\n');
        var sb = new StringBuilder();
        var inUnorderedList = false;
        var inOrderedList = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                if (inOrderedList)
                {
                    sb.Append("</ol>");
                    inOrderedList = false;
                }

                if (!inUnorderedList)
                {
                    sb.Append("<ul>");
                    inUnorderedList = true;
                }

                sb.Append("<li>")
                  .Append(ApplyInlineMarkdown(line[2..].Trim()))
                  .Append("</li>");
                continue;
            }

            if (OrderedListRegex.IsMatch(line))
            {
                if (inUnorderedList)
                {
                    sb.Append("</ul>");
                    inUnorderedList = false;
                }

                if (!inOrderedList)
                {
                    sb.Append("<ol>");
                    inOrderedList = true;
                }

                var listItem = OrderedListRegex.Replace(line, string.Empty).Trim();
                sb.Append("<li>")
                  .Append(ApplyInlineMarkdown(listItem))
                  .Append("</li>");
                continue;
            }

            if (inUnorderedList)
            {
                sb.Append("</ul>");
                inUnorderedList = false;
            }

            if (inOrderedList)
            {
                sb.Append("</ol>");
                inOrderedList = false;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                sb.Append("<br />");
                continue;
            }

            sb.Append("<p>")
              .Append(ApplyInlineMarkdown(line.Trim()))
              .Append("</p>");
        }

        if (inUnorderedList)
        {
            sb.Append("</ul>");
        }

        if (inOrderedList)
        {
            sb.Append("</ol>");
        }

        return sb.ToString();
    }

    private static string NormalizeLayout(string markdown)
    {
        var normalized = markdown;
        var markerMatches = InlineOrderedMarkerRegex.Matches(normalized);
        if (markerMatches.Count >= 2)
        {
            var firstMarkerKept = false;
            normalized = InlineOrderedMarkerRegex.Replace(normalized, match =>
            {
                if (!firstMarkerKept)
                {
                    firstMarkerKept = true;
                    return match.Value;
                }

                return "\n" + match.Groups[1].Value;
            });
        }

        return normalized;
    }

    private static string ApplyInlineMarkdown(string text)
    {
        var transformed = CodeRegex.Replace(text, "<code>$1</code>");
        transformed = BoldRegex.Replace(transformed, "<strong>$1</strong>");
        transformed = ItalicRegex.Replace(transformed, "<em>$1</em>");

        return transformed;
    }
}
