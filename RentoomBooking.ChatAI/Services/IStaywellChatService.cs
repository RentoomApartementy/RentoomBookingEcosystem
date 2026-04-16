using RentoomBooking.ChatAI.Contracts;

namespace RentoomBooking.ChatAI.Services;

public interface IStaywellChatService
{
    Task<StaywellChatStreamResult> StreamAsync(
        ChatRequestDto request,
        Func<ChatChunkDto, CancellationToken, Task> onChunk,
        CancellationToken cancellationToken = default);
}
