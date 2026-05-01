using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using RentoomBooking.SharedClasses.LiveChat;

namespace RentoomBooking.StayWell.Services;

public sealed class LiveChatClientService
{
    private sealed record CachedHistory(LiveChatSessionDto Data, DateTimeOffset CachedAt);

    private readonly HttpClient _http;
    private readonly LocalStorageService _localStorage;
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan _historyCacheTtl = TimeSpan.FromSeconds(60);

    public LiveChatClientService(IHttpClientFactory httpClientFactory, LocalStorageService localStorage)
    {
        _http = httpClientFactory.CreateClient("FunctionsApi");
        _localStorage = localStorage;
    }

    public async Task<LiveChatMessageDto?> SendMessageAsync(string reservationToken, string message, string? guestName = null, string? guestEmail = null, CancellationToken ct = default)
    {
        var request = new LiveChatSendRequest(reservationToken, message, guestName, guestEmail);
        var response = await _http.PostAsJsonAsync("staywell/livechat/send", request, _jsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LiveChatMessageDto>(_jsonOptions, ct);
    }

    public async Task<LiveChatSessionDto?> GetHistoryAsync(string reservationToken, CancellationToken ct = default)
    {
        var key = BuildCacheKey(reservationToken);

        var (exists, cached) = await _localStorage.TryGetItemAsync<CachedHistory>(key);
        if (exists && cached is not null && DateTimeOffset.UtcNow - cached.CachedAt < _historyCacheTtl)
            return cached.Data;

        var response = await _http.GetAsync($"staywell/livechat/history?reservationToken={Uri.EscapeDataString(reservationToken)}", ct);
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<LiveChatSessionDto>(_jsonOptions, ct);
        if (result is not null)
            await _localStorage.SetItemAsync(key, new CachedHistory(result, DateTimeOffset.UtcNow));

        return result;
    }

    public async Task InvalidateHistory(string reservationToken)
        => await _localStorage.RemoveItemAsync(BuildCacheKey(reservationToken));

    private static string BuildCacheKey(string reservationToken) => $"staywell:livechat:history:{reservationToken}";

    public async Task StreamMessagesAsync(
        string reservationToken,
        Func<LiveChatMessageDto, Task> onMessage,
        Func<Task> onConnected,
        CancellationToken ct = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get,
            $"staywell/livechat/stream?reservationToken={Uri.EscapeDataString(reservationToken)}");
        httpRequest.SetBrowserResponseStreamingEnabled(true);

        using var response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        var currentEvent = "message";

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                currentEvent = "message";
                continue;
            }

            if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
            {
                currentEvent = line[6..].Trim();
                continue;
            }

            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var payload = line[5..].Trim();

            switch (currentEvent)
            {
                case "connected":
                    await onConnected();
                    break;
                case "message":
                    var msg = JsonSerializer.Deserialize<LiveChatMessageDto>(payload, _jsonOptions);
                    if (msg is not null)
                    {
                        await onMessage(msg);
                    }
                    break;
                case "done":
                    return;
            }
        }
    }

    public async Task<LinkPreviewDto?> GetLinkPreviewAsync(string url, CancellationToken ct = default)
    {
        var encoded = Uri.EscapeDataString(url);
        var response = await _http.GetAsync($"staywell/link-preview?url={encoded}", ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<LinkPreviewDto>(_jsonOptions, ct);
    }

    public async Task<LiveChatSessionSettingsDto?> GetSettingsAsync(string reservationToken, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"staywell/livechat/settings?reservationToken={Uri.EscapeDataString(reservationToken)}", ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<LiveChatSessionSettingsDto>(_jsonOptions, ct);
    }

    public async Task<bool> UpdateSettingsAsync(string reservationToken, bool? guestAutoTranslateEnabled = null, string? preferredLanguage = null, CancellationToken ct = default)
    {
        var request = new LiveChatSessionSettingsUpdateRequest(reservationToken, guestAutoTranslateEnabled, preferredLanguage);
        var response = await _http.PostAsJsonAsync("staywell/livechat/settings", request, _jsonOptions, ct);
        return response.IsSuccessStatusCode;
    }
}
