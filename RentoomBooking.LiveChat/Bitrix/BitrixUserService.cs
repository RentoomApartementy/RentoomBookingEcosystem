using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace RentoomBooking.Api.LiveChat.Bitrix;

public sealed class BitrixUserService : IBitrixUserService
{
    private readonly HttpClient _httpClient;
    private readonly IBitrixOAuthService _oauthService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<BitrixUserService> _logger;

    private static readonly TimeSpan OperatorInfoCacheTtl = TimeSpan.FromHours(2);

    public BitrixUserService(
        HttpClient httpClient,
        IBitrixOAuthService oauthService,
        IMemoryCache cache,
        ILogger<BitrixUserService> logger)
    {
        _httpClient = httpClient;
        _oauthService = oauthService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<(string? FirstName, string? AvatarUrl)> FetchOperatorInfoAsync(string bitrixUserId, CancellationToken ct)
    {
        BitrixRestConnection connection;
        try
        {
            connection = await _oauthService.GetConnectionAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot get REST connection to fetch operator info for user {UserId}", bitrixUserId);
            return (null, null);
        }

        var cacheKey = $"bitrix-operator-info:{connection.ClientEndpoint}:{bitrixUserId}";
        if (_cache.TryGetValue<(string? FirstName, string? AvatarUrl)>(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            // user.get requires "user" scope; im.user.get requires "im" scope which is typically unavailable.
            var url = BitrixRestHelpers.BuildRestMethodUrl(connection.ClientEndpoint, "user.get", connection.AccessToken)
                + $"&ID={Uri.EscapeDataString(bitrixUserId)}";

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var response = await _httpClient.GetAsync(url, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("user.get returned HTTP {Status} for user {UserId}", (int)response.StatusCode, bitrixUserId);
                return (null, null);
            }

            var body = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var errorProp))
            {
                _logger.LogWarning("user.get error for user {UserId}: {Error}", bitrixUserId, errorProp.GetString());
                return (null, null);
            }

            if (!root.TryGetProperty("result", out var resultProp))
            {
                return (null, null);
            }

            // user.get always returns an array
            var userElement = resultProp.ValueKind == JsonValueKind.Array
                ? (resultProp.GetArrayLength() > 0 ? resultProp[0] : (JsonElement?)null)
                : (JsonElement?)resultProp;

            if (userElement is null)
            {
                return (null, null);
            }

            // user.get returns NAME (first name) and PERSONAL_PHOTO (avatar URL)
            var firstName = userElement.Value.TryGetProperty("NAME", out var fnProp)
                ? fnProp.GetString()?.Trim()
                : null;

            var avatar = userElement.Value.TryGetProperty("PERSONAL_PHOTO", out var avProp)
                ? avProp.GetString()?.Trim()
                : null;

            if (string.IsNullOrEmpty(avatar))
            {
                avatar = null;
            }

            var info = (string.IsNullOrEmpty(firstName) ? null : firstName, avatar);
            _cache.Set(cacheKey, info, OperatorInfoCacheTtl);
            return info;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch operator info for user {UserId}", bitrixUserId);
            return (null, null);
        }
    }
}
