namespace RentoomBooking.ChatAI.Contracts;

public sealed record ChatRequestDto(string Message, string ReservationToken, int ReservationId, string? ConversationId);
