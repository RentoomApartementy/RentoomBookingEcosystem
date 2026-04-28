namespace RentoomBooking.LiveChat.Bitrix;

public interface IBitrixLandingService
{
    Task<string?> GetLandingPreviewUrlAsync(string url, CancellationToken ct);
}