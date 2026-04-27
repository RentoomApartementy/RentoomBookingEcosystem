using System.Net;
using System.Text.RegularExpressions;

namespace RentoomBooking.StayWell.Utils;

/// <summary>
/// Converts Bitrix BB code markup to sanitised HTML for display in the live chat panel.
/// All text content is HTML-encoded before transformation (no XSS possible through text fragments).
/// URLs are validated to only allow http/https/ftp schemes.
/// </summary>
public static partial class BbCodeParser
{
    private const int MaxInputLength = 32_000;

    // ── inline SVG icons (no external dependencies) ─────────────────────────
    private const string LinkSvg = """<svg xmlns="http://www.w3.org/2000/svg" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/></svg>""";
    private const string CallSvg = """<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07A19.5 19.5 0 0 1 4.69 12a19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 3.6 1h3a2 2 0 0 1 2 1.72c.127.96.361 1.903.7 2.81a2 2 0 0 1-.45 2.11L7.91 8.96a16 16 0 0 0 6.29 6.29l1.42-1.42a2 2 0 0 1 2.11-.45c.907.339 1.85.573 2.81.7A2 2 0 0 1 22 16.92z"/></svg>""";
    private const string ClockSvg = """<svg xmlns="http://www.w3.org/2000/svg" width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>""";

    // ── protected-block extraction ───────────────────────────────────────────
    [GeneratedRegex(@"\[code\]([\s\S]*?)\[/code\]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 500)]
    private static partial Regex CodeRx();

    [GeneratedRegex(@"(?m)^-{6,}[ \t]*\r?\n([\s\S]*?)\r?\n-{6,}[ \t]*$", RegexOptions.None, matchTimeoutMilliseconds: 500)]
    private static partial Regex FullQuoteRx();

    // ── text-formatting tags ─────────────────────────────────────────────────
    [GeneratedRegex(@"\[b\]([\s\S]*?)\[/b\]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 500)]
    private static partial Regex BoldRx();

    [GeneratedRegex(@"\[i\]([\s\S]*?)\[/i\]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 500)]
    private static partial Regex ItalicRx();

    [GeneratedRegex(@"\[u\]([\s\S]*?)\[/u\]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 500)]
    private static partial Regex UnderlineRx();

    [GeneratedRegex(@"\[s\]([\s\S]*?)\[/s\]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 500)]
    private static partial Regex StrikeRx();

    [GeneratedRegex(@"\[size=(\d{1,3})\]([\s\S]*?)\[/size\]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 500)]
    private static partial Regex SizeRx();

    [GeneratedRegex(@"\[color=(#[0-9a-fA-F]{3,6}|[a-zA-Z]{1,20})\]([\s\S]*?)\[/color\]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 500)]
    private static partial Regex ColorRx();

    // ── links (named first — more specific) ─────────────────────────────────
    [GeneratedRegex(@"\[url=([^\]]{1,2048})\]([\s\S]*?)\[/url\]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 500)]
    private static partial Regex UrlNamedRx();

    [GeneratedRegex(@"\[url\]([\s\S]*?)\[/url\]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 500)]
    private static partial Regex UrlRx();

    // ── call / phone ─────────────────────────────────────────────────────────
    [GeneratedRegex(@"\[call\]([\s\S]*?)\[/call\]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 500)]
    private static partial Regex CallRx();

    // ── timestamp ────────────────────────────────────────────────────────────
    [GeneratedRegex(@"\[timestamp=(\d{1,15})(?:\s+format=([A-Z_]{1,30}))?\]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 500)]
    private static partial Regex TimestampRx();

    // ── mentions / channels / context ────────────────────────────────────────
    [GeneratedRegex(@"\[user=([a-zA-Z0-9]{1,64})\]([\s\S]*?)\[/user\]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 500)]
    private static partial Regex UserRx();

    [GeneratedRegex(@"\[chat=([^\]]{1,128})\]([\s\S]*?)\[/chat\]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 500)]
    private static partial Regex ChatRx();

    [GeneratedRegex(@"\[context=[^\]]{0,256}\]([\s\S]*?)\[/context\]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 500)]
    private static partial Regex ContextRx();

    // ── plain URL auto-linking (no [url] tag) ────────────────────────────────
    [GeneratedRegex(@"https?://[^\s\[\]]+", RegexOptions.IgnoreCase)]
    private static partial Regex PlainUrlRx();

    // ── quote lines: after HtmlEncode, ">>" becomes "&gt;&gt;" ──────────────
    [GeneratedRegex(@"(?m)^&gt;&gt;(.*)$")]
    private static partial Regex QuoteLineRx();

    // merge consecutive quote blockquotes separated only by a newline
    [GeneratedRegex(@"</blockquote>\r?\n<blockquote class=""bb-quote"">")]
    private static partial Regex QuoteMergeRx();

    private static readonly char[] UrlTrailingPunctuation = ['.', ',', '!', '?', ';', ':'];

    public static string ToHtml(string? input, IReadOnlySet<string>? skipLinkCardUrls = null)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        if (input.Length > MaxInputLength)
            input = input[..MaxInputLength];

        try
        {
            return ToHtmlCore(input, skipLinkCardUrls);
        }
        catch (RegexMatchTimeoutException)
        {
            // Pathological input triggered a regex timeout — return plain HTML-encoded text as a safe fallback.
            return WebUtility.HtmlEncode(input);
        }
    }

    private static string ToHtmlCore(string input, IReadOnlySet<string>? skipLinkCardUrls)
    {
        // GUID-based placeholders — unpredictable, cannot be injected by crafted input.
        var blocks = new Dictionary<string, string>();

        // ── Step 1: extract [code] blocks ────────────────────────────────────
        input = CodeRx().Replace(input, m =>
        {
            var key = MakeKey();
            blocks[key] = $"<pre class=\"bb-code\"><code>{WebUtility.HtmlEncode(m.Groups[1].Value)}</code></pre>";
            return key;
        });

        // ── Step 2: extract full-quote blocks (------…------) ────────────────
        input = FullQuoteRx().Replace(input, m =>
        {
            var key = MakeKey();
            blocks[key] = $"<blockquote class=\"bb-quote-full\">{WebUtility.HtmlEncode(m.Groups[1].Value.Trim())}</blockquote>";
            return key;
        });

        // ── Step 3: HTML-encode remaining text ───────────────────────────────
        // Neutralises < > & " in user text; [ ] are not HTML-special so BB codes survive.
        input = WebUtility.HtmlEncode(input);

        // ── Step 4: text formatting ───────────────────────────────────────────
        input = BoldRx().Replace(input,      "<strong>$1</strong>");
        input = ItalicRx().Replace(input,    "<em>$1</em>");
        input = UnderlineRx().Replace(input, "<u>$1</u>");
        input = StrikeRx().Replace(input,    "<s>$1</s>");

        input = SizeRx().Replace(input, m =>
        {
            if (!int.TryParse(m.Groups[1].Value, out var size)) return m.Groups[2].Value;
            size = Math.Clamp(size, 8, 48);
            return $"<span style=\"font-size:{size}px\">{m.Groups[2].Value}</span>";
        });

        input = ColorRx().Replace(input, "<span style=\"color:$1\">$2</span>");

        // ── Step 5: link cards (protected to survive newline replacement) ─────
        // URLs may contain & which gets HtmlEncoded in step 3; decode before Uri.TryCreate.
        input = UrlNamedRx().Replace(input, m =>
        {
            var url  = WebUtility.HtmlDecode(m.Groups[1].Value);
            var text = m.Groups[2].Value; // already encoded
            if (!IsSafeUrl(url)) return text;
            if (skipLinkCardUrls?.Contains(url) == true) return string.Empty;
            var key = MakeKey();
            blocks[key] = BuildLinkCard(url, text);
            return key;
        });

        input = UrlRx().Replace(input, m =>
        {
            var url = WebUtility.HtmlDecode(m.Groups[1].Value);
            if (!IsSafeUrl(url)) return m.Groups[1].Value;
            if (skipLinkCardUrls?.Contains(url) == true) return string.Empty;
            var key = MakeKey();
            blocks[key] = BuildLinkCard(url, m.Groups[1].Value); // display = encoded url
            return key;
        });

        // ── Step 6: [call] buttons (protected) ───────────────────────────────
        input = CallRx().Replace(input, m =>
        {
            var display   = m.Groups[1].Value.Trim(); // already encoded
            var telNumber = Regex.Replace(WebUtility.HtmlDecode(display), @"[^\+\d]", "");
            if (telNumber.Length < 5) return display;
            var key = MakeKey();
            blocks[key] = $"<a href=\"tel:{telNumber}\" class=\"bb-call\">{CallSvg}<span>{display}</span></a>";
            return key;
        });

        // ── Step 7: [timestamp] (protected) ──────────────────────────────────
        input = TimestampRx().Replace(input, m =>
        {
            var formatted = FormatTimestamp(m.Groups[1].Value, m.Groups[2].Success ? m.Groups[2].Value : null);
            if (string.IsNullOrEmpty(formatted)) return string.Empty;
            var key = MakeKey();
            blocks[key] = formatted;
            return key;
        });

        // ── Step 8: mentions / channels / context ────────────────────────────
        input = UserRx().Replace(input,    "<span class=\"bb-mention\">@$2</span>");
        input = ChatRx().Replace(input,    "<span class=\"bb-channel\">#$2</span>");
        input = ContextRx().Replace(input, "$1");

        // ── Step 8b: plain URL auto-linking (not wrapped in [url] tags) ──────
        // At this point any [url]-wrapped links are already protected blocks.
        // Remaining https?:// occurrences are bare URLs typed in plain text.
        input = PlainUrlRx().Replace(input, m =>
        {
            var raw = m.Value;
            // Strip trailing punctuation unlikely to be part of the URL
            var stripped = raw.TrimEnd(UrlTrailingPunctuation);
            var suffix = raw[stripped.Length..];
            var decodedUrl = WebUtility.HtmlDecode(stripped);
            if (!IsSafeUrl(decodedUrl)) return raw;
            if (skipLinkCardUrls?.Contains(decodedUrl) == true) return suffix;
            var key = MakeKey();
            blocks[key] = BuildLinkCard(decodedUrl, stripped);
            return key + suffix;
        });

        // ── Step 9: >> quote lines → merge consecutive into one blockquote ────
        input = QuoteLineRx().Replace(input, m =>
            $"<blockquote class=\"bb-quote\">{m.Groups[1].Value.Trim()}</blockquote>");
        input = QuoteMergeRx().Replace(input, "<br>");

        // ── Step 10: line breaks ──────────────────────────────────────────────
        input = input
            .Replace("[br]", "<br>")
            .Replace("\r\n", "<br>")
            .Replace("\n", "<br>");

        // ── Step 11: restore protected blocks ────────────────────────────────
        foreach (var (key, value) in blocks)
            input = input.Replace(key, value);

        return input;
    }

    private static string BuildLinkCard(string rawUrl, string alreadyEncodedText)
    {
        Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri);
        var host   = WebUtility.HtmlEncode(uri?.Host ?? string.Empty);
        var safeUrl = WebUtility.HtmlEncode(rawUrl);

        return $"""<a href="{safeUrl}" class="bb-link-card" target="_blank" rel="noopener noreferrer"><span class="bb-link-card-icon">{LinkSvg}</span><span class="bb-link-card-body"><span class="bb-link-card-title">{alreadyEncodedText}</span><span class="bb-link-card-host">{host}</span></span><span class="bb-link-card-arrow">↗</span></a>""";
    }

    private static string FormatTimestamp(string unixStr, string? format)
    {
        if (!long.TryParse(unixStr, out var unix)) return string.Empty;

        try
        {
            var dt = DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;
            var text = format switch
            {
                "SHORT_TIME_FORMAT"     => dt.ToString("HH:mm"),
                "SHORT_DATE_FORMAT"     => dt.ToString("d MMM"),
                "FULL_DATE_FORMAT"      => dt.ToString("d MMMM yyyy"),
                "FULL_DATE_TIME_FORMAT" => dt.ToString("d MMM yyyy, HH:mm"),
                _                       => dt.ToString("d MMM yyyy, HH:mm"),
            };
            var iso = new DateTimeOffset(dt, TimeZoneInfo.Local.GetUtcOffset(dt)).ToString("O");
            return $"<time class=\"bb-timestamp\" datetime=\"{WebUtility.HtmlEncode(iso)}\">{ClockSvg}<span>{WebUtility.HtmlEncode(text)}</span></time>";
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Returns the first safe http(s) URL found in raw BB-code content, or null.</summary>
    public static string? ExtractFirstUrl(string? input)
    {
        if (string.IsNullOrEmpty(input)) return null;

        var m = UrlNamedRx().Match(input);
        if (m.Success) { var u = m.Groups[1].Value; if (IsSafeUrl(u)) return u; }

        m = UrlRx().Match(input);
        if (m.Success) { var u = m.Groups[1].Value; if (IsSafeUrl(u)) return u; }

        m = PlainUrlRx().Match(input);
        if (m.Success) { var u = m.Value.TrimEnd(UrlTrailingPunctuation); if (IsSafeUrl(u)) return u; }

        return null;
    }

    private static bool IsSafeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > 2048) return false;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https" or "ftp";
    }

    private static string MakeKey() => $"\x02{Guid.NewGuid():N}\x02";
}
