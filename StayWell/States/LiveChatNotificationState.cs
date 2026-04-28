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
                            _liveChatClient.InvalidateHistory(token);

                            if (msg.Sender != "guest" && msg.Sender != "system" && !_isChatOpen
                                && _countedMessageIds.Add(msg.Id))
                            {
                                _unreadCount++;
                                _ = PersistUnreadAsync();
                                OnChange?.Invoke();
                            }

                            if (OnNewMessage is not null)
                                await OnNewMessage(msg);
                        },
                        () => Task.CompletedTask,
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

                await Task.Delay(5000, ct);
            }
        }, ct);
    }

    public void StopListening()
    {
        _streamCts?.Cancel();
        _streamCts?.Dispose();
        _streamCts = null;
        _currentToken = null;
        _countedMessageIds.Clear();
    }

    public async Task LoadUnreadAsync(string token)
    {
        var (exists, count) = await _localStorage.TryGetItemAsync<int>($"livechat:unread:{token}");
        if (exists && count > 0)
        {
            _unreadCount = count;
            OnChange?.Invoke();
        }
    }

    private Task PersistUnreadAsync()
        => _currentToken is not null
            ? _localStorage.SetItemAsync($"livechat:unread:{_currentToken}", _unreadCount)
            : Task.CompletedTask;

    public void SetChatOpen(bool isOpen)
    {
        _isChatOpen = isOpen;
        if (isOpen) ClearUnread();
    }

    public void ClearUnread()
    {
        if (_unreadCount == 0) return;
        _unreadCount = 0;
        _ = PersistUnreadAsync();
        OnChange?.Invoke();
    }

    public void Dispose() => StopListening();
}
