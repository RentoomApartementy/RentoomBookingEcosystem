namespace RentoomBooking.ChatAI.Services;

public interface IChatRateLimiter
{
    bool TryAcquire(int reservationId, out TimeSpan retryAfter);
}
