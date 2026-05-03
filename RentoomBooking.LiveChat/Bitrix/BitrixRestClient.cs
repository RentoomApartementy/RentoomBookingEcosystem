using System.Text.Json;
using Microsoft.Extensions.Logging;
using RentoomBooking.LiveChat.Entities;

namespace RentoomBooking.LiveChat.Bitrix;

public sealed class BitrixRestClient : IBitrixRestClient
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly IBitrixLandingService _landingService;
    private readonly ILogger<BitrixRestClient> _logger;
    private readonly IBitrixMessageSender _messageSender;
    private readonly IBitrixOAuthService _oauthService;
    private readonly IBitrixUserService _userService;
    private readonly IBitrixWebhookService _webhookService;

    public BitrixRestClient(
        IBitrixOAuthService oauthService,
        IBitrixMessageSender messageSender,
        IBitrixUserService userService,
        IBitrixWebhookService webhookService,
        IBitrixLandingService landingService,
        HttpClient httpClient,
        ILogger<BitrixRestClient> logger)
    {
        _oauthService = oauthService;
        _messageSender = messageSender;
        _userService = userService;
        _webhookService = webhookService;
        _landingService = landingService;
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<BitrixRestConnection> GetConnectionAsync(CancellationToken ct)
    {
        return _oauthService.GetConnectionAsync(ct);
    }

    public Task<bool> SendGuestMessageToBitrixAsync(
        LiveChatSessionEntity session,
        LiveChatMessageEntity message,
        LiveChatCrmBindingTarget? crmTarget,
        CancellationToken ct)
    {
        return _messageSender.SendGuestMessageToBitrixAsync(session, message, crmTarget, ct);
    }

    public Task SendDeliveryStatusAsync(string messageId, string connectorChatId, CancellationToken ct)
    {
        return _messageSender.SendDeliveryStatusAsync(messageId, connectorChatId, ct);
    }

    public Task<(string? FirstName, string? AvatarUrl)> FetchOperatorInfoAsync(string bitrixUserId,
        CancellationToken ct)
    {
        return _userService.FetchOperatorInfoAsync(bitrixUserId, ct);
    }

    public Task<long?> BindWebhookEventAsync(BitrixLiveChatPortalEntity portal, Uri webhookUrl, CancellationToken ct)
    {
        return _webhookService.BindWebhookEventAsync(portal, webhookUrl, ct);
    }

    public Task RegisterConnectorAsync(BitrixLiveChatPortalEntity portal, string connectorId,
        Uri placementHandlerUrl, CancellationToken ct)
    {
        return _webhookService.RegisterConnectorAsync(portal, connectorId, placementHandlerUrl, ct);
    }

    public Task SetConnectorDataAsync(BitrixLiveChatPortalEntity portal, string connectorId, int lineId,
        Uri channelBaseUrl, CancellationToken ct)
    {
        return _webhookService.SetConnectorDataAsync(portal, connectorId, lineId, channelBaseUrl, ct);
    }

    public Task<string?> GetLandingPreviewUrlAsync(string url, CancellationToken ct)
    {
        return _landingService.GetLandingPreviewUrlAsync(url, ct);
    }
}