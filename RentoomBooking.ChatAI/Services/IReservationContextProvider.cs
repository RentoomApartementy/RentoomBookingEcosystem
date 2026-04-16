using RentoomBooking.ChatAI.Contracts;

namespace RentoomBooking.ChatAI.Services;

public interface IReservationContextProvider
{
    Task<ReservationPromptContext?> GetContextAsync(int reservationId, string reservationToken, CancellationToken cancellationToken = default);
}
