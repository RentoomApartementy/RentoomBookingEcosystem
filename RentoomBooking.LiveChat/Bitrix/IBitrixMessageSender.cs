using RentoomBooking.Api.LiveChat.Entities;

namespace RentoomBooking.Api.LiveChat.Bitrix;

public interface IBitrixMessageSender
{
    Task<bool> SendGuestMessageToBitrixAsync(
        LiveChatSessionEntity session,
        LiveChatMessageEntity message,
        LiveChatCrmBindingTarget? crmTarget,
        CancellationToken ct);

    Task SendDeliveryStatusAsync(string messageId, string connectorChatId, CancellationToken ct);
}
