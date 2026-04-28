using RentoomBooking.LiveChat.Entities;

namespace RentoomBooking.LiveChat.Bitrix;

public interface IBitrixOAuthService
{
    Task<BitrixRestConnection> GetConnectionAsync(CancellationToken ct);
    Task<BitrixRestConnection> GetPortalConnectionAsync(BitrixLiveChatPortalEntity portal, CancellationToken ct);
}