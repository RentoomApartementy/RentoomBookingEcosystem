using RentoomBooking.LiveChat.Entities;

namespace RentoomBooking.LiveChat;

public interface ILiveChatSseSubscriptions
{
    Guid Subscribe(Guid sessionId);
    void Unsubscribe(Guid sessionId, Guid subscriptionId);

    Task<LiveChatMessageEntity?> WaitForOperatorMessageAsync(Guid sessionId, Guid subscriptionId, TimeSpan timeout,
        CancellationToken ct);

    void Notify(Guid sessionId, LiveChatMessageEntity message);
}