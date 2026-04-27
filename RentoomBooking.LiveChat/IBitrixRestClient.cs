using RentoomBooking.LiveChat.Bitrix;
using RentoomBooking.LiveChat.Entities;

namespace RentoomBooking.LiveChat;

public interface IBitrixRestClient
{
    Task<BitrixRestConnection> GetConnectionAsync(CancellationToken ct);

    Task<bool> SendGuestMessageToBitrixAsync(LiveChatSessionEntity session, LiveChatMessageEntity message,
        LiveChatCrmBindingTarget? crmTarget, CancellationToken ct);

    Task SendDeliveryStatusAsync(string messageId, string connectorChatId, CancellationToken ct);
    Task<(string? FirstName, string? AvatarUrl)> FetchOperatorInfoAsync(string bitrixUserId, CancellationToken ct);
    Task<long?> BindWebhookEventAsync(BitrixLiveChatPortalEntity portal, Uri webhookUrl, CancellationToken ct);
    Task<string?> GetLandingPreviewUrlAsync(string url, CancellationToken ct);
}