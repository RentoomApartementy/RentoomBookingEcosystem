using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models.BookingCom;
using RentoomBooking.SharedClasses.Services.BookingCom;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RentoomBooking.Api.Integrations.BookingComMailsAzure;

public class IncomingBookingEmailFunction
{
    private const string ProcessingEnabledKey = "BookingCom:ReservationProcessingEnabled";

    private readonly ILogger<IncomingBookingEmailFunction> _logger;
    private readonly IConfiguration _configuration;
    private readonly IBookingComLogStore _bookingComLogStore;
    private readonly IBookingComReservationWorkflowService _bookingComReservationWorkflowService;

    public IncomingBookingEmailFunction(
        ILogger<IncomingBookingEmailFunction> logger,
        IConfiguration configuration,
        IBookingComLogStore bookingComLogStore,
        IBookingComReservationWorkflowService bookingComReservationWorkflowService)
    {
        _logger = logger;
        _configuration = configuration;
        _bookingComLogStore = bookingComLogStore;
        _bookingComReservationWorkflowService = bookingComReservationWorkflowService;
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

            var subject = email.Subject?.Trim() ?? string.Empty;
            var reservationIdRaw = ExtractReservationId(subject);
            var reservationId = int.TryParse(reservationIdRaw, out var parsedReservationId) ? parsedReservationId : (int?)null;
            var bodyLen = email.BodyHtml?.Length ?? 0;
            var bodySnippet = SafeSnippet(email.BodyHtml, 120);

            _logger.LogInformation(
                "Incoming Booking.com email. MessageId={MessageId}; ReceivedDateTime={ReceivedDateTime}; ReservationId={ReservationId}; Subject=\"{Subject}\"; BodyHtmlLen={BodyHtmlLen}; BodySnippet=\"{BodySnippet}\"",
                email.MessageId,
                email.ReceivedDateTime,
                reservationId,
                subject,
                bodyLen,
                bodySnippet);

            bookingComLogGuid = await _bookingComLogStore.CreateAsync(email, reservationId, processingEnabled);
            await AppendLogStepAsync(
                bookingComLogGuid.Value,
                "email_received",
                "Completed",
                "Incoming Booking.com email was received.",
                payload: new
                {
                    reservationId,
                    Subject = subject,
                    BodyLength = bodyLen,
                    BodySnippet = bodySnippet
                },
                overallStatus: processingEnabled ? BookingComLogStatuses.Processing : BookingComLogStatuses.Disabled);

            if (!reservationId.HasValue)
            {
                await AppendLogStepAsync(
                    bookingComLogGuid.Value,
                    "reservation_id_missing",
                    "Ignored",
                    "No IdoBooking reservation id could be extracted from the incoming email subject.",
                    payload: new { Subject = subject },
                    overallStatus: BookingComLogStatuses.Ignored);

                var ignored = req.CreateResponse(HttpStatusCode.OK);
                ignored.Headers.Add("Content-Type", "application/json");
                await ignored.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    status = BookingComLogStatuses.Ignored,
                    logId = bookingComLogGuid,
                    messageId = email.MessageId
                }));
                return ignored;
            }

            if (!processingEnabled)
            {
                await AppendLogStepAsync(
                    bookingComLogGuid.Value,
                    "processing_disabled",
                    "Skipped",
                    "Booking.com reservation processing is switched off. Logged the incoming email only.",
                    payload: new { reservationId },
                    overallStatus: BookingComLogStatuses.Disabled);

                var disabled = req.CreateResponse(HttpStatusCode.OK);
                disabled.Headers.Add("Content-Type", "application/json");
                await disabled.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    status = BookingComLogStatuses.Disabled,
                    logId = bookingComLogGuid,
                    reservationId
                }));
                return disabled;
            }

            var existingGuid = await _bookingComReservationWorkflowService.CheckForDuplicate(reservationId.Value);
            
            if (existingGuid.HasValue)
            {
                await AppendLogStepAsync(
                                bookingComLogGuid.Value,
                                "processing",
                                "Skipped",
                                $"Reservation already exists in the system under {existingGuid}.",
                                payload: new { reservationId },
                                overallStatus: BookingComLogStatuses.Duplicate);

                var disabled = req.CreateResponse(HttpStatusCode.OK);
                disabled.Headers.Add("Content-Type", "application/json");
                await disabled.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    status = BookingComLogStatuses.Duplicate,
                    logId = bookingComLogGuid,
                    reservationId
                }));
                return disabled;
            }


            var result = await _bookingComReservationWorkflowService.ProcessIncomingReservationAsync(
                new BookingComReservationImportRequest
                {
                    BookingComLogGuid = bookingComLogGuid.Value,
                    ReservationId = reservationId.Value,
                    IncomingEmail = email
                });

            var ok = req.CreateResponse(HttpStatusCode.OK);
            ok.Headers.Add("Content-Type", "application/json");
            await ok.WriteStringAsync(JsonSerializer.Serialize(new
            {
                status = result.Status,
                logId = result.BookingComLogGuid,
                reservationId = result.ReservationId,
                reservationGuid = result.ReservationGuid,
                emailConfirmed = result.EmailConfirmed
            }));

            return ok;
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

    private static string? ExtractReservationId(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var match = Regex.Match(
            subject,
            @"\bnr\s+(?<id>\d+)\s*(?=\()",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (match.Success)
        {
            return match.Groups["id"].Value;
        }

        match = Regex.Match(subject, @"\bnr\s+(?<id>\d+)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (match.Success)
        {
            return match.Groups["id"].Value;
        }

        return null;
    }

    private static string SafeSnippet(string? html, int maxLen)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var compact = html.Replace("\r", " ").Replace("\n", " ").Trim();
        if (compact.Length <= maxLen)
        {
            return compact;
        }

        return compact.Substring(0, maxLen) + "...";
    }
}
