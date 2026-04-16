using RentoomBooking.ChatAI.Entities;

namespace RentoomBooking.ChatAI.Repositories;

public interface IChatMessageRepository
{
    Task<IReadOnlyList<ChatMessageEntity>> GetRecentByConversationAsync(Guid conversationId, int limit, CancellationToken cancellationToken = default);
    Task AddAsync(ChatMessageEntity message, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<ChatMessageEntity> messages, CancellationToken cancellationToken = default);
}
