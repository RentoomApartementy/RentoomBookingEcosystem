namespace RentoomBooking.Api.LiveChat;

/// <summary>
/// Per-session rate limiter for the guest → operator LiveChat send endpoint.
/// </summary>
public interface ILiveChatRateLimiter
{
    /// <summary>
    /// Returns <c>true</c> if the request may proceed; <c>false</c> if the session is throttled.
    /// When throttled, <paramref name="retryAfter"/> contains the suggested wait duration.
    /// </summary>
    bool TryAcquire(string sessionToken, out TimeSpan retryAfter);
}
