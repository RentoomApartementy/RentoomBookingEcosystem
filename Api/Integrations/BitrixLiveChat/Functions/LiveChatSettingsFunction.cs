using System.Net;
using System.Text.Json;
using System.Web;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Globalization;
using RentoomBooking.LiveChat;
using RentoomBooking.SharedClasses.LiveChat;

namespace RentoomBooking.Api.Integrations.BitrixLiveChat.Functions;

public sealed class LiveChatSettingsFunction
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly BitrixLiveChatService _liveChatService;
    private readonly ILogger<LiveChatSettingsFunction> _logger;

    public LiveChatSettingsFunction(
        BitrixLiveChatService liveChatService,
        ILogger<LiveChatSettingsFunction> logger)
    {
        _liveChatService = liveChatService;
        _logger = logger;
    }

    [Function("LiveChatSettingsGet")]
    public async Task<HttpResponseData> GetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "staywell/livechat/settings")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var query = HttpUtility.ParseQueryString(req.Url.Query);
        var reservationToken = query["reservationToken"];

        if (string.IsNullOrWhiteSpace(reservationToken))
            return await CreateJsonResponse(req, HttpStatusCode.BadRequest,
                new { message = "reservationToken query param is required." });

        try
        {
            var session = await _liveChatService.GetActiveSessionAsync(reservationToken, ct);
            if (session is null)
                return await CreateJsonResponse(req, HttpStatusCode.OK,
                    new LiveChatSessionSettingsDto());

            return await CreateJsonResponse(req, HttpStatusCode.OK,
                new LiveChatSessionSettingsDto(session.GuestAutoTranslateEnabled, session.PreferredLanguage));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LiveChatSettingsGet failed for token {Token}", reservationToken);
            return await CreateJsonResponse(req, HttpStatusCode.InternalServerError,
                new { message = "Failed to get settings." });
        }
    }

    [Function("LiveChatSettings")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "staywell/livechat/settings")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
            return await CreateJsonResponse(req, HttpStatusCode.BadRequest,
                new { message = "Request body is required." });

        LiveChatSessionSettingsUpdateRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<LiveChatSessionSettingsUpdateRequest>(body, _jsonOptions);
        }
        catch
        {
            return await CreateJsonResponse(req, HttpStatusCode.BadRequest, new { message = "Invalid JSON." });
        }

        if (request is null || string.IsNullOrWhiteSpace(request.ReservationToken))
            return await CreateJsonResponse(req, HttpStatusCode.BadRequest,
                new { message = "ReservationToken is required." });

        try
        {
            var session = await _liveChatService.GetActiveSessionAsync(request.ReservationToken, ct);
            if (session is null)
                return await CreateJsonResponse(req, HttpStatusCode.NotFound,
                    new { message = "Session not found." });

            if (request.GuestAutoTranslateEnabled.HasValue)
                session.GuestAutoTranslateEnabled = request.GuestAutoTranslateEnabled.Value;

            if (request.PreferredLanguage is not null)
            {
                if (string.IsNullOrWhiteSpace(request.PreferredLanguage))
                {
                    session.PreferredLanguage = null;
                }
                else
                {
                    var lang = request.PreferredLanguage.Trim();
                    if (lang.Length > 10)
                        return await CreateJsonResponse(req, HttpStatusCode.BadRequest,
                            new { message = "PreferredLanguage is too long." });
                    try
                    {
                        CultureInfo.GetCultureInfo(lang);
                    }
                    catch (CultureNotFoundException)
                    {
                        return await CreateJsonResponse(req, HttpStatusCode.BadRequest,
                            new { message = "PreferredLanguage is not a valid language code." });
                    }

                    session.PreferredLanguage = lang;
                }
            }

            session.UpdatedAt = DateTime.UtcNow;
            await _liveChatService.UpdateSessionAsync(session, ct);

            return await CreateJsonResponse(req, HttpStatusCode.OK,
                new LiveChatSessionSettingsDto(session.GuestAutoTranslateEnabled, session.PreferredLanguage));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LiveChatSettings failed for token {Token}", request.ReservationToken);
            return await CreateJsonResponse(req, HttpStatusCode.InternalServerError,
                new { message = "Failed to update settings." });
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
