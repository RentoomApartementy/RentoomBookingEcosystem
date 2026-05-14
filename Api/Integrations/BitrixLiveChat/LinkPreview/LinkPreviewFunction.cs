using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Caching.Memory;
using RentoomBooking.LiveChat;
using RentoomBooking.SharedClasses.LiveChat;

namespace RentoomBooking.Api.Integrations.BitrixLiveChat.LinkPreview;

public sealed class LinkPreviewFunction
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly BitrixLiveChatService _bitrixService;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private const int MaxBodyBytes = 256 * 1024;
    private const string CacheKeyPrefix = "og_preview:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);
    
    private static readonly Regex OgTitleRx    = new(@"<meta[^>]+property\s*=\s*[""']og:title[""'][^>]+content\s*=\s*[""']([^""'<>]{1,300})[""']",     RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OgTitleRx2   = new(@"<meta[^>]+content\s*=\s*[""']([^""'<>]{1,300})[""'][^>]+property\s*=\s*[""']og:title[""']",      RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OgDescRx     = new(@"<meta[^>]+property\s*=\s*[""']og:description[""'][^>]+content\s*=\s*[""']([^""'<>]{1,500})[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OgDescRx2    = new(@"<meta[^>]+content\s*=\s*[""']([^""'<>]{1,500})[""'][^>]+property\s*=\s*[""']og:description[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OgImageRx    = new(@"<meta[^>]+property\s*=\s*[""']og:image[""'][^>]+content\s*=\s*[""']([^""'<>]{1,2048})[""']",      RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OgImageRx2   = new(@"<meta[^>]+content\s*=\s*[""']([^""'<>]{1,2048})[""'][^>]+property\s*=\s*[""']og:image[""']",      RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HtmlTitleRx  = new(@"<title[^>]*>([^<]{1,300})</title>",                                                                RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public LinkPreviewFunction(IHttpClientFactory httpClientFactory, IMemoryCache cache, BitrixLiveChatService bitrixService)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _bitrixService = bitrixService;
    }

    [Function("LinkPreview")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "staywell/link-preview")] HttpRequestData req,
        CancellationToken ct)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var urlParam = query["url"];

        if (string.IsNullOrWhiteSpace(urlParam) || urlParam.Length > 2048)
            return req.CreateResponse(HttpStatusCode.BadRequest);

        if (!Uri.TryCreate(urlParam, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return req.CreateResponse(HttpStatusCode.BadRequest);

        if (await IsPrivateHostAsync(uri.Host))
            return req.CreateResponse(HttpStatusCode.BadRequest);

        var cacheKey = CacheKeyPrefix + urlParam;
        if (_cache.TryGetValue(cacheKey, out LinkPreviewDto? cached))
            return await JsonResponseAsync(req, cached!);

        var preview = await FetchPreviewAsync(urlParam, uri, ct);

        _cache.Set(cacheKey, preview, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl
        });

        return await JsonResponseAsync(req, preview);
    }

    private async Task<HttpResponseData> JsonResponseAsync(HttpRequestData req, LinkPreviewDto dto)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(dto, _jsonOptions));
        return response;
    }

    private async Task<LinkPreviewDto> FetchPreviewAsync(string originalUrl, Uri uri, CancellationToken ct)
    {
        // Dla stron bitrixa (z api)
        var path = uri.AbsolutePath.TrimStart('/');
        if (path.StartsWith('~'))
        {
            var bitrixImageUrl = await _bitrixService.GetLandingPreviewUrlAsync(originalUrl, ct);
            if (!string.IsNullOrEmpty(bitrixImageUrl))
            {
                return new LinkPreviewDto(
                    Url:         originalUrl,
                    Title:       null,
                    Description: null,
                    ImageUrl:    bitrixImageUrl,
                    Host:        uri.Host);
            }
        }

        // (ogólne rozwiązanie)
        try
        {
            using var http = _httpClientFactory.CreateClient("LinkPreview");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(8));

            using var request = new HttpRequestMessage(HttpMethod.Get, originalUrl);
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml");

            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            if (!response.IsSuccessStatusCode)
                return EmptyPreview(originalUrl, uri.Host);

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                return EmptyPreview(originalUrl, uri.Host);

            var finalUri = response.RequestMessage?.RequestUri ?? uri;

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            var buf = new byte[8192];
            using var ms = new MemoryStream(MaxBodyBytes);
            int read, total = 0;
            while ((read = await stream.ReadAsync(buf, cts.Token)) > 0)
            {
                ms.Write(buf, 0, read);
                total += read;
                if (total >= MaxBodyBytes) break;
            }

            var html = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
            return ParseOgTags(html, originalUrl, finalUri);
        }
        catch
        {
            return EmptyPreview(originalUrl, uri.Host);
        }
    }

    private static LinkPreviewDto ParseOgTags(string html, string originalUrl, Uri finalUri)
    {
        string Extract(Regex rx, Regex rx2)
        {
            var m = rx.Match(html);
            if (m.Success) return m.Groups[1].Value.Trim();
            m = rx2.Match(html);
            return m.Success ? m.Groups[1].Value.Trim() : string.Empty;
        }

        var ogTitle = Extract(OgTitleRx, OgTitleRx2);
        if (string.IsNullOrEmpty(ogTitle))
            ogTitle = HtmlTitleRx.Match(html) is { Success: true } tm ? tm.Groups[1].Value.Trim() : string.Empty;

        var ogDesc  = Extract(OgDescRx,  OgDescRx2);
        var ogImage = Extract(OgImageRx, OgImageRx2);

        // Resolve relative image URLs against final response URL
        if (!string.IsNullOrEmpty(ogImage) && !ogImage.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(finalUri, ogImage, out var abs))
                ogImage = abs.ToString();
            else
                ogImage = string.Empty;
        }

        return new LinkPreviewDto(
            Url:         originalUrl,
            Title:       string.IsNullOrEmpty(ogTitle) ? null : WebUtility.HtmlDecode(ogTitle),
            Description: string.IsNullOrEmpty(ogDesc)  ? null : WebUtility.HtmlDecode(ogDesc),
            ImageUrl:    string.IsNullOrEmpty(ogImage) ? null : ogImage,
            Host:        finalUri.Host
        );
    }

    private static LinkPreviewDto EmptyPreview(string url, string host) =>
        new(url, null, null, null, host);

    private static async Task<bool> IsPrivateHostAsync(string host)
    {
        if (string.IsNullOrEmpty(host)) return true;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        if (host.StartsWith("169.254.", StringComparison.Ordinal)) return true;

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host);
            return addresses.Any(IsPrivateIp);
        }
        catch
        {
            return true; // DNS failure → reject
        }
    }

    private static bool IsPrivateIp(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal;

        var b = ip.GetAddressBytes();
        return b[0] == 10
            || b[0] == 172 && b[1] >= 16 && b[1] <= 31
            || b[0] == 192 && b[1] == 168
            || b[0] == 169 && b[1] == 254;
    }
}
