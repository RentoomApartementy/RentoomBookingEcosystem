using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RentoomBooking.ChatAI.Contracts;

namespace RentoomBooking.ChatAI.Services;

public sealed class MemoryChatRateLimiter : IChatRateLimiter
{
    private sealed class RateEntry
    {
        public int Count { get; set; }
        public DateTimeOffset WindowStart { get; set; }
    }

    private readonly IMemoryCache _cache;
    private readonly StaywellChatOptions _options;
    private readonly object _sync = new();

    public MemoryChatRateLimiter(IMemoryCache cache, IOptions<StaywellChatOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public bool TryAcquire(int reservationId, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        var now = DateTimeOffset.UtcNow;
        var key = $"chat-rate:{reservationId}";
        lock (_sync)
        {
            if (!_cache.TryGetValue<RateEntry>(key, out var entry) || entry is null || now - entry.WindowStart >= TimeSpan.FromMinutes(1))
            {
                entry = new RateEntry
                {
                    Count = 1,
                    WindowStart = now
                };

                _cache.Set(key, entry, TimeSpan.FromMinutes(1));
                return true;
            }

            if (entry.Count >= _options.MaxRequestsPerMinute)
            {
                var nextWindow = entry.WindowStart.AddMinutes(1);
                retryAfter = nextWindow - now;
                if (retryAfter < TimeSpan.Zero)
                {
                    retryAfter = TimeSpan.Zero;
                }

                return false;
            }

            entry.Count++;
            _cache.Set(key, entry, TimeSpan.FromMinutes(1));
            return true;
        }
    }
}
