using RentoomBooking.Api.LiveChat.Entities;

namespace RentoomBooking.Api.LiveChat.Bitrix;

public interface IBitrixOAuthService
{
    Task<BitrixRestConnection> GetConnectionAsync(CancellationToken ct);
    Task<BitrixRestConnection> GetPortalConnectionAsync(BitrixLiveChatPortalEntity portal, CancellationToken ct);
}
