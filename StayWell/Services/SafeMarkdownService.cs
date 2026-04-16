using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace RentoomBooking.StayWell.Services;

public sealed class SafeMarkdownService
{
    private static readonly Regex BoldRegex = new("\\*\\*(.+?)\\*\\*", RegexOptions.Compiled);
    private static readonly Regex ItalicRegex = new("(?<!\\*)\\*(?!\\*)(.+?)(?<!\\*)\\*(?!\\*)", RegexOptions.Compiled);
    private static readonly Regex CodeRegex = new("`(.+?)`", RegexOptions.Compiled);

    public string ToSafeHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var encoded = WebUtility.HtmlEncode(markdown)
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");

        var lines = encoded.Split('\n');
        var sb = new StringBuilder();
        var inList = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                if (!inList)
                {
                    sb.Append("<ul>");
                    inList = true;
                }

                sb.Append("<li>")
                  .Append(ApplyInlineMarkdown(line[2..].Trim()))
                  .Append("</li>");
                continue;
            }

            if (inList)
            {
                sb.Append("</ul>");
                inList = false;
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

        if (inList)
        {
            sb.Append("</ul>");
        }

        return sb.ToString();
    }

    private static string ApplyInlineMarkdown(string text)
    {
        var transformed = CodeRegex.Replace(text, "<code>$1</code>");
        transformed = BoldRegex.Replace(transformed, "<strong>$1</strong>");
        transformed = ItalicRegex.Replace(transformed, "<em>$1</em>");

        return transformed;
    }
}
