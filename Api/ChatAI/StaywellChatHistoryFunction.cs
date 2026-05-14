using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RentoomBooking.ChatAI.Contracts;
using RentoomBooking.ChatAI.Repositories;

namespace RentoomBooking.Api.Chat;

public sealed class StaywellChatHistoryFunction
{
    private readonly IChatConversationRepository _conversationRepository;
    private readonly IChatMessageRepository _messageRepository;
    private readonly ILogger<StaywellChatHistoryFunction> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public StaywellChatHistoryFunction(
        IChatConversationRepository conversationRepository,
        IChatMessageRepository messageRepository,
        ILogger<StaywellChatHistoryFunction> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _logger = logger;
    }

    [Function("StaywellChatHistory")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "staywell/chatai/history")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(req.Url.Query);
        var reservationToken = query["reservationToken"]?.Trim();
        var conversationId = query["conversationId"]?.Trim();
        var mode = query["mode"]?.Trim();

        if (string.IsNullOrWhiteSpace(reservationToken))
        {
            return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, new { message = "reservationToken query param is required." }, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return await CreateJsonResponseAsync(req, HttpStatusCode.OK, new StaywellChatHistoryDto(null, []), cancellationToken);
        }

        var isAgentMode = string.Equals(mode, "agent", StringComparison.OrdinalIgnoreCase);
        var reservationIdHash = HashToken(reservationToken);

        if (isAgentMode)
        {
            var auditToken = $"{reservationToken}:agent:{conversationId}";
            var auditConversation = await _conversationRepository.GetLatestByReservationTokenAsync(auditToken, cancellationToken);
            if (auditConversation is null)
            {
                _logger.LogInformation(
                    "StaywellChatHistory agent conversation not found. ReservationTokenHash={ReservationTokenHash}, FoundryConversationId={FoundryConversationId}",
                    reservationIdHash,
                    conversationId);
                return await CreateJsonResponseAsync(req, HttpStatusCode.OK, new StaywellChatHistoryDto(conversationId, []), cancellationToken);
            }

            var messages = await _messageRepository.GetRecentByConversationAsync(auditConversation.Id, 200, cancellationToken);
            var payload = new StaywellChatHistoryDto(
                conversationId,
                messages
                    .Where(m => string.Equals(m.Role, ChatRoles.User, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(m.Role, ChatRoles.Assistant, StringComparison.OrdinalIgnoreCase))
                    .Select(m => new StaywellChatHistoryMessageDto(m.Role, m.Content, DateTime.SpecifyKind(m.CreatedAt, DateTimeKind.Utc)))
                    .ToList());

            return await CreateJsonResponseAsync(req, HttpStatusCode.OK, payload, cancellationToken);
        }

        if (!Guid.TryParse(conversationId, out var classicConversationId))
        {
            return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, new { message = "conversationId is invalid for classic mode." }, cancellationToken);
        }

        var conversation = await _conversationRepository.GetByIdAsync(classicConversationId, cancellationToken);
        if (conversation is null)
        {
            return await CreateJsonResponseAsync(req, HttpStatusCode.OK, new StaywellChatHistoryDto(conversationId, []), cancellationToken);
        }

        if (!string.Equals(conversation.ReservationToken, reservationToken, StringComparison.OrdinalIgnoreCase))
        {
            return await CreateJsonResponseAsync(req, HttpStatusCode.Forbidden, new { message = "Conversation does not belong to provided reservation token." }, cancellationToken);
        }

        var classicMessages = await _messageRepository.GetRecentByConversationAsync(classicConversationId, 200, cancellationToken);
        var classicPayload = new StaywellChatHistoryDto(
            conversationId,
            classicMessages
                .Where(m => string.Equals(m.Role, ChatRoles.User, StringComparison.OrdinalIgnoreCase)
                         || string.Equals(m.Role, ChatRoles.Assistant, StringComparison.OrdinalIgnoreCase))
                .Select(m => new StaywellChatHistoryMessageDto(m.Role, m.Content, DateTime.SpecifyKind(m.CreatedAt, DateTimeKind.Utc)))
                .ToList());

        return await CreateJsonResponseAsync(req, HttpStatusCode.OK, classicPayload, cancellationToken);
    }

    private async Task<HttpResponseData> CreateJsonResponseAsync(
        HttpRequestData req,
        HttpStatusCode statusCode,
        object payload,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(statusCode);
        response.Headers.Remove("Content-Type");
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(payload, _jsonOptions), cancellationToken);
        return response;
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
