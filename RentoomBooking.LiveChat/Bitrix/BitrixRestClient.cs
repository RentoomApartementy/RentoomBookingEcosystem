using System.Text.Json;
using Microsoft.Extensions.Logging;
using RentoomBooking.Api.LiveChat;
using RentoomBooking.Api.LiveChat.Entities;

namespace RentoomBooking.Api.LiveChat.Bitrix;

public sealed class BitrixRestClient : IBitrixRestClient
{
    private readonly IBitrixOAuthService _oauthService;
    private readonly IBitrixMessageSender _messageSender;
    private readonly IBitrixUserService _userService;
    private readonly IBitrixWebhookService _webhookService;
    private readonly IBitrixLandingService _landingService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<BitrixRestClient> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

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
        => _oauthService.GetConnectionAsync(ct);

    public Task<bool> SendGuestMessageToBitrixAsync(
        LiveChatSessionEntity session,
        LiveChatMessageEntity message,
        LiveChatCrmBindingTarget? crmTarget,
        CancellationToken ct)
        => _messageSender.SendGuestMessageToBitrixAsync(session, message, crmTarget, ct);

    public Task SendDeliveryStatusAsync(string messageId, string connectorChatId, CancellationToken ct)
        => _messageSender.SendDeliveryStatusAsync(messageId, connectorChatId, ct);

    public Task<(string? FirstName, string? AvatarUrl)> FetchOperatorInfoAsync(string bitrixUserId, CancellationToken ct)
        => _userService.FetchOperatorInfoAsync(bitrixUserId, ct);

    public Task<long?> BindWebhookEventAsync(BitrixLiveChatPortalEntity portal, Uri webhookUrl, CancellationToken ct)
        => _webhookService.BindWebhookEventAsync(portal, webhookUrl, ct);

    public Task<string?> GetLandingPreviewUrlAsync(string url, CancellationToken ct)
        => _landingService.GetLandingPreviewUrlAsync(url, ct);
}
