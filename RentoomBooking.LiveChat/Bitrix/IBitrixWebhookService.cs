using RentoomBooking.LiveChat.Entities;

namespace RentoomBooking.LiveChat.Bitrix;

public interface IBitrixWebhookService
{
    Task<long?> BindWebhookEventAsync(BitrixLiveChatPortalEntity portal, Uri webhookUrl, CancellationToken ct);
    Task RegisterConnectorAsync(BitrixLiveChatPortalEntity portal, string connectorId, Uri placementHandlerUrl, CancellationToken ct);
    Task SetConnectorDataAsync(BitrixLiveChatPortalEntity portal, string connectorId, int lineId, Uri channelBaseUrl, CancellationToken ct);
}