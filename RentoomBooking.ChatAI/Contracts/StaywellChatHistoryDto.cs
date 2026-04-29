namespace RentoomBooking.ChatAI.Contracts;

public sealed record StaywellChatHistoryDto(
    string? ConversationId,
    IReadOnlyList<StaywellChatHistoryMessageDto> Messages);

public sealed record StaywellChatHistoryMessageDto(
    string Role,
    string Content,
    DateTime CreatedAt);
