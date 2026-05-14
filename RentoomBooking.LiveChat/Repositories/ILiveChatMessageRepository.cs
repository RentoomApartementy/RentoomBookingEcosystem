using RentoomBooking.LiveChat.Entities;

namespace RentoomBooking.LiveChat.Repositories;

public interface ILiveChatMessageRepository
{
    Task<List<LiveChatMessageEntity>> GetBySessionAsync(Guid sessionId, DateTime? after = null,
        CancellationToken ct = default);

    Task<LiveChatMessageEntity?> GetByBitrixMessageIdAsync(string bitrixMessageId, CancellationToken ct = default);
    Task<LiveChatMessageEntity> CreateAsync(LiveChatMessageEntity message, CancellationToken ct = default);
    Task UpdateAsync(LiveChatMessageEntity message, CancellationToken ct = default);
}