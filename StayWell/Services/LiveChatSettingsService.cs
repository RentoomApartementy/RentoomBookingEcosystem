namespace RentoomBooking.StayWell.Services;

public class LiveChatSettingsService
{
    public event Action? OnOpenSettingsRequested;

    public void RequestOpenSettings() => OnOpenSettingsRequested?.Invoke();
}
