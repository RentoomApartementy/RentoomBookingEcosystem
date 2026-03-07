using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace RentoomBooking.Api;

public class EditReservationFunction
{
    private readonly IdoSellService _idoSellService;
    private readonly PostgresBookingDatabase _bookingDatabase;
    private readonly ILogger<EditReservationFunction> _logger;

    public EditReservationFunction(IdoSellService idoSellService, PostgresBookingDatabase bookingDatabase, ILogger<EditReservationFunction> logger)
    {
        _idoSellService = idoSellService ?? throw new ArgumentNullException(nameof(idoSellService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _bookingDatabase = bookingDatabase ?? throw new ArgumentNullException(nameof(bookingDatabase));
    }

    [Function("EditReservationsInIdoSell")]
    public async Task<HttpResponseData> EditReservationsInIdoSell(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ido/reservations/edit")] HttpRequestData req)
    {
        _logger.LogInformation("EditReservationsInIdoSell started at: {time}", DateTime.UtcNow);
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
                await response.WriteStringAsync("Request body is empty.");
                return response;
            }

            var request = JsonConvert.DeserializeObject<ReservationEditParams>(body);

            if (request == null || request.Reservations.Count == 0)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Provide at least one reservation in the reservations array.");
                return response;
            }

            var result = await _idoSellService.EditReservationsAsync(request.Reservations);

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(result));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during EditReservationsInIdoSell.");
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.");
            return response;
        }
        finally
        {
            _logger.LogInformation("EditReservationsInIdoSell finished at: {time}", DateTime.UtcNow);
        }
    }


    [Function("EditReservationsInIdoSellByToken")]
    public async Task<HttpResponseData> EditReservationsInIdoSellByToken(
       [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "reservation/{reservationToken}/edit")] HttpRequestData req,
       string reservationToken,
       CancellationToken cancellationToken)
    {
        _logger.LogInformation("EditReservationsInIdoSell started at: {time}", DateTime.UtcNow);
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
                await response.WriteStringAsync("Request body is empty.");
                return response;
            }

            if (string.IsNullOrWhiteSpace(reservationToken))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Reservation token is required.");
                return response;
            }

            _ = Guid.TryParse(reservationToken, out var reservationGuidFromToken);

            var reservation = await ResolveReservationAsync(reservationToken, reservationGuidFromToken, cancellationToken);

            if (reservation?.Reservation is null)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                await response.WriteStringAsync("Reservation not found.", cancellationToken);
                return response;
            }

            if (!Guid.TryParse(reservation.ResToken, out var reservationGuid))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Reservation token must resolve to a GUID.", cancellationToken);
                return response;
            }

            var request = JsonConvert.DeserializeObject<ReservationEditParams>(body);

            if (request == null || request.Reservations.Count == 0)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Provide at least one reservation in the reservations array.");
                return response;
            }

            var result = await _idoSellService.EditReservationsAsync(request.Reservations);

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(result));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during EditReservationsInIdoSell.");
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.");
            return response;
        }
        finally
        {
            _logger.LogInformation("EditReservationsInIdoSell finished at: {time}", DateTime.UtcNow);
        }
    }


    private async Task<RentoomReservation?> ResolveReservationAsync(string providedToken, Guid reservationGuid, CancellationToken cancellationToken)
    {
        var candidates = new[]
        {
            providedToken,
            reservationGuid == Guid.Empty ? null : reservationGuid.ToString("D"),
            reservationGuid == Guid.Empty ? null : reservationGuid.ToString("N")
        }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            var reservation = await _bookingDatabase.GetRentoomReservationByResTokenAsync(candidate!, _logger, cancellationToken);
            if (reservation is not null)
            {
                return reservation;
            }
        }

        return null;
    }

    private static async Task<T?> DeserializeAsync<T>(HttpRequestData req, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(req.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(body)
            ? default
            : JsonConvert.DeserializeObject<T>(body);
    }

}