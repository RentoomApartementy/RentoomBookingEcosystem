using RentoomBooking.ChatAI.Contracts;

namespace RentoomBooking.ChatAI.Services;

public interface IStaywellChatClient
{
    IAsyncEnumerable<string> CompleteStreamingAsync(
        string systemPrompt,
        IReadOnlyList<ChatTurn> history,
        string userMessage,
        CancellationToken cancellationToken = default);
}
