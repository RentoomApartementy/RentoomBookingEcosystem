using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.LiveChat;
using RentoomBooking.StayWell.Services;

namespace RentoomBooking.StayWell.States;

public sealed class LiveChatNotificationState : IDisposable
{
    private readonly LiveChatClientService _liveChatClient;
    private readonly LocalStorageService _localStorage;
    private readonly ILogger<LiveChatNotificationState> _logger;
    private CancellationTokenSource? _streamCts;
    private string? _currentToken;
    private int _unreadCount;
    private bool _isChatOpen;
    private readonly HashSet<Guid> _countedMessageIds = new();
    private readonly HashSet<Guid> _knownMessageIds = new();
    private DateTimeOffset? _lastReadAt;

    public int UnreadCount => _unreadCount;

    public event Action? OnChange;
    public event Func<LiveChatMessageDto, Task>? OnNewMessage;

    public LiveChatNotificationState(LiveChatClientService liveChatClient, LocalStorageService localStorage, ILogger<LiveChatNotificationState> logger)
    {
        _liveChatClient = liveChatClient;
        _localStorage = localStorage;
        _logger = logger;
    }

    public void StartListening(string token)
    {
        if (_currentToken == token && _streamCts is { IsCancellationRequested: false }) return;

        StopListening();
        _currentToken = token;
        _streamCts = new CancellationTokenSource();
        var ct = _streamCts.Token;

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await _liveChatClient.StreamMessagesAsync(
                        token,
                        async msg =>
                        {
                            await _liveChatClient.InvalidateHistory(token);
                            _knownMessageIds.Add(msg.Id);

                            if (msg.Sender != "guest" && msg.Sender != "system")
                            {
                                if (_isChatOpen)
                                {
                                    _lastReadAt = DateTimeOffset.UtcNow;
                                    if (_currentToken is not null)
                                        _ = _localStorage.SetItemAsync(LastReadKey(_currentToken), _lastReadAt.Value);
                                }
                                else if (_countedMessageIds.Add(msg.Id))
                                {
                                    _unreadCount++;
                                    _ = PersistUnreadAsync();
                                    OnChange?.Invoke();
                                }
                            }

                            if (OnNewMessage is not null)
                                await OnNewMessage(msg);
                        },
                        () => SyncMissedMessagesAsync(token),
                        ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LiveChat SSE stream error for token {Token}; reconnecting in 5 s", _currentToken);
                }

                await Task.Delay(3000, ct);
            }
        }, ct);
    }

    private async Task SyncMissedMessagesAsync(string token)
    {
        try
        {
            await _liveChatClient.InvalidateHistory(token);
            var session = await _liveChatClient.GetHistoryAsync(token);
            if (session?.Messages is not { Count: > 0 }) return;

            foreach (var msg in session.Messages)
            {
                if (!_knownMessageIds.Add(msg.Id)) continue;

                if (msg.Sender != "guest" && msg.Sender != "system" && !_isChatOpen
                    && !IsBeforeLastRead(msg.CreatedAt)
                    && _countedMessageIds.Add(msg.Id))
                {
                    _unreadCount++;
                    _ = PersistUnreadAsync();
                    OnChange?.Invoke();
                }

                if (OnNewMessage is not null)
                    await OnNewMessage(msg);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync missed messages for token {Token}", token);
        }
    }

    public void StopListening()
    {
        _streamCts?.Cancel();
        _streamCts?.Dispose();
        _streamCts = null;
        _currentToken = null;
        _countedMessageIds.Clear();
        _knownMessageIds.Clear();
    }

    public async Task LoadUnreadAsync(string token)
    {
        var (hasLastRead, lastRead) = await _localStorage.TryGetItemAsync<DateTimeOffset>(LastReadKey(token));
        _lastReadAt = hasLastRead ? lastRead : null;

        try
        {
            await _liveChatClient.InvalidateHistory(token);
            var session = await _liveChatClient.GetHistoryAsync(token);
            if (session?.Messages is { Count: > 0 })
            {
                foreach (var msg in session.Messages)
                    _knownMessageIds.Add(msg.Id);

                var unread = 0;
                foreach (var msg in session.Messages)
                {
                    if (msg.Sender == "guest" || msg.Sender == "system") continue;
                    if (IsBeforeLastRead(msg.CreatedAt)) continue;
                    if (_countedMessageIds.Add(msg.Id))
                        unread++;
                }

                if (unread > 0)
                {
                    _unreadCount = unread;
                    await PersistUnreadAsync();
                    OnChange?.Invoke();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to compute unread count from history for token {Token}", token);
            var (exists, count) = await _localStorage.TryGetItemAsync<int>(UnreadKey(token));
            if (exists && count > 0)
            {
                _unreadCount = count;
                OnChange?.Invoke();
            }
        }
    }

    private bool IsBeforeLastRead(DateTime createdAt)
        => _lastReadAt.HasValue
            && DateTime.SpecifyKind(createdAt, DateTimeKind.Utc) <= _lastReadAt.Value.UtcDateTime;

    private Task PersistUnreadAsync()
        => _currentToken is not null
            ? _localStorage.SetItemAsync(UnreadKey(_currentToken), _unreadCount)
            : Task.CompletedTask;

    public void SetChatOpen(bool isOpen)
    {
        _isChatOpen = isOpen;
        if (isOpen) ClearUnread();
    }

    public void ClearUnread()
    {
        _unreadCount = 0;
        _countedMessageIds.Clear();
        _lastReadAt = DateTimeOffset.UtcNow;

        if (_currentToken is not null)
        {
            _ = _localStorage.SetItemAsync(LastReadKey(_currentToken), _lastReadAt.Value);
            _ = _localStorage.SetItemAsync(UnreadKey(_currentToken), 0);
        }

        OnChange?.Invoke();
    }

    private static string UnreadKey(string token) => $"livechat:unread:{token}";
    private static string LastReadKey(string token) => $"livechat:lastread:{token}";

    public void Dispose() => StopListening();
}
