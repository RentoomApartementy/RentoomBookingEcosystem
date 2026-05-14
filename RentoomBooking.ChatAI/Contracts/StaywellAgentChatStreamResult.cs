namespace RentoomBooking.ChatAI.Contracts;

public sealed record StaywellAgentChatStreamResult(
    string ConversationId,
    int ChunkCount,
    int PromptTokenCount,
    int CompletionTokenCount,
    TimeSpan? TimeToFirstByte,
    TimeSpan TotalDuration,
    bool Completed);
