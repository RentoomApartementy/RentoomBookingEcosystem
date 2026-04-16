using System.Runtime.CompilerServices;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using RentoomBooking.ChatAI.Contracts;

namespace RentoomBooking.ChatAI.Services;

public sealed class AzureFoundryStaywellChatClient : IStaywellChatClient
{
    private readonly ChatClient _chatClient;

    public AzureFoundryStaywellChatClient(IOptions<StaywellChatOptions> options)
    {
        var chatOptions = options.Value;

        var azureClient = new AzureOpenAIClient(
            new Uri(chatOptions.Endpoint),
            new AzureKeyCredential(chatOptions.ApiKey));

        _chatClient = azureClient.GetChatClient(chatOptions.DeploymentName);
    }

    public async IAsyncEnumerable<string> CompleteStreamingAsync(
        string systemPrompt,
        IReadOnlyList<ChatTurn> history,
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt)
        };

        foreach (var item in history)
        {
            if (string.Equals(item.Role, ChatRoles.User, StringComparison.OrdinalIgnoreCase))
            {
                messages.Add(new UserChatMessage(item.Content));
            }
            else if (string.Equals(item.Role, ChatRoles.Assistant, StringComparison.OrdinalIgnoreCase))
            {
                messages.Add(new AssistantChatMessage(item.Content));
            }
        }

        messages.Add(new UserChatMessage(userMessage));

        var stream = _chatClient.CompleteChatStreaming(messages);

        foreach (var update in stream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var part in update.ContentUpdate)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(part.Text))
                {
                    continue;
                }

                yield return part.Text;
            }

            await Task.Yield();
        }
    }
}
