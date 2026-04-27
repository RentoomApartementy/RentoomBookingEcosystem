using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RentoomBooking.Api.LiveChat;
using RentoomBooking.SharedClasses.LiveChat;

namespace RentoomBooking.Api.LiveChat.Functions;

public sealed class LiveChatHistoryFunction
{
    private readonly BitrixLiveChatService _liveChatService;
    private readonly ILogger<LiveChatHistoryFunction> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public LiveChatHistoryFunction(BitrixLiveChatService liveChatService, ILogger<LiveChatHistoryFunction> logger)
    {
        _liveChatService = liveChatService;
        _logger = logger;
    }

    [Function("LiveChatHistory")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "staywell/livechat/history")] HttpRequestData req,
        CancellationToken ct)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var reservationToken = query["reservationToken"];

        if (string.IsNullOrWhiteSpace(reservationToken))
        {
            return await CreateJsonResponse(req, HttpStatusCode.BadRequest, new { message = "reservationToken query param is required." });
        }

        var session = await _liveChatService.GetActiveSessionAsync(reservationToken, ct);
        if (session is null)
        {
            return await CreateJsonResponse(req, HttpStatusCode.OK, new LiveChatSessionDto(Guid.Empty, "none", new()));
        }

        var messages = await _liveChatService.GetMessagesAsync(session.Id, ct: ct);
        var dtos = messages.Select(m => new LiveChatMessageDto(
            m.Id, m.Sender, m.Content, m.CreatedAt, m.OperatorName, m.OperatorAvatarUrl,
            _liveChatService.DeserializeAttachments(m), m.OperatorBitrixUserId)).ToList();

        return await CreateJsonResponse(req, HttpStatusCode.OK, new LiveChatSessionDto(session.Id, session.Status, dtos));
    }

    private async Task<HttpResponseData> CreateJsonResponse(HttpRequestData req, HttpStatusCode status, object payload)
    {
        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(payload, _jsonOptions));
        return response;
    }
}
