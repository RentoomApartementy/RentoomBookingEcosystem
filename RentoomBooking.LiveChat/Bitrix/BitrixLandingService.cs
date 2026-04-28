using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RentoomBooking.LiveChat.Repositories;

namespace RentoomBooking.LiveChat.Bitrix;

public sealed class BitrixLandingService : IBitrixLandingService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<BitrixLandingService> _logger;
    private readonly IBitrixOAuthService _oauthService;
    private readonly IBitrixPortalRepository _portalRepo;

    public BitrixLandingService(
        HttpClient httpClient,
        IBitrixOAuthService oauthService,
        IBitrixPortalRepository portalRepo,
        ILogger<BitrixLandingService> logger)
    {
        _httpClient = httpClient;
        _oauthService = oauthService;
        _portalRepo = portalRepo;
        _logger = logger;
    }

    public async Task<string?> GetLandingPreviewUrlAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;

        var pathSegment = uri.AbsolutePath.TrimStart('/');
        if (!pathSegment.StartsWith('~') || pathSegment.Length < 2) return null;
        var siteCode = pathSegment[1..].Split('/')[0];

        var urlHost = uri.Host;
        var portals = await _portalRepo.GetAllAsync(ct: ct);

        var portal = portals.FirstOrDefault(p =>
            string.Equals(BitrixRestHelpers.NormalizeDomain(p.ClientEndpoint), urlHost,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Domain, urlHost, StringComparison.OrdinalIgnoreCase));

        if (portal is null && portals.Count == 0) return null;

        try
        {
            BitrixRestConnection connection;
            if (portal is not null)
                connection = await _oauthService.GetPortalConnectionAsync(portal, ct);
            else
                connection = await _oauthService.GetConnectionAsync(ct);

            var listUrl = BitrixRestHelpers.BuildRestMethodUrl(connection.ClientEndpoint, "landing.site.getList",
                connection.AccessToken);
            var listPayload = new { filter = new { CODE = siteCode }, select = new[] { "ID", "CODE" } };
            using var listContent = new StringContent(
                JsonSerializer.Serialize(listPayload, _jsonOptions),
                Encoding.UTF8, "application/json");

            var listResponse = await _httpClient.PostAsync(listUrl, listContent, ct);
            var listJson = await listResponse.Content.ReadAsStringAsync(ct);
            using var listDoc = JsonDocument.Parse(listJson);

            if (!listDoc.RootElement.TryGetProperty("result", out var resultEl)) return null;

            JsonElement? itemsEl = resultEl.ValueKind == JsonValueKind.Array
                ? resultEl
                : resultEl.TryGetProperty("items", out var items)
                    ? items
                    : null;

            if (itemsEl is null || itemsEl.Value.GetArrayLength() == 0) return null;

            var first = itemsEl.Value[0];
            if (!first.TryGetProperty("ID", out var idProp)) return null;
            var siteId = idProp.ValueKind == JsonValueKind.Number
                ? idProp.GetInt32()
                : int.Parse(idProp.GetString() ?? "0");

            if (siteId == 0) return null;

            var previewUrl = BitrixRestHelpers.BuildRestMethodUrl(connection.ClientEndpoint, "landing.site.getPreview",
                connection.AccessToken);
            using var previewContent = new StringContent(
                JsonSerializer.Serialize(new { id = siteId }, _jsonOptions),
                Encoding.UTF8, "application/json");

            var previewResponse = await _httpClient.PostAsync(previewUrl, previewContent, ct);
            var previewJson = await previewResponse.Content.ReadAsStringAsync(ct);
            using var previewDoc = JsonDocument.Parse(previewJson);

            if (!previewDoc.RootElement.TryGetProperty("result", out var previewResult)) return null;
            var previewPath = previewResult.GetString();
            if (string.IsNullOrEmpty(previewPath)) return null;

            if (previewPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return previewPath;

            var portalUri = new Uri(BitrixRestHelpers.NormalizeClientEndpoint(connection.ClientEndpoint, null));
            return new Uri(portalUri, previewPath).ToString();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "landing.site.getPreview failed for URL {Url}", url);
            return null;
        }
    }
}