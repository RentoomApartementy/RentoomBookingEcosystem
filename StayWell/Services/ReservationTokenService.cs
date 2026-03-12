namespace RentoomBooking.StayWell.Services
{
    public class ReservationTokenService(LocalStorageService localStorage)
    {
        private const string TokenKey = "staywell_reservation_token";
        private const string PwaTokenKey = "staywell_pwa_token";
        private readonly LocalStorageService _localStorage = localStorage;

        public Task<string?> GetTokenAsync() => _localStorage.GetItemAsync(TokenKey);

        public Task SaveTokenAsync(string token) => _localStorage.SetItemAsync(TokenKey, token);

        public Task ClearTokenAsync() => _localStorage.RemoveItemAsync(TokenKey);

        public Task<string?> GetPwaTokenAsync() => _localStorage.GetItemAsync(PwaTokenKey);

        public Task SavePwaTokenAsync(string token) => _localStorage.SetItemAsync(PwaTokenKey, token);

        public Task ClearPwaTokenAsync() => _localStorage.RemoveItemAsync(PwaTokenKey);
    }
}
