using RentoomBooking.Api.LiveChat.Entities;

namespace RentoomBooking.Api.LiveChat.Repositories;

public interface ILiveChatSessionRepository
{
    Task<LiveChatSessionEntity?> GetActiveByReservationTokenAsync(string token, CancellationToken ct = default);
    Task<LiveChatSessionEntity?> GetActiveByIdAsync(Guid id, CancellationToken ct = default);
    Task<LiveChatSessionEntity?> GetActiveByBitrixChatIdAsync(string bitrixChatId, CancellationToken ct = default);
    Task<LiveChatSessionEntity> CreateAsync(LiveChatSessionEntity session, CancellationToken ct = default);
    Task UpdateAsync(LiveChatSessionEntity session, CancellationToken ct = default);
}
