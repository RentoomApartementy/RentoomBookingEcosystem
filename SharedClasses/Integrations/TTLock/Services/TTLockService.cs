using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Integrations.TTLock.Models;

namespace RentoomBooking.SharedClasses.Integrations.TTLock
{
    public class TTLockService
    {
        private readonly HttpClient _http;
        private readonly TTLockSettings _settings;
        private readonly ILogger<TTLockService> _logger;
        private string? _accessToken;

        public TTLockService(HttpClient http, IOptions<TTLockSettings> options, ILogger<TTLockService> logger)
        {
            _http = http;
            _settings = options.Value;
            _logger = logger;
        }

        private async Task<string> GetTokenAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken)) return _accessToken;

            var fields = new Dictionary<string, string>
            {
                { "client_id", _settings.ClientId },
                { "client_secret", _settings.ClientSecret },
                { "grant_type", "password" },
                { "username", _settings.Username },
                { "password", _settings.Password }
            };

            var resp = await _http.PostAsync($"{_settings.ApiBaseUrl}/oauth2/token", new FormUrlEncodedContent(fields));
            var result = JsonConvert.DeserializeObject<TTLockTokenResponse>(await resp.Content.ReadAsStringAsync());

            if (result?.ErrCode != 0) throw new Exception($"TTLock Auth Error: {result?.ErrMsg}");

            _accessToken = result!.AccessToken;
            return _accessToken;
        }

        public async Task<TTLockBaseResponse> UnlockAsync(int lockId)
            => await SendPostAsync("/v3/lock/unlock", lockId);

        public async Task<TTLockBaseResponse> LockAsync(int lockId)
            => await SendPostAsync("/v3/lock/lock", lockId);

        public async Task<TTLockStateResponse> GetLockStateAsync(int lockId)
        {
            var result = await SendGetAsync<TTLockStateResponse>("/v3/lock/queryOpenState", lockId);
            return result;
        }

        public async Task<TTLockElectricResponse> GetBatteryLevelAsync(int lockId)
        {
            var result = await SendGetAsync<TTLockElectricResponse>("/v3/lock/queryElectricQuantity", lockId);
            return result;
        }

        private async Task<T> SendGetAsync<T>(string endpoint, int lockId) where T : TTLockBaseResponse
        {
            var token = await GetTokenAsync();
            var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var url = $"{_settings.ApiBaseUrl}{endpoint}?clientId={_settings.ClientId}&accessToken={token}&lockId={lockId}&date={ts}";

            var resp = await _http.GetAsync(url);
            var content = await resp.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(content) ?? Activator.CreateInstance<T>();
        }

        private async Task<TTLockBaseResponse> SendPostAsync(string endpoint, int lockId)
        {
            var token = await GetTokenAsync();
            var fields = new Dictionary<string, string>
            {
                { "clientId", _settings.ClientId },
                { "accessToken", token },
                { "lockId", lockId.ToString() },
                { "date", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() }
            };

            var resp = await _http.PostAsync($"{_settings.ApiBaseUrl}{endpoint}", new FormUrlEncodedContent(fields));
            var content = await resp.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TTLockBaseResponse>(content) ?? new TTLockBaseResponse { ErrCode = -1 };
        }
    }
}