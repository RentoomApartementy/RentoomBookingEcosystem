using RentoomBooking.ChatAI.Contracts;

namespace RentoomBooking.ChatAI.Services;

public interface IStaywellAgentChatService
{
    Task<StaywellAgentChatStreamResult> StreamAsync(
        ChatRequestDto request,
        Func<ChatChunkDto, CancellationToken, Task> onChunk,
        CancellationToken cancellationToken = default);
}
