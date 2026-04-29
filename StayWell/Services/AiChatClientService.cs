using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using RentoomBooking.ChatAI.Contracts;
using RentoomBooking.StayWell.Models.Chat;

namespace RentoomBooking.StayWell.Services;

public sealed class AiChatClientService
{
    private sealed record DonePayload(bool IsDone, string? ConversationId);
    private sealed record ErrorPayload(string Message);

    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public AiChatClientService(IHttpClientFactory httpClientFactory)
    {
        _http = httpClientFactory.CreateClient("FunctionsApi");
    }

    public async Task StreamAsync(
        ChatRequestDto request,
        AiChatTransportMode transportMode,
        Func<ChatChunkDto, Task> onChunk,
        Func<string?, Task> onDone,
        Func<string, Task> onError,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ResolveStreamPath(transportMode))
        {
            Content = JsonContent.Create(request, options: _jsonOptions)
        };

        httpRequest.SetBrowserResponseStreamingEnabled(true);

        using var response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response, cancellationToken);
            throw new HttpRequestException(message, null, response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var currentEvent = "message";

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync();
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                currentEvent = "message";
                continue;
            }

            if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
            {
                currentEvent = line[6..].Trim();
                continue;
            }

            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var payload = line[5..].Trim();
            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            switch (currentEvent)
            {
                case "chunk":
                {
                    var chunk = JsonSerializer.Deserialize<ChatChunkDto>(payload, _jsonOptions);
                    if (chunk is not null)
                    {
                        await onChunk(chunk);
                    }

                    break;
                }
                case "done":
                {
                    var done = JsonSerializer.Deserialize<DonePayload>(payload, _jsonOptions);
                    await onDone(done?.ConversationId);
                    return;
                }
                case "error":
                {
                    var error = JsonSerializer.Deserialize<ErrorPayload>(payload, _jsonOptions);
                    await onError(error?.Message ?? "Chat stream error.");
                    return;
                }
            }
        }
    }

    private static string ResolveStreamPath(AiChatTransportMode transportMode)
    {
        return transportMode == AiChatTransportMode.Agent
            ? "staywell/chatai/agent/stream"
            : "staywell/chatai/stream";
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(body))
        {
            return $"Chat request failed with status {(int)response.StatusCode}.";
        }

        try
        {
            var payload = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
            if (payload is not null && payload.TryGetValue("message", out var message) && !string.IsNullOrWhiteSpace(message))
            {
                return message;
            }
        }
        catch
        {
        }

        return body;
    }
}
