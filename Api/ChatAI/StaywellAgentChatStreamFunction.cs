using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RentoomBooking.ChatAI.Contracts;
using RentoomBooking.ChatAI.Exceptions;
using RentoomBooking.ChatAI.Services;

namespace RentoomBooking.Api.Chat;

public sealed class StaywellAgentChatStreamFunction
{
    private readonly IStaywellAgentChatService _agentChatService;
    private readonly ILogger<StaywellAgentChatStreamFunction> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public StaywellAgentChatStreamFunction(IStaywellAgentChatService agentChatService, ILogger<StaywellAgentChatStreamFunction> logger)
    {
        _agentChatService = agentChatService;
        _logger = logger;
    }

    [Function("StaywellAgentChatAIStream")]
    public async Task<HttpResponseData> StreamAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "staywell/chatai/agent/stream")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var correlationId = GetOrCreateCorrelationId(req);
        var start = Stopwatch.StartNew();

        ChatRequestDto? request;
        try
        {
            var body = await req.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(body))
            {
                return await CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, "Request payload is required.", cancellationToken);
            }

            request = JsonSerializer.Deserialize<ChatRequestDto>(body, _jsonOptions);
        }
        catch (JsonException)
        {
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, "Invalid JSON payload.", cancellationToken);
        }

        if (request is null)
        {
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, "Request payload is required.", cancellationToken);
        }

        var reservationIdHash = HashToken(request.ReservationId.ToString());
        _logger.LogInformation(
            "StaywellAgentChatStream started. CorrelationId={CorrelationId}, ReservationIdHash={ReservationIdHash}, ConversationId={ConversationId}",
            correlationId,
            reservationIdHash,
            request.ConversationId ?? "new");

        var streamResponse = req.CreateResponse(HttpStatusCode.OK);
        streamResponse.Headers.Add("Content-Type", "text/event-stream");
        streamResponse.Headers.Add("Cache-Control", "no-cache");
        streamResponse.Headers.Add("Connection", "keep-alive");

        await using var writer = new StreamWriter(streamResponse.Body, new UTF8Encoding(false), leaveOpen: true);
        var streamStarted = false;

        try
        {
            var result = await _agentChatService.StreamAsync(
                request,
                async (chunk, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    await WriteSseEventAsync(writer, "chunk", chunk, token);
                    streamStarted = true;
                },
                cancellationToken);

            await WriteSseEventAsync(writer, "done", new
            {
                isDone = true,
                conversationId = result.ConversationId
            }, cancellationToken);
            streamStarted = true;

            _logger.LogInformation(
                "StaywellAgentChatStream completed. CorrelationId={CorrelationId}, ReservationIdHash={ReservationIdHash}, ConversationId={ConversationId}, ChunkCount={ChunkCount}, PromptTokenCount={PromptTokenCount}, CompletionTokenCount={CompletionTokenCount}, TtfbMs={TtfbMs}, TotalMs={TotalMs}",
                correlationId,
                reservationIdHash,
                result.ConversationId,
                result.ChunkCount,
                result.PromptTokenCount,
                result.CompletionTokenCount,
                result.TimeToFirstByte?.TotalMilliseconds ?? -1,
                result.TotalDuration.TotalMilliseconds);

            return streamResponse;
        }
        catch (ChatRateLimitException ex)
        {
            _logger.LogWarning(
                "StaywellAgentChatStream rate-limited. CorrelationId={CorrelationId}, ReservationIdHash={ReservationIdHash}, RetryAfterSeconds={RetryAfterSeconds}",
                correlationId,
                reservationIdHash,
                (int)Math.Ceiling(ex.RetryAfter.TotalSeconds));

            if (streamStarted)
            {
                await TryWriteErrorEventAsync(writer, ex.Message, cancellationToken);
                return streamResponse;
            }

            return await CreateErrorResponseAsync(req, HttpStatusCode.TooManyRequests, ex.Message, cancellationToken, (int)Math.Ceiling(ex.RetryAfter.TotalSeconds));
        }
        catch (ChatValidationException ex)
        {
            if (streamStarted)
            {
                await TryWriteErrorEventAsync(writer, ex.Message, cancellationToken);
                return streamResponse;
            }

            return await CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, ex.Message, cancellationToken);
        }
        catch (ChatForbiddenException ex)
        {
            if (streamStarted)
            {
                await TryWriteErrorEventAsync(writer, ex.Message, cancellationToken);
                return streamResponse;
            }

            return await CreateErrorResponseAsync(req, HttpStatusCode.Forbidden, ex.Message, cancellationToken);
        }
        catch (ChatNotFoundException ex)
        {
            if (streamStarted)
            {
                await TryWriteErrorEventAsync(writer, ex.Message, cancellationToken);
                return streamResponse;
            }

            return await CreateErrorResponseAsync(req, HttpStatusCode.NotFound, ex.Message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "StaywellAgentChatStream canceled. CorrelationId={CorrelationId}, ReservationIdHash={ReservationIdHash}, ElapsedMs={ElapsedMs}",
                correlationId,
                reservationIdHash,
                start.Elapsed.TotalMilliseconds);

            return streamResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "StaywellAgentChatStream failed. CorrelationId={CorrelationId}, ReservationIdHash={ReservationIdHash}, ElapsedMs={ElapsedMs}",
                correlationId,
                reservationIdHash,
                start.Elapsed.TotalMilliseconds);

            if (streamStarted)
            {
                await TryWriteErrorEventAsync(writer, "Internal server error.", cancellationToken);
                return streamResponse;
            }

            return await CreateErrorResponseAsync(req, HttpStatusCode.InternalServerError, "Internal server error: " + ex.Message, cancellationToken);
        }
    }

    private async Task<HttpResponseData> CreateErrorResponseAsync(
        HttpRequestData req,
        HttpStatusCode statusCode,
        string message,
        CancellationToken cancellationToken,
        int? retryAfterSeconds = null)
    {
        var response = req.CreateResponse(statusCode);
        response.Headers.Remove("Content-Type");
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");

        if (retryAfterSeconds.HasValue)
        {
            response.Headers.Add("Retry-After", retryAfterSeconds.Value.ToString());
        }

        await response.WriteStringAsync(JsonSerializer.Serialize(new { message }, _jsonOptions), cancellationToken);
        return response;
    }

    private async Task WriteSseEventAsync(StreamWriter writer, string eventName, object payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        await writer.WriteAsync($"event: {eventName}\n");
        await writer.WriteAsync($"data: {json}\n\n");
        await writer.FlushAsync();
    }

    private async Task TryWriteErrorEventAsync(StreamWriter writer, string message, CancellationToken cancellationToken)
    {
        try
        {
            await WriteSseEventAsync(writer, "error", new { message }, cancellationToken);
        }
        catch
        {
            // Client likely disconnected; ignore and close stream.
        }
    }

    private static string GetOrCreateCorrelationId(HttpRequestData req)
    {
        if (req.Headers.TryGetValues("x-correlation-id", out var values))
        {
            var value = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    private static string HashToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return "empty";
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes[..6]);
    }
}
