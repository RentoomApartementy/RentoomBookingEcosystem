using RentoomBooking.Api.LiveChat.Entities;

namespace RentoomBooking.Api.LiveChat.Bitrix;

public interface IBitrixWebhookService
{
    Task<long?> BindWebhookEventAsync(BitrixLiveChatPortalEntity portal, Uri webhookUrl, CancellationToken ct);
}
