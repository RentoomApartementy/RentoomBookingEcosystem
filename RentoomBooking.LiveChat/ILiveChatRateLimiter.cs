namespace RentoomBooking.LiveChat;

public interface ILiveChatRateLimiter
{
    bool TryAcquire(string sessionToken, out TimeSpan retryAfter);
}