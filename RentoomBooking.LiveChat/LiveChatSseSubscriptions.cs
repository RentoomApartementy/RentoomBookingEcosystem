using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RentoomBooking.Api.LiveChat.Entities;

namespace RentoomBooking.Api.LiveChat;

public sealed class LiveChatSseSubscriptions : ILiveChatSseSubscriptions, IDisposable
{
    private sealed record SubscriptionEntry(Channel<LiveChatMessageEntity> Channel, DateTimeOffset CreatedAt);

    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, SubscriptionEntry>> _sessionSubscriptions = new();
    private readonly ILogger<LiveChatSseSubscriptions> _logger;
    private readonly Timer _cleanupTimer;

    // Max lifetime of any subscription — longer than StreamMaxDuration in LiveChatStreamFunction (4 min).
    private static readonly TimeSpan StaleSubscriptionAge = TimeSpan.FromMinutes(10);

    public LiveChatSseSubscriptions(ILogger<LiveChatSseSubscriptions> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(_ => CleanupStaleSubscriptions(), null,
            dueTime: StaleSubscriptionAge,
            period: StaleSubscriptionAge);
    }

    public Guid Subscribe(Guid sessionId)
    {
        var subscriptionId = Guid.NewGuid();
        var channel = Channel.CreateBounded<LiveChatMessageEntity>(
            new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });
        _sessionSubscriptions
            .GetOrAdd(sessionId, _ => new ConcurrentDictionary<Guid, SubscriptionEntry>())
            [subscriptionId] = new SubscriptionEntry(channel, DateTimeOffset.UtcNow);
        return subscriptionId;
    }

    public void Unsubscribe(Guid sessionId, Guid subscriptionId)
    {
        if (_sessionSubscriptions.TryGetValue(sessionId, out var subscribers))
        {
            subscribers.TryRemove(subscriptionId, out _);
            if (subscribers.IsEmpty)
                _sessionSubscriptions.TryRemove(sessionId, out _);
        }
    }

    public async Task<LiveChatMessageEntity?> WaitForOperatorMessageAsync(Guid sessionId, Guid subscriptionId, TimeSpan timeout, CancellationToken ct = default)
    {
        if (!_sessionSubscriptions.TryGetValue(sessionId, out var subscribers) ||
            !subscribers.TryGetValue(subscriptionId, out var entry))
        {
            return null;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            return await entry.Channel.Reader.ReadAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public void Notify(Guid sessionId, LiveChatMessageEntity message)
    {
        if (_sessionSubscriptions.TryGetValue(sessionId, out var subscribers))
        {
            foreach (var (subscriptionId, entry) in subscribers)
            {
                if (!entry.Channel.Writer.TryWrite(message))
                {
                    _logger.LogWarning(
                        "SSE channel full for session {SessionId}, subscription {SubscriptionId} — message {MessageId} dropped.",
                        sessionId, subscriptionId, message.Id);
                }
            }
        }
    }

    private void CleanupStaleSubscriptions()
    {
        var cutoff = DateTimeOffset.UtcNow - StaleSubscriptionAge;
        var staleCount = 0;

        foreach (var (sessionId, subscribers) in _sessionSubscriptions)
        {
            foreach (var (subscriptionId, entry) in subscribers)
            {
                if (entry.CreatedAt < cutoff)
                {
                    subscribers.TryRemove(subscriptionId, out _);
                    staleCount++;
                }
            }

            if (subscribers.IsEmpty)
                _sessionSubscriptions.TryRemove(sessionId, out _);
        }

        if (staleCount > 0)
            _logger.LogInformation("SSE cleanup: removed {Count} stale subscription(s).", staleCount);
    }

    public void Dispose() => _cleanupTimer.Dispose();
}
