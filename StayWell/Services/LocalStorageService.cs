using System.Text.Json;
using Microsoft.JSInterop;

namespace RentoomBooking.StayWell.Services
{
    public class LocalStorageService(IJSRuntime jsRuntime, JsonSerializerOptions json)
    {
        private readonly IJSRuntime _jsRuntime = jsRuntime;
        private readonly JsonSerializerOptions _json = json;

        public async Task<string?> GetItemAsync(string key)
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LocalStorageService.GetItemAsync failed: {ex.Message}");
                return null;
            }
        }

        public async Task<(bool Exists, T? Value)> TryGetItemAsync<T>(string key)
        {
            var raw = await GetItemAsync(key);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return (false, default);
            }

            try
            {
                return (true, JsonSerializer.Deserialize<T>(raw, _json));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LocalStorageService.TryGetItemAsync failed: {ex.Message}");
                return (true, default);
            }
        }

        public async Task SetItemAsync(string key, string value)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LocalStorageService.SetItemAsync failed: {ex.Message}");
            }
        }

        public async Task SetItemAsync<T>(string key, T value)
        {
            try
            {
                var payload = JsonSerializer.Serialize(value, _json);
                await SetItemAsync(key, payload);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LocalStorageService.SetItemAsync<T> failed: {ex.Message}");
            }
        }

        public async Task RemoveItemAsync(string key)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LocalStorageService.RemoveItemAsync failed: {ex.Message}");
            }
        }
    }
}
