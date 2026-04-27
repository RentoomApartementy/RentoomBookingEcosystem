using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RentoomBooking.Api.LiveChat;
using RentoomBooking.Api.LiveChat.Entities;
using RentoomBooking.SharedClasses.LiveChat;

namespace RentoomBooking.Api.LiveChat.Functions;

public sealed class LiveChatSendFunction
{
    private readonly BitrixLiveChatService _liveChatService;
    private readonly ILiveChatRateLimiter _rateLimiter;
    private readonly ILogger<LiveChatSendFunction> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public LiveChatSendFunction(
        BitrixLiveChatService liveChatService,
        ILiveChatRateLimiter rateLimiter,
        ILogger<LiveChatSendFunction> logger)
    {
        _liveChatService = liveChatService;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    [Function("LiveChatSend")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "staywell/livechat/send")] HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return await CreateJsonResponse(req, HttpStatusCode.BadRequest, new { message = "Request body is required." });
        }

        LiveChatSendRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<LiveChatSendRequest>(body, _jsonOptions);
        }
        catch
        {
            return await CreateJsonResponse(req, HttpStatusCode.BadRequest, new { message = "Invalid JSON." });
        }

        if (request is null || string.IsNullOrWhiteSpace(request.ReservationToken) || string.IsNullOrWhiteSpace(request.Message))
        {
            return await CreateJsonResponse(req, HttpStatusCode.BadRequest, new { message = "ReservationToken and Message are required." });
        }

        if (request.Message.Length > 4000)
        {
            return await CreateJsonResponse(req, HttpStatusCode.BadRequest, new { message = "Message too long (max 4000 characters)." });
        }

        if (!_rateLimiter.TryAcquire(request.ReservationToken, out var retryAfter))
        {
            var response429 = await CreateJsonResponse(req, HttpStatusCode.TooManyRequests, new { message = "Too many requests. Please slow down." });
            response429.Headers.Add("Retry-After", ((int)retryAfter.TotalSeconds + 1).ToString());
            return response429;
        }

        try
        {
            var session = await _liveChatService.GetOrCreateSessionAsync(
                request.ReservationToken, request.GuestName, request.GuestEmail, ct);

            var message = await _liveChatService.SendGuestMessageAsync(session.Id, request.Message, ct);

            return await CreateJsonResponse(req, HttpStatusCode.OK, new LiveChatMessageDto(
                message.Id, message.Sender, message.Content, message.CreatedAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LiveChatSend failed for token {Token}", request.ReservationToken);
            return await CreateJsonResponse(req, HttpStatusCode.InternalServerError, new { message = "Failed to send message." });
        }
    }

    private async Task<HttpResponseData> CreateJsonResponse(HttpRequestData req, HttpStatusCode status, object payload)
    {
        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(payload, _jsonOptions));
        return response;
    }
}
