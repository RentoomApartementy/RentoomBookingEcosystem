using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models.BookingCom;
using RentoomBooking.SharedClasses.Services.BookingCom;
using System.Net;
using System.Text.Json;

namespace RentoomBooking.Api.Integrations.BookingComMailsAzure;

public class BackfillIncomingBookingEmailFunction
{
    private readonly ILogger<BackfillIncomingBookingEmailFunction> _logger;
    private readonly IBookingComBackfillImportBuilder _bookingComBackfillImportBuilder;
    private readonly IBookingComIncomingEmailProcessor _bookingComIncomingEmailProcessor;

    public BackfillIncomingBookingEmailFunction(
        ILogger<BackfillIncomingBookingEmailFunction> logger,
        IBookingComBackfillImportBuilder bookingComBackfillImportBuilder,
        IBookingComIncomingEmailProcessor bookingComIncomingEmailProcessor)
    {
        _logger = logger;
        _bookingComBackfillImportBuilder = bookingComBackfillImportBuilder;
        _bookingComIncomingEmailProcessor = bookingComIncomingEmailProcessor;
    }

    [Function("BackfillIncomingBookingEmailFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "mail/incoming/backfill")] HttpRequestData req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var reservationIds = ParseReservationIds(body);

        if (reservationIds.Count == 0)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            badRequest.Headers.Add("Content-Type", "application/json");
            await badRequest.WriteStringAsync(JsonSerializer.Serialize(new
            {
                status = "Failed",
                message = "Provide at least one reservation id in reservationIds."
            }));
            return badRequest;
        }

        var results = new List<BookingComBackfillItemResult>();

        foreach (var reservationId in reservationIds)
        {
            BookingComIncomingEmail email;
            BookingComEmailProcessingContext context;

            try
            {
                var preparedImport = await _bookingComBackfillImportBuilder.BuildAsync(reservationId);
                email = preparedImport.IncomingEmail;
                context = preparedImport.ProcessingContext;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to build enriched backfill email for reservation {ReservationId}. Falling back to skeletal payload.",
                    reservationId);

                email = BuildFallbackEmail(reservationId);
                context = new BookingComEmailProcessingContext
                {
                    ReservationId = reservationId,
                    Provider = "IDB_PANEL",
                    ProviderTransactionId = "IDB_PANEL_TRANSACTION",
                    IsSynthetic = true,
                    Source = "backfill"
                };
            }

            var result = await _bookingComIncomingEmailProcessor.ProcessAsync(email, context);
            results.Add(new BookingComBackfillItemResult
            {
                RequestedReservationId = reservationId,
                Subject = email.Subject ?? string.Empty,
                Provider = context.Provider,
                ProviderTransactionId = context.ProviderTransactionId,
                BookingComLogGuid = result.BookingComLogGuid,
                ReservationGuid = result.ReservationGuid,
                ReservationId = result.ReservationId,
                EmailConfirmed = result.EmailConfirmed,
                Status = result.Status,
                Message = result.Message,
                MessageId = result.MessageId
            });
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(new
        {
            requestedCount = reservationIds.Count,
            processedCount = results.Count,
            completedCount = results.Count(result => string.Equals(result.Status, BookingComLogStatuses.Completed, StringComparison.OrdinalIgnoreCase)),
            failedCount = results.Count(result => string.Equals(result.Status, BookingComLogStatuses.Failed, StringComparison.OrdinalIgnoreCase)),
            duplicateCount = results.Count(result => string.Equals(result.Status, BookingComLogStatuses.Duplicate, StringComparison.OrdinalIgnoreCase)),
            disabledCount = results.Count(result => string.Equals(result.Status, BookingComLogStatuses.Disabled, StringComparison.OrdinalIgnoreCase)),
            results
        }));

        return response;
    }

    private static List<int> ParseReservationIds(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new List<int>();
        }

        try
        {
            var request = JsonSerializer.Deserialize<BookingComBackfillRequest>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request?.ReservationIds?.Count > 0)
            {
                return request.ReservationIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();
            }

            var rawList = JsonSerializer.Deserialize<List<int>>(body);
            return rawList?
                .Where(id => id > 0)
                .Distinct()
                .ToList()
                ?? new List<int>();
        }
        catch (JsonException)
        {
            return new List<int>();
        }
    }

    private static BookingComIncomingEmail BuildFallbackEmail(int reservationId)
    {
        return new BookingComIncomingEmail
        {
            MessageId = $"backfill:{reservationId}:{Guid.NewGuid():N}",
            ReceivedDateTime = DateTime.UtcNow.ToString("O"),
            Subject = $"Rentoom - Przyjęto rezerwację nr {reservationId}",
            BodyHtml = JsonSerializer.Serialize(new
            {
                kind = "backfill",
                reservationId,
                synthetic = true,
                fallback = true
            }),
            RawPayload = JsonSerializer.Serialize(new
            {
                kind = "backfill",
                reservationId,
                fallback = true
            })
        };
    }
}
