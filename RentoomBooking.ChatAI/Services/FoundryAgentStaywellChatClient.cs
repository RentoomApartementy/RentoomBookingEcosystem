using System.Runtime.CompilerServices;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;
using RentoomBooking.ChatAI.Contracts;

namespace RentoomBooking.ChatAI.Services;

public sealed class FoundryAgentStaywellChatClient : IStaywellAgentChatClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly StaywellAgentChatOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TokenCredential _credential;

    public FoundryAgentStaywellChatClient(IOptions<StaywellAgentChatOptions> options, IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _credential = new DefaultAzureCredential();
    }

    public async Task<string> CreateConversationAsync(
        string systemPrompt,
        CancellationToken cancellationToken = default)
    {
        var projectEndpoint = ResolveProjectEndpoint(_options);
        var requestBody = new
        {
            items = new[]
            {
                new FoundryResponseMessage("message", ChatRoles.User, BuildContextMessage(systemPrompt))
            }
        };

        using var request = await CreatePostRequestAsync(projectEndpoint, "conversations", requestBody, "application/json", cancellationToken);
        var httpClient = _httpClientFactory.CreateClient();
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Foundry conversation creation failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        if (document.RootElement.TryGetProperty("id", out var id) && !string.IsNullOrWhiteSpace(id.GetString()))
        {
            return id.GetString()!;
        }

        throw new InvalidOperationException("Foundry conversation creation response did not include an id.");
    }

    public async IAsyncEnumerable<string> CompleteStreamingAsync(
        string conversationId,
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var projectEndpoint = ResolveProjectEndpoint(_options);
        var agentName = ResolveAgentName(_options);
        var requestBody = new
        {
            agent_reference = new
            {
                name = agentName,
                type = "agent_reference"
            },
            conversation = conversationId,
            input = userMessage,
            stream = true
        };

        using var request = await CreatePostRequestAsync(projectEndpoint, "responses", requestBody, "text/event-stream", cancellationToken);
        var httpClient = _httpClientFactory.CreateClient();
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Foundry agent response failed with {(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var dataBuilder = new StringBuilder();

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (line.Length == 0)
            {
                foreach (var text in ReadTextDeltas(dataBuilder.ToString()))
                {
                    yield return text;
                }

                dataBuilder.Clear();
                continue;
            }

            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                dataBuilder.AppendLine(line["data:".Length..].TrimStart());
            }
        }

        foreach (var text in ReadTextDeltas(dataBuilder.ToString()))
        {
            yield return text;
        }
    }

    private async Task<HttpRequestMessage> CreatePostRequestAsync(
        string projectEndpoint,
        string path,
        object requestBody,
        string accept,
        CancellationToken cancellationToken)
    {
        var tokenScope = string.IsNullOrWhiteSpace(_options.TokenScope)
            ? "https://ai.azure.com/.default"
            : _options.TokenScope.Trim();
        var token = await _credential.GetTokenAsync(new TokenRequestContext([tokenScope]), cancellationToken);

        var requestUri = new Uri($"{projectEndpoint.TrimEnd('/')}/openai/v1/{path.TrimStart('/')}");
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        return request;
    }

    private static IEnumerable<string> ReadTextDeltas(string data)
    {
        if (string.IsNullOrWhiteSpace(data) || string.Equals(data.Trim(), "[DONE]", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        using var document = JsonDocument.Parse(data);
        var root = document.RootElement;
        if (root.TryGetProperty("type", out var type)
            && string.Equals(type.GetString(), "response.output_text.delta", StringComparison.OrdinalIgnoreCase)
            && root.TryGetProperty("delta", out var delta)
            && delta.ValueKind == JsonValueKind.String)
        {
            var deltaText = delta.GetString();
            if (deltaText is not null)
            {
                yield return deltaText;
            }
        }
        else if (root.TryGetProperty("error", out var error))
        {
            throw new InvalidOperationException($"Foundry agent response stream failed: {error}");
        }
    }

    private static string ResolveProjectEndpoint(StaywellAgentChatOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ProjectEndpoint))
        {
            return options.ProjectEndpoint.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.ToolboxEndpoint))
        {
            const string toolboxSegment = "/toolboxes/";
            var endpoint = options.ToolboxEndpoint.Trim();
            var index = endpoint.IndexOf(toolboxSegment, StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                return endpoint[..index];
            }
        }

        throw new InvalidOperationException("StaywellAgentChat:ProjectEndpoint is required to call the Foundry agent.");
    }

    private static string ResolveAgentName(StaywellAgentChatOptions options)
    {
        return string.IsNullOrWhiteSpace(options.AgentName)
            ? "staywell-events-mvp"
            : options.AgentName.Trim();
    }

    private static string BuildContextMessage(string systemPrompt)
    {
        return string.Join(
            Environment.NewLine,
            "StayWell context for this conversation turn follows.",
            "Use it as reservation and apartment context when answering the guest.",
            "Do not quote or expose this context unless the guest asks for a specific visible detail.",
            string.Empty,
            systemPrompt);
    }

    private sealed record FoundryResponseMessage(string Type, string Role, string Content);
}
