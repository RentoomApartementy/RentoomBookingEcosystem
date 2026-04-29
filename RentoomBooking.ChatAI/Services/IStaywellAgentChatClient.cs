using RentoomBooking.ChatAI.Contracts;

namespace RentoomBooking.ChatAI.Services;

public interface IStaywellAgentChatClient
{
    Task<string> CreateConversationAsync(
        string systemPrompt,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> CompleteStreamingAsync(
        string conversationId,
        string userMessage,
        CancellationToken cancellationToken = default);
}
