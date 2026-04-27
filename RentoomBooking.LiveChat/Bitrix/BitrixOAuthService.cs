using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RentoomBooking.Api.LiveChat.Data;
using RentoomBooking.Api.LiveChat.Entities;
using RentoomBooking.SharedClasses.Configuration;

namespace RentoomBooking.Api.LiveChat.Bitrix;

public sealed class BitrixOAuthService : IBitrixOAuthService
{
    private readonly HttpClient _httpClient;
    private readonly string _configuredClientEndpoint;
    private readonly string _oauthClientId;
    private readonly string _oauthClientSecret;
    private readonly string _configuredRefreshToken;
    private readonly IDbContextFactory<LiveChatDbContext> _dbContextFactory;
    private readonly ILogger<BitrixOAuthService> _logger;

    private string? _fallbackAccessToken;
    private string? _fallbackRefreshToken;
    private DateTime _fallbackTokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public BitrixOAuthService(
        HttpClient httpClient,
        IOptions<BitrixLiveChatOptions> options,
        IDbContextFactory<LiveChatDbContext> dbContextFactory,
        ILogger<BitrixOAuthService> logger)
    {
        _httpClient = httpClient;
        _dbContextFactory = dbContextFactory;
        _logger = logger;

        var opt = options.Value;
        _configuredClientEndpoint = BitrixRestHelpers.NormalizeClientEndpoint(opt.Domain, null);
        _oauthClientId = opt.OAuthClientId;
        _oauthClientSecret = opt.OAuthClientSecret;
        _configuredRefreshToken = opt.OAuthRefreshToken;
        _fallbackRefreshToken = _configuredRefreshToken;
    }

    public async Task<BitrixRestConnection> GetConnectionAsync(CancellationToken ct)
    {
        await using var db = _dbContextFactory.CreateDbContext();
        var portal = await GetPreferredPortalAsync(db, trackChanges: true, ct);
        if (portal is not null)
        {
            return new BitrixRestConnection(
                portal.ClientEndpoint,
                await GetPortalAccessTokenAsync(db, portal, ct));
        }

        return new BitrixRestConnection(
            _configuredClientEndpoint,
            await GetFallbackAccessTokenAsync(ct));
    }

    public async Task<BitrixRestConnection> GetPortalConnectionAsync(BitrixLiveChatPortalEntity portal, CancellationToken ct)
    {
        await using var db = _dbContextFactory.CreateDbContext();
        var trackedPortal = await db.BitrixLiveChatPortals
            .FirstOrDefaultAsync(p => p.Id == portal.Id, ct)
            ?? throw new InvalidOperationException($"Bitrix portal {portal.Domain} not found in database.");

        return new BitrixRestConnection(
            trackedPortal.ClientEndpoint,
            await GetPortalAccessTokenAsync(db, trackedPortal, ct));
    }

    private async Task<string> GetPortalAccessTokenAsync(LiveChatDbContext db, BitrixLiveChatPortalEntity portal, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(portal.AccessToken) &&
            (!portal.AccessTokenExpiresAt.HasValue || DateTime.UtcNow < portal.AccessTokenExpiresAt.Value.AddSeconds(-60)))
        {
            return portal.AccessToken;
        }

        if (string.IsNullOrWhiteSpace(portal.RefreshToken))
        {
            throw new InvalidOperationException($"Bitrix portal {portal.Domain} does not have a refresh token.");
        }

        await _tokenLock.WaitAsync(ct);
        try
        {
            await db.Entry(portal).ReloadAsync(ct);

            if (!string.IsNullOrWhiteSpace(portal.AccessToken) &&
                (!portal.AccessTokenExpiresAt.HasValue || DateTime.UtcNow < portal.AccessTokenExpiresAt.Value.AddSeconds(-60)))
            {
                return portal.AccessToken;
            }

            var refreshed = await RefreshAccessTokenAsync(portal.RefreshToken, ct);
            portal.AccessToken = refreshed.AccessToken;
            portal.RefreshToken = refreshed.RefreshToken ?? portal.RefreshToken;
            portal.AccessTokenExpiresAt = refreshed.ExpiresAt;
            portal.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return portal.AccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<string> GetFallbackAccessTokenAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_fallbackAccessToken) && DateTime.UtcNow < _fallbackTokenExpiry)
        {
            return _fallbackAccessToken;
        }

        if (string.IsNullOrWhiteSpace(_fallbackRefreshToken))
        {
            throw new InvalidOperationException("Bitrix livechat OAuth refresh token is not configured.");
        }

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(_fallbackAccessToken) && DateTime.UtcNow < _fallbackTokenExpiry)
            {
                return _fallbackAccessToken;
            }

            var refreshed = await RefreshAccessTokenAsync(_fallbackRefreshToken, ct);
            _fallbackAccessToken = refreshed.AccessToken;
            _fallbackRefreshToken = refreshed.RefreshToken ?? _fallbackRefreshToken;
            _fallbackTokenExpiry = refreshed.ExpiresAt;
            return _fallbackAccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<BitrixOAuthToken> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync(
            $"https://oauth.bitrix.info/oauth/token/?grant_type=refresh_token&client_id={Uri.EscapeDataString(_oauthClientId)}&client_secret={Uri.EscapeDataString(_oauthClientSecret)}&refresh_token={Uri.EscapeDataString(refreshToken)}",
            ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogDebug("OAuth refresh response: {Body}", body);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to refresh Bitrix OAuth token (HTTP {(int)response.StatusCode}): {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        if (!root.TryGetProperty("access_token", out var accessTokenProp))
        {
            throw new InvalidOperationException($"Failed to refresh Bitrix OAuth token: {body}");
        }

        var accessToken = accessTokenProp.GetString();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Bitrix OAuth response did not include an access token.");
        }

        var expiresIn = root.TryGetProperty("expires_in", out var expiresInProp) && expiresInProp.TryGetInt32(out var parsedExpires)
            ? parsedExpires
            : 3600;

        return new BitrixOAuthToken(
            accessToken,
            root.TryGetProperty("refresh_token", out var refreshTokenProp) ? refreshTokenProp.GetString() : null,
            DateTime.UtcNow.AddSeconds(Math.Max(60, expiresIn - 60)));
    }

    private async Task<BitrixLiveChatPortalEntity?> GetPreferredPortalAsync(LiveChatDbContext db, bool trackChanges, CancellationToken ct)
    {
        IQueryable<BitrixLiveChatPortalEntity> query = db.BitrixLiveChatPortals;
        if (!trackChanges)
            query = query.AsNoTracking();

        var configuredHost = BitrixRestHelpers.NormalizeDomain(_configuredClientEndpoint);

        // Prefer the portal whose domain matches configuration; fall back to the most-recently updated one.
        var preferred = await query
            .Where(x => x.Domain == configuredHost)
            .FirstOrDefaultAsync(ct);

        if (preferred is not null)
            return preferred;

        return await query
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    }

    private sealed record BitrixOAuthToken(string AccessToken, string? RefreshToken, DateTime ExpiresAt);
}
