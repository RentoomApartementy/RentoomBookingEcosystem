namespace RentoomBooking.ChatAI.Contracts;

public sealed record StaywellChatStreamResult(
    Guid ConversationId,
    int ChunkCount,
    int PromptTokenCount,
    int CompletionTokenCount,
    TimeSpan? TimeToFirstByte,
    TimeSpan TotalDuration,
    bool Completed);
