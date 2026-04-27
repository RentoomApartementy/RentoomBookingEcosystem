namespace RentoomBooking.LiveChat.Bitrix;

public interface IBitrixUserService
{
    Task<(string? FirstName, string? AvatarUrl)> FetchOperatorInfoAsync(string bitrixUserId, CancellationToken ct);
}