using RentoomBooking.ChatAI.Entities;

namespace RentoomBooking.ChatAI.Repositories;

public interface IChatConversationRepository
{
    Task<ChatConversationEntity?> GetByIdAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<ChatConversationEntity?> GetLatestByReservationTokenAsync(string reservationToken, CancellationToken cancellationToken = default);
    Task<ChatConversationEntity> CreateAsync(string reservationToken, CancellationToken cancellationToken = default);
    Task TouchAsync(Guid conversationId, CancellationToken cancellationToken = default);
}
