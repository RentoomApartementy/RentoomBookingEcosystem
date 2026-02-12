using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Enums;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.Upsell;
using RentoomBooking.SharedClasses.Models.Upsell.StayWell;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using RentoomBooking.SharedClasses.Services.Upsell;
using System;
using System.Net;

namespace RentoomBooking.Api.Upsell;

public class GetReservationUpsellsFunction
{
    private readonly IUpsellPurchasedSummaryService _upsellPurchasedSummaryService;
    private readonly IUpsellCatalogService _upsellCatalogService;
    private readonly PostgresBookingDatabase _bookingDatabase;
    private readonly IUpsellOrderStore _upsellOrderStore;
    private readonly ILogger<GetReservationUpsellsFunction> _logger;

    public GetReservationUpsellsFunction(IUpsellPurchasedSummaryService upsellPurchasedSummaryService, IUpsellCatalogService upsellCatalogService, PostgresBookingDatabase bookingDatabase, IUpsellOrderStore upsellOrderStore, ILogger<GetReservationUpsellsFunction> logger)
    {
        _upsellPurchasedSummaryService = upsellPurchasedSummaryService ?? throw new ArgumentNullException(nameof(upsellPurchasedSummaryService));
        _upsellCatalogService = upsellCatalogService ?? throw new ArgumentNullException(nameof(upsellCatalogService));
        _bookingDatabase = bookingDatabase ?? throw new ArgumentNullException(nameof(bookingDatabase));
        _upsellOrderStore = upsellOrderStore ?? throw new ArgumentNullException(nameof(_upsellOrderStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("GetReservationPurchasedUpsellsByToken")]
    public async Task<HttpResponseData> GetReservationUpsellsByToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "db/reservations/{reservationToken}/upsells/purchased")] HttpRequestData req,
        string reservationToken,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("GetReservationUpsellsByToken started at: {time}", DateTime.UtcNow);
        var res = req.CreateResponse();

        try
        {
            if (string.IsNullOrWhiteSpace(reservationToken))
            {
                res.StatusCode = System.Net.HttpStatusCode.BadRequest;
                await res.WriteStringAsync("Provide reservationToken in path (/reservations/{reservationToken}/upsells/purchased).", cancellationToken);
                return res;
            }

            if (!Guid.TryParse(reservationToken, out var reservationGuid))
            {
                res.StatusCode = System.Net.HttpStatusCode.BadRequest;
                await res.WriteStringAsync("Reservation token must be a valid GUID.", cancellationToken);
                return res;
            }

            var responseDto = await _upsellPurchasedSummaryService.GetPurchasedSummaryAsync(reservationGuid, cancellationToken);

            res.StatusCode = System.Net.HttpStatusCode.OK;
            res.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await res.WriteStringAsync(JsonConvert.SerializeObject(responseDto), cancellationToken);
            return res;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during GetReservationUpsellsByToken.");
            res.StatusCode = System.Net.HttpStatusCode.InternalServerError;
            await res.WriteStringAsync("Internal server error.", cancellationToken);
            return res;
        }
        finally
        {
            _logger.LogInformation("GetReservationUpsellsByToken finished at: {time}", DateTime.UtcNow);
        }
    }

    [Function("GetReservationAvailableUpsells")]
    public async Task<HttpResponseData> GetAvailable(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "db/reservations/{reservationTokenGuid}/upsells/available")] HttpRequestData req,
           string reservationTokenGuid,
           CancellationToken cancellationToken)
    {
        var response = req.CreateResponse();

        try
        {
            if (!Guid.TryParse(reservationTokenGuid, out var reservationGuid))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Reservation token must be a valid GUID.", cancellationToken);
                return response;
            }


            var reservation = await ResolveReservationAsync(reservationTokenGuid, reservationGuid, cancellationToken);
           
            if (reservation?.Reservation is null)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                await response.WriteStringAsync("Reservation not found.", cancellationToken);
                return response;
            }
            var reservationItem = reservation.Reservation.Items?.FirstOrDefault();
            
            var apartmentId = reservationItem.objectItemId;

            var locale = GetLocale(req, reservation); //<== sprawdza czy jezyk przychodzi z query stringa, a jak nie to bierze z danych rezerwacji, a jak tam nie ma to domyslnie "pl".

            //znajdz wszystkie dostepne upselle dla tego apartamentu i tego jezyka, z katalogu rentoomApp.
            var availableTiles = await _upsellCatalogService.GetUpsellTilesForApartmentAsync(apartmentId, locale, "staywell", cancellationToken);

            var orders = await _upsellOrderStore.GetByReservationGuidAsync(reservationGuid, cancellationToken);

            HashSet<int> alreadyPurchasedServiceIds = orders
                .Where(order => string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                .SelectMany(order => order.Lines)
                .Where(line => string.Equals(line.LineStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                .Select(line => line.PartnerServiceId)
                .ToHashSet();


            //jeśli upsell jest one-time, to pokazujemy go nawet jeśli został już zakupiony, bo może być kupiony tylko raz, ale nadal jest dostępny do zakupu (np masaż moze sobie dokupic).
            // przegadac z Bartkiem i Krystianem czy faktycznie pokazywać.
            var onlyAvailable = availableTiles
                .Where(tile => !alreadyPurchasedServiceIds.Contains(tile.PartnerServiceId) || tile.PricingModel == PartnerServicePricingModel.OneTime) 
                .ToList();


            var df = reservation.Reservation.ReservationDetails.getDateFrom();
            var dt = reservation.Reservation.ReservationDetails.getDateTo();
            var adults = reservation.Reservation.Items[0].numberOfAdults ?? 0;
            AvailableUpsellsResponseDto responseDto = new AvailableUpsellsResponseDto
            {
                Available = onlyAvailable,
                ReservationGuid = reservationGuid,
                Context =  new ReservationPricingContext
                {
                    StartDate = new DateOnly(df.Year, df.Month, df.Day),
                    EndDate = new DateOnly(dt.Year, dt.Month, dt.Day),
                    Adults = reservation.Reservation.Items[0].numberOfAdults ?? 0,
                    Children = int.TryParse(reservation.Reservation.Items[0].numberOfSmallChildren, out var children) ? children : 0,
                    Currency =  "PLN"
                }

            };

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(responseDto), cancellationToken);
            return response;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while getting available upsells for reservation token {ReservationTokenGuid}.", reservationTokenGuid);
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.", cancellationToken);
            return response;
        }
    }

    private async Task<RentoomReservation?> ResolveReservationAsync(string providedToken, Guid reservationGuid, CancellationToken cancellationToken)
    {
        var candidates = new[]
        {
            providedToken,
            reservationGuid.ToString("D"),
            reservationGuid.ToString("N")
        }.Distinct(StringComparer.OrdinalIgnoreCase); //fix: na rozne wersje zapisu tokenu guid.

        foreach (var candidate in candidates)
        {
            var reservation = await _bookingDatabase.GetRentoomReservationByResTokenAsync(candidate, _logger, cancellationToken);
            if (reservation is not null)
            {
                return reservation;
            }
        }

        return null;
    }

    private static string GetLocale(HttpRequestData req, RentoomReservation reservation)
    {
        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var locale = queryParams.Get("locale") ?? queryParams.Get("culture");
        if (!string.IsNullOrWhiteSpace(locale))
        {
            return locale;
        }

        return string.IsNullOrWhiteSpace(reservation.Reservation.Client?.Language)
            ? "pl"
            : reservation.Reservation.Client.Language;
    }


}


/* private async Task LoadUpsellsAsync()
    {
        if (_apartment is null)
        {
            _availableUpsells = Array.Empty<UpsellTileDto>();
            return;
        }

        try
        {
            _availableUpsells = await UpsellCatalogService.GetUpsellTilesForApartmentAsync(
                _apartment.Id,
                CultureInfo.CurrentUICulture.Name,
                "rentoombooking");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading upsells: {ex.Message}");
            _availableUpsells = Array.Empty<UpsellTileDto>();
        }
    }*/