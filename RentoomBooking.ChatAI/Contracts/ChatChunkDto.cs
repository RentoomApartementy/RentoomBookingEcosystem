namespace RentoomBooking.ChatAI.Contracts;

public sealed record ChatChunkDto(string Text, string Role = ChatRoles.Assistant, bool IsDone = false);
