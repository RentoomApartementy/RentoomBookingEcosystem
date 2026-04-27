using RentoomBooking.Api.LiveChat.Entities;

namespace RentoomBooking.Api.LiveChat.Repositories;

public interface IBitrixPortalRepository
{
    Task<BitrixLiveChatPortalEntity?> GetByMemberIdAsync(string memberId, bool trackChanges = false, CancellationToken ct = default);
    Task<BitrixLiveChatPortalEntity?> GetByDomainAsync(string domain, bool trackChanges = false, CancellationToken ct = default);
    Task<List<BitrixLiveChatPortalEntity>> GetAllAsync(bool trackChanges = false, CancellationToken ct = default);
    Task<BitrixLiveChatPortalEntity> UpsertAsync(BitrixLiveChatPortalEntity portal, CancellationToken ct = default);
}
