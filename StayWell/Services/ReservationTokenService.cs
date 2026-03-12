namespace RentoomBooking.StayWell.Services
{
    public class ReservationTokenService(LocalStorageService localStorage)
    {
        private const string TokenKey = "staywell_reservation_token";
        private readonly LocalStorageService _localStorage = localStorage;

        public Task<string?> GetTokenAsync() => _localStorage.GetItemAsync(TokenKey);

        public Task SaveTokenAsync(string token) => _localStorage.SetItemAsync(TokenKey, token);

        public Task ClearTokenAsync() => _localStorage.RemoveItemAsync(TokenKey);
    }
}
