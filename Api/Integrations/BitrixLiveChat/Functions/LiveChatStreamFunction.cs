using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RentoomBooking.LiveChat;
using RentoomBooking.SharedClasses.LiveChat;

namespace RentoomBooking.Api.Integrations.BitrixLiveChat.Functions;

public sealed class LiveChatStreamFunction
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan StreamMaxDuration = TimeSpan.FromMinutes(4);
    private readonly BitrixLiveChatService _liveChatService;
    private readonly ILogger<LiveChatStreamFunction> _logger;
    private readonly ILiveChatSseSubscriptions _sseSubscriptions;

    public LiveChatStreamFunction(BitrixLiveChatService liveChatService, ILiveChatSseSubscriptions sseSubscriptions,
        ILogger<LiveChatStreamFunction> logger)
    {
        _liveChatService = liveChatService;
        _sseSubscriptions = sseSubscriptions;
        _logger = logger;
    }

    [Function("LiveChatStream")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "staywell/livechat/stream")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var query = HttpUtility.ParseQueryString(req.Url.Query);
        var reservationToken = query["reservationToken"];

        if (string.IsNullOrWhiteSpace(reservationToken))
        {
            var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
            badReq.Headers.Add("Content-Type", "application/json");
            await badReq.WriteStringAsync("{\"message\":\"reservationToken is required.\"}");
            return badReq;
        }

        var session = await _liveChatService.GetActiveSessionAsync(reservationToken, ct);
        if (session is null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            notFound.Headers.Add("Content-Type", "application/json");
            await notFound.WriteStringAsync("{\"message\":\"No active livechat session.\"}");
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/event-stream");
        response.Headers.Add("Cache-Control", "no-cache");
        response.Headers.Add("Connection", "keep-alive");

        await using var writer = new StreamWriter(response.Body, new UTF8Encoding(false), leaveOpen: true);

        await WriteSseAsync(writer, "connected", new { sessionId = session.Id }, ct);

        var subscriptionId = _sseSubscriptions.Subscribe(session.Id);
        var deadline = DateTime.UtcNow + StreamMaxDuration;

        try
        {
            while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
                var msg = await _sseSubscriptions.WaitForOperatorMessageAsync(session.Id, subscriptionId, PollTimeout,
                    ct);
                if (msg is not null)
                    await WriteSseAsync(writer, "message",
                        new LiveChatMessageDto(msg.Id, msg.Sender, msg.Content, msg.CreatedAt, msg.SenderName,
                            msg.OperatorAvatarUrl, _liveChatService.DeserializeAttachments(msg),
                            msg.OperatorBitrixUserId, msg.OriginalContent, msg.DetectedLanguage, msg.IsTranslated), ct);
                else
                    await WriteSseAsync(writer, "heartbeat", new { ts = DateTime.UtcNow }, ct);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Livechat stream cancelled for session {SessionId}", session.Id);
        }
        finally
        {
            _sseSubscriptions.Unsubscribe(session.Id, subscriptionId);
        }

        try
        {
            await WriteSseAsync(writer, "done", new { reason = "timeout" }, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Could not send final SSE message for session {SessionId}", session.Id);
        }

        return response;
    }

    private async Task WriteSseAsync(StreamWriter writer, string eventName, object payload,
        CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        await writer.WriteAsync($"event: {eventName}\n".AsMemory(), ct);
        await writer.WriteAsync($"data: {json}\n\n".AsMemory(), ct);
        await writer.FlushAsync(ct);
    }
}