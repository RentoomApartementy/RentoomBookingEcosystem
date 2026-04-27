using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RentoomBooking.SharedClasses.Configuration;

namespace RentoomBooking.LiveChat;

public sealed class MemoryLiveChatRateLimiter : ILiveChatRateLimiter
{
    private readonly IMemoryCache _cache;
    private readonly int _maxRequestsPerMinute;
    private readonly object _sync = new();

    public MemoryLiveChatRateLimiter(IMemoryCache cache, IOptions<BitrixLiveChatOptions> options)
    {
        _cache = cache;
        _maxRequestsPerMinute = options.Value.MaxMessagesPerMinute;
    }

    public bool TryAcquire(string sessionToken, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        var now = DateTimeOffset.UtcNow;
        var key = $"livechat-rate:{sessionToken}";

        lock (_sync)
        {
            if (!_cache.TryGetValue<RateEntry>(key, out var entry) || entry is null ||
                now - entry.WindowStart >= TimeSpan.FromMinutes(1))
            {
                _cache.Set(key, new RateEntry(1, now), TimeSpan.FromMinutes(1));
                return true;
            }

            if (entry.Count >= _maxRequestsPerMinute)
            {
                retryAfter = entry.WindowStart.AddMinutes(1) - now;
                if (retryAfter < TimeSpan.Zero) retryAfter = TimeSpan.Zero;
                return false;
            }

            _cache.Set(key, entry with { Count = entry.Count + 1 }, TimeSpan.FromMinutes(1));
            return true;
        }
    }

    private sealed record RateEntry(int Count, DateTimeOffset WindowStart);
}