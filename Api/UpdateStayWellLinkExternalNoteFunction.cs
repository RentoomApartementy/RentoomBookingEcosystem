using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using System.Net;

namespace RentoomBooking.Api;

public class UpdateStayWellLinkExternalNoteFunction
{
    private readonly IdoSellService _idoSellService;
    private readonly PostgresBookingDatabase _bookingDatabase;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UpdateStayWellLinkExternalNoteFunction> _logger;

    public UpdateStayWellLinkExternalNoteFunction(
        IdoSellService idoSellService,
        PostgresBookingDatabase bookingDatabase,
        IConfiguration configuration,
        ILogger<UpdateStayWellLinkExternalNoteFunction> logger)
    {
        _idoSellService = idoSellService ?? throw new ArgumentNullException(nameof(idoSellService));
        _bookingDatabase = bookingDatabase ?? throw new ArgumentNullException(nameof(bookingDatabase));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdateStayWellLinkExternalNote")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "ido/reservations/external-note/staywell")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse();

        try
        {
            string body;
            using (var reader = new StreamReader(req.Body))
            {
                body = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Request body is empty.", cancellationToken);
                return response;
            }

            var request = JsonConvert.DeserializeObject<StayWellExternalNoteBatchRequest>(body);
            var reservationIds = request?.ReservationIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (reservationIds is null || reservationIds.Count == 0)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Provide at least one reservation id in reservationIds.", cancellationToken);
                return response;
            }

            var results = new List<StayWellExternalNoteBatchResult>();

            foreach (var reservationId in reservationIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var idoRecord = await _idoSellService.FetchReservationByIDFromIdoSellAsync(
                        reservationId,
                        false,
                        cancellationToken: cancellationToken);

                    var reservation = idoRecord.ReservationResponse?.result?.Reservations?.FirstOrDefault();
                    if (reservation is null)
                    {
                        throw new InvalidOperationException($"Reservation {reservationId} was not found in IdoBooking.");
                    }

                    var rentoomReservation = await _bookingDatabase.GetRentoomReservationByReservationIdAsync(
                        reservationId,
                        _logger,
                        cancellationToken);

                    var resToken = ResolveReservationToken(rentoomReservation, reservation);
                    if (string.IsNullOrWhiteSpace(resToken))
                    {
                        throw new InvalidOperationException($"Reservation {reservationId} does not have a StayWell token.");
                    }

                    var stayWellLink = BuildStayWellLink(resToken);
                    if (string.IsNullOrWhiteSpace(stayWellLink))
                    {
                        throw new InvalidOperationException("StayWell reservation base url is not configured.");
                    }

                    reservation.ReservationDetails ??= new ReservationDetails();
                    var existingExternalNote = reservation.ReservationDetails.externalNote ?? string.Empty;
                    var updatedExternalNote = BuildExternalNote(stayWellLink, existingExternalNote);

                    var editResponse = await _idoSellService.EditReservationAsync(
                        new EditReservation
                        {
                            Id = reservationId,
                            ExternalNote = updatedExternalNote,
                            Notify = ReservationNotifyType.No,
                            NotifyService = ReservationNotifyType.No
                        },
                        cancellationToken);

                    var editResult = editResponse?.Reservations?.FirstOrDefault();
                    if (editResult?.Success != true)
                    {
                        var errorMessage = editResult?.Error?.FaultString
                            ?? editResponse?.Errors?.FaultString
                            ?? $"Unknown error while updating reservation {reservationId}.";

                        throw new InvalidOperationException(errorMessage);
                    }

                    results.Add(new StayWellExternalNoteBatchResult
                    {
                        ReservationId = reservationId,
                        Success = true,
                        ResToken = resToken,
                        StayWellLink = stayWellLink
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update ExternalNote for reservation {ReservationId}.", reservationId);

                    results.Add(new StayWellExternalNoteBatchResult
                    {
                        ReservationId = reservationId,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(new
            {
                requestedCount = reservationIds.Count,
                updatedCount = results.Count(r => r.Success),
                failedCount = results.Count(r => !r.Success),
                results
            }), cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during UpdateStayWellLinkExternalNote.");
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.", cancellationToken);
            return response;
        }
    }

    private string? BuildStayWellLink(string resToken)
    {
        var baseUrl =
            Environment.GetEnvironmentVariable("StayWell__ReservationUrlBase") ??
            Environment.GetEnvironmentVariable("StayWellReservationUrlBase") ??
            _configuration["StayWell:ReservationUrlBase"] ??
            _configuration["StayWellReservationUrlBase"];

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(resToken))
        {
            return null;
        }

        return baseUrl.Replace("{resToken}", resToken).TrimEnd('/');
    }

    private static string? ResolveReservationToken(RentoomReservation? rentoomReservation, Reservation reservation)
    {
        if (!string.IsNullOrWhiteSpace(rentoomReservation?.ResToken))
        {
            return rentoomReservation.ResToken;
        }

        return reservation.RentoomReservationId?.ToString("D");
    }

    private static string BuildExternalNote(string stayWellLink, string? existingExternalNote)
    {
        var notePrefix = $"{stayWellLink}";

        if (string.IsNullOrWhiteSpace(existingExternalNote))
        {
            return notePrefix;
        }

        if (existingExternalNote.Contains(notePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return existingExternalNote;
        }

        return $"{notePrefix}{Environment.NewLine}{Environment.NewLine}{existingExternalNote.Trim()}";
    }

    private sealed class StayWellExternalNoteBatchRequest
    {
        public List<int> ReservationIds { get; set; } = new();
    }

    private sealed class StayWellExternalNoteBatchResult
    {
        public int ReservationId { get; set; }
        public bool Success { get; set; }
        public string? ResToken { get; set; }
        public string? StayWellLink { get; set; }
        public string? Error { get; set; }
    }
}
