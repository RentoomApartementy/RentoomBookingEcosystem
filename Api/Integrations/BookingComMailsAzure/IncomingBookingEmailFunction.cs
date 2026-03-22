using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models.BookingCom;
using RentoomBooking.SharedClasses.Services.BookingCom;
using System.Net;
using System.Text.Json;

namespace RentoomBooking.Api.Integrations.BookingComMailsAzure;

public class IncomingBookingEmailFunction
{
    private const string ProcessingEnabledKey = "BookingCom:ReservationProcessingEnabled";

    private readonly ILogger<IncomingBookingEmailFunction> _logger;
    private readonly IConfiguration _configuration;
    private readonly IBookingComLogStore _bookingComLogStore;
    private readonly IBookingComIncomingEmailProcessor _bookingComIncomingEmailProcessor;

    public IncomingBookingEmailFunction(
        ILogger<IncomingBookingEmailFunction> logger,
        IConfiguration configuration,
        IBookingComLogStore bookingComLogStore,
        IBookingComIncomingEmailProcessor bookingComIncomingEmailProcessor)
    {
        _logger = logger;
        _configuration = configuration;
        _bookingComLogStore = bookingComLogStore;
        _bookingComIncomingEmailProcessor = bookingComIncomingEmailProcessor;
    }

    [Function("IncomingBookingEmailFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "mail/incoming")] HttpRequestData req)
    {
        var processingEnabled = _configuration.GetValue<bool>(ProcessingEnabledKey);
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        Guid? bookingComLogGuid = null;

        try
        {
            BookingComIncomingEmail? email;
            try
            {
                email = JsonSerializer.Deserialize<BookingComIncomingEmail>(
                    body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                var invalidPayloadEmail = new BookingComIncomingEmail { RawPayload = body };
                bookingComLogGuid = await _bookingComLogStore.CreateAsync(invalidPayloadEmail, null, processingEnabled);
                await AppendLogStepAsync(
                    bookingComLogGuid.Value,
                    "payload_invalid",
                    "Failed",
                    "Incoming Booking.com email payload contained invalid JSON.",
                    payload: new { Exception = ex.Message, Payload = body },
                    overallStatus: BookingComLogStatuses.Failed);

                _logger.LogWarning(ex, "Invalid JSON payload for Booking.com incoming email: {Payload}", body);
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    status = BookingComLogStatuses.Failed,
                    logId = bookingComLogGuid
                }));
                return bad;
            }

            email ??= new BookingComIncomingEmail();
            email.RawPayload = body;

            var result = await _bookingComIncomingEmailProcessor.ProcessAsync(email);
            bookingComLogGuid = result.BookingComLogGuid;

            var statusCode = string.Equals(result.Status, BookingComLogStatuses.Failed, StringComparison.OrdinalIgnoreCase)
                ? HttpStatusCode.InternalServerError
                : HttpStatusCode.OK;

            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                status = result.Status,
                logId = result.BookingComLogGuid,
                reservationId = result.ReservationId,
                reservationGuid = result.ReservationGuid,
                emailConfirmed = result.EmailConfirmed,
                messageId = result.MessageId,
                message = result.Message
            }));

            return response;
        }
        catch (Exception ex)
        {
            if (bookingComLogGuid.HasValue)
            {
                try
                {
                    await AppendLogStepAsync(
                        bookingComLogGuid.Value,
                        "function_failed",
                        "Failed",
                        "Incoming Booking.com email processing failed.",
                        payload: new { Exception = ex.Message },
                        overallStatus: BookingComLogStatuses.Failed);
                }
                catch (Exception logEx)
                {
                    _logger.LogWarning(logEx, "Failed to append Booking.com failure log step for {LogGuid}.", bookingComLogGuid.Value);
                }
            }

            _logger.LogError(ex, "Booking.com incoming email processing failed.");

            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            error.Headers.Add("Content-Type", "application/json");
            await error.WriteStringAsync(JsonSerializer.Serialize(new
            {
                status = BookingComLogStatuses.Failed,
                logId = bookingComLogGuid
            }));
            return error;
        }
    }

    private async Task AppendLogStepAsync(
        Guid bookingComLogGuid,
        string step,
        string status,
        string message,
        object? payload = null,
        string? overallStatus = null)
    {
        await _bookingComLogStore.AppendStepAsync(
            bookingComLogGuid,
            new BookingComLogStep
            {
                OccurredAtUtc = DateTime.UtcNow,
                Step = step,
                Status = status,
                Message = message,
                PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload)
            },
            overallStatus);
    }
}
