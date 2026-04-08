using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models.BookingCom;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.BookingCom;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace RentoomBooking.Api.Integrations.BookingComMailsAzure;

public class BackfillIncomingBookingEmailFromIdoAddDateRangeFunction
{
    private static readonly string[] SupportedDateFormats = ["yyyy-MM-dd HH:mm", "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssZ", "O"];

    private readonly ILogger<BackfillIncomingBookingEmailFromIdoAddDateRangeFunction> _logger;
    private readonly IdoSellService _idoSellService;
    private readonly IBookingComBackfillImportBuilder _bookingComBackfillImportBuilder;
    private readonly IBookingComIncomingEmailProcessor _bookingComIncomingEmailProcessor;

    public BackfillIncomingBookingEmailFromIdoAddDateRangeFunction(
        ILogger<BackfillIncomingBookingEmailFromIdoAddDateRangeFunction> logger,
        IdoSellService idoSellService,
        IBookingComBackfillImportBuilder bookingComBackfillImportBuilder,
        IBookingComIncomingEmailProcessor bookingComIncomingEmailProcessor)
    {
        _logger = logger;
        _idoSellService = idoSellService;
        _bookingComBackfillImportBuilder = bookingComBackfillImportBuilder;
        _bookingComIncomingEmailProcessor = bookingComIncomingEmailProcessor;
    }

    [Function("BackfillIncomingBookingEmailFromIdoAddDateRangeFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "mail/incoming/backfill/ido/add-date-range")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse();

        try
        {
            var request = await ParseRequestAsync(req, cancellationToken);
            if (request is null)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    status = "Failed",
                    message = "Provide startDate and endDate in format yyyy-MM-dd HH:mm."
                }), cancellationToken);
                return response;
            }

            if (request.EndDate.Date < request.StartDate.Date)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    status = "Failed",
                    message = "endDate must be greater than or equal to startDate."
                }), cancellationToken);
                return response;
            }

            var execution = await ExecuteAsync(request, cancellationToken);

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                startDate = request.StartDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                endDate = request.EndDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                fetchedCount = execution.FetchedCount,
                requestedCount = execution.ReservationIds.Count,
                processedCount = execution.Results.Count,
                completedCount = execution.Results.Count(result => string.Equals(result.Status, BookingComLogStatuses.Completed, StringComparison.OrdinalIgnoreCase)),
                failedCount = execution.Results.Count(result => string.Equals(result.Status, BookingComLogStatuses.Failed, StringComparison.OrdinalIgnoreCase)),
                duplicateCount = execution.Results.Count(result => string.Equals(result.Status, BookingComLogStatuses.Duplicate, StringComparison.OrdinalIgnoreCase)),
                disabledCount = execution.Results.Count(result => string.Equals(result.Status, BookingComLogStatuses.Disabled, StringComparison.OrdinalIgnoreCase)),
                reservationIds = execution.ReservationIds,
                results = execution.Results
            }), cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Booking.com backfill build from IdoBooking addDateRange.");
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.", cancellationToken);
            return response;
        }
    }

    [Function("BackfillIncomingBookingEmailFromIdoAddDateRangeCron")]
    [FixedDelayRetry(5, "00:00:10")]
    public async Task RunCron(
        [TimerTrigger("%CRON_SYNC_DAILY_RESERVATIONS%")] TimerInfo timerInfo,
        FunctionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var request = CreateDefaultRequest();
        var execution = await ExecuteAsync(request, cancellationToken);

        _logger.LogInformation(
            "Completed Booking.com backfill from IdoBooking addDateRange. StartDate={StartDate}, EndDate={EndDate}, Fetched={FetchedCount}, Processed={ProcessedCount}, Completed={CompletedCount}, Failed={FailedCount}, NextRun={NextRun}.",
            request.StartDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            request.EndDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            execution.FetchedCount,
            execution.Results.Count,
            execution.Results.Count(result => string.Equals(result.Status, BookingComLogStatuses.Completed, StringComparison.OrdinalIgnoreCase)),
            execution.Results.Count(result => string.Equals(result.Status, BookingComLogStatuses.Failed, StringComparison.OrdinalIgnoreCase)),
            timerInfo.ScheduleStatus?.Next);
    }

    private async Task<BackfillExecutionResult> ExecuteAsync(AddDateRangeRequest request, CancellationToken cancellationToken)
    {
        var reservations = await _idoSellService.FetchReservationByAddDateRangeFromIdoSellAsync(
            request.StartDate,
            request.EndDate,
            cancellationToken);

        var reservationIds = reservations
            .Select(reservation => reservation.id)
            .Where(reservationId => reservationId > 0)
            .Distinct()
            .ToList();

        var results = new List<BookingComBackfillItemResult>();

        foreach (var reservationId in reservationIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            BookingComIncomingEmail email;
            BookingComEmailProcessingContext context;

            try
            {
                var preparedImport = await _bookingComBackfillImportBuilder.BuildAsync(reservationId, cancellationToken);
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

        return new BackfillExecutionResult
        {
            FetchedCount = reservations.Count,
            ReservationIds = reservationIds,
            Results = results
        };
    }

    private static async Task<AddDateRangeRequest?> ParseRequestAsync(HttpRequestData req, CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var startDateRaw = query["startDate"] ?? query["from"];
        var endDateRaw = query["endDate"] ?? query["to"];

        if (string.IsNullOrWhiteSpace(startDateRaw) || string.IsNullOrWhiteSpace(endDateRaw))
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(body))
            {
                var bodyRequest = JsonSerializer.Deserialize<AddDateRangeRequestDto>(
                    body,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                startDateRaw ??= bodyRequest?.StartDate;
                endDateRaw ??= bodyRequest?.EndDate;
            }
        }

        if (string.IsNullOrWhiteSpace(startDateRaw) && string.IsNullOrWhiteSpace(endDateRaw))
        {
            return CreateDefaultRequest();
        }

        if (!TryParseDate(startDateRaw, out var startDate) || !TryParseDate(endDateRaw, out var endDate))
        {
            return null;
        }

        return new AddDateRangeRequest
        {
            StartDate = startDate,
            EndDate = endDate
        };
    }

    private static bool TryParseDate(string? value, out DateTime date)
    {
        if (DateTime.TryParseExact(
            value,
            SupportedDateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out date))
        {
            return true;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date))
        {
            return true;
        }

        return false;
    }

    private static DateTime GetNowInWarsaw()
    {
        var utcNow = DateTime.UtcNow;

        foreach (var timeZoneId in new[] { "Europe/Warsaw", "Central European Standard Time" })
        {
            try
            {
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                return TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return utcNow;
    }

    private sealed class AddDateRangeRequestDto
    {
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
    }

    private sealed class AddDateRangeRequest
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    private sealed class BackfillExecutionResult
    {
        public int FetchedCount { get; set; }
        public List<int> ReservationIds { get; set; } = new();
        public List<BookingComBackfillItemResult> Results { get; set; } = new();
    }

    private static AddDateRangeRequest CreateDefaultRequest()
    {
        var nowInWarsaw = GetNowInWarsaw();
        return new AddDateRangeRequest
        {
            StartDate = nowInWarsaw.AddMinutes(-30),
            EndDate = nowInWarsaw.AddMinutes(30)
        };
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
