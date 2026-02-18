using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Integrations.Tpay.Models;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.Payments;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Models.Upsell;
using RentoomBooking.SharedClasses.Models.Upsell.StayWell;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using RentoomBooking.SharedClasses.Services.Payments;
using RentoomBooking.SharedClasses.Services.Upsell;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.Api.Upsell
{
    public class CreateUpsellOrderFunction
    {
        private readonly PostgresBookingDatabase _bookingDatabase;
        private readonly IUpsellCatalogService _upsellCatalogService;
        private readonly IUpsellOrderStore _upsellOrderStore;
        private readonly IPaymentOrchestrator _paymentOrchestrator;
        private readonly TpaySettings _tpaySettings;
        private readonly ILogger<CreateUpsellOrderFunction> _logger;

        public CreateUpsellOrderFunction(
            PostgresBookingDatabase bookingDatabase,
            IUpsellCatalogService upsellCatalogService,
            IUpsellOrderStore upsellOrderStore,
            IPaymentOrchestrator paymentOrchestrator,
            IOptions<TpaySettings> tpaySettings,
            ILogger<CreateUpsellOrderFunction> logger)
        {
            _bookingDatabase = bookingDatabase ?? throw new ArgumentNullException(nameof(bookingDatabase));
            _upsellCatalogService = upsellCatalogService ?? throw new ArgumentNullException(nameof(upsellCatalogService));
            _upsellOrderStore = upsellOrderStore ?? throw new ArgumentNullException(nameof(upsellOrderStore));
            _paymentOrchestrator = paymentOrchestrator ?? throw new ArgumentNullException(nameof(paymentOrchestrator));
            _tpaySettings = tpaySettings.Value ?? throw new ArgumentNullException(nameof(tpaySettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Function("CreateReservationUpsellOrderWithPaymentRedirectLink")]
        public async Task<HttpResponseData> CreateOrder(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "reservations/{reservationToken}/upsells/orders")] HttpRequestData req,
            string reservationToken,
            CancellationToken cancellationToken)
        {
            var response = req.CreateResponse();

            try
            {
                if (string.IsNullOrWhiteSpace(reservationToken))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Reservation token is required.", cancellationToken);
                    return response;
                }

                var payload = await DeserializeAsync<UpsellOrderRequest>(req, cancellationToken);
                if (payload is null)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Invalid payload.", cancellationToken);
                    return response;
                }

                if (payload.SelectedUpsells is null || payload.SelectedUpsells.Count == 0)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("At least one upsell line is required.", cancellationToken);
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

                var reservationItem = reservation.Reservation.Items?.FirstOrDefault();
                if (reservationItem is null)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Reservation item is missing.", cancellationToken);
                    return response;
                }

                var locale = reservation.Reservation.Client?.Language ?? "en-EN";

                var catalog = await _upsellCatalogService.GetUpsellTilesForApartmentAsync(reservationItem.objectItemId, locale, "staywell", cancellationToken);
                var catalogByServiceId = catalog.ToDictionary(x => x.PartnerServiceId);

                var lines = payload.SelectedUpsells;

                /*var invalidServiceIds = lines
                    .Where(line => !catalogByServiceId.ContainsKey(line.PartnerServiceId))
                    .Select(line => line.PartnerServiceId)
                    .ToList();

                if (invalidServiceIds.Count > 0)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync($"Upsell service ids are not purchasable for this reservation: {string.Join(",", invalidServiceIds)}.", cancellationToken);
                    return response;
                }*/

                // var dateFrom = reservation.Reservation.ReservationDetails.getDateFrom();
                // var dateTo = reservation.Reservation.ReservationDetails.getDateTo();

                var context = new ReservationPricingContext
                {
                    StartDate = payload.StartDate,// new DateOnly(dateFrom.Year, dateFrom.Month, dateFrom.Day),
                    EndDate = payload.EndDate, //new DateOnly(dateTo.Year, dateTo.Month, dateTo.Day),
                    Adults = payload.Adults,// reservationItem.numberOfAdults ?? 0,
                    Children = payload.Children, //int.TryParse(reservationItem.numberOfSmallChildren, out var children) ? children : 0,
                    Currency = "PLN"
                };



                /*var orderRequest = new UpsellOrderRequest
              {
                  ReservationGuid = reservationGuid,
                  ApartmentId = reservationItem.objectItemId,
                  StartDate = context.StartDate,
                  EndDate = context.EndDate,
                  Adults = context.Adults,
                  Children = context.Children,
                  Currency = context.Currency,
                  Buyer = new UpsellBuyerDto
                  {
                      Email = payload.Buyer?.Email ?? string.Empty,
                      Name = payload.Buyer?.Name ?? string.Empty,
                      Phone = payload.Buyer?.Phone
                  },
                  SuccessUrl = payload.SuccessUrl,
                  ErrorUrl = payload.ErrorUrl,
                  SelectedUpsells = lines
              };*/

                UpsellOrderRequest orderRequest = payload;

                var lineRecords = new List<UpsellOrderLineRecord>();
                decimal totalGross = 0m;

                foreach (var line in lines)
                {
                    var tile = catalogByServiceId[line.PartnerServiceId];
                    var lineTotal = UpsellPricingCalculator.CalculateTotal(tile.PricingModel, tile.Price, context.Nights, context.TotalGuests, line.Quantity);

                    lineRecords.Add(new UpsellOrderLineRecord
                    {
                        UpsellOrderLineGuid = Guid.NewGuid(),
                        PartnerServiceId = tile.PartnerServiceId,
                        TitleSnapshot = tile.Title,
                        PricingModel = tile.PricingModel,
                        Quantity = line.Quantity,
                        UnitPriceGross = tile.Price,
                        Nights = context.Nights,
                        TotalGuests = context.TotalGuests,
                        LineTotalGross = lineTotal,
                        Currency = context.Currency,
                        LineStatus = UpsellLineStatuses.Pending,
                        UpsellDefinitionSnapshot = tile,
                       
                    });

                    totalGross += lineTotal;
                }

                var record = await _upsellOrderStore.CreateWithLinesAsync(orderRequest, lineRecords, cancellationToken);

                record.State.UpsellsTotal = totalGross;
                record.State.GrandTotal = totalGross;

                record.State.Request.NotificationUrl = _tpaySettings.NotificationUrl; //.Replace("UpsellOrderGuid", record.UpsellOrderGuid.ToString());
                record.State.Request.SuccessUrl = payload.SuccessUrl + "/"+ _tpaySettings.SuccessUrl?.Replace("UpsellOrderGuid", record.UpsellOrderGuid.ToString()).Replace("{Token}",payload.ReservationGuid.ToString());
                record.State.Request.ErrorUrl = payload.ErrorUrl + "/" + _tpaySettings.ErrorUrl?.Replace("UpsellOrderGuid", record.UpsellOrderGuid.ToString()).Replace("{Token}", payload.ReservationGuid.ToString());

                await _upsellOrderStore.UpdateAsync(record, cancellationToken);

                   var payment = await _paymentOrchestrator.CreatePaymentAsync(new PaymentIntentRequest
                   {
                       FlowType = PaymentFlowType.Upsell,
                       OrderId = record.UpsellOrderGuid,
                       SuccessUrl = record.State.Request.SuccessUrl,//payload.SuccessUrl +"/"+ _tpaySettings.SuccessUrl?.Replace("UpsellOrderGuid", record.UpsellOrderGuid.ToString()),
                       ErrorUrl = record.State.Request.ErrorUrl,//payload.ErrorUrl + "/" +_tpaySettings.ErrorUrl?.Replace("UpsellOrderGuid", record.UpsellOrderGuid.ToString()),
                       NotificationUrl = record.State.Request.NotificationUrl, //_tpaySettings.NotificationUrl //payload.NotificationUrl.Replace("UpsellOrderGuid", record.UpsellOrderGuid.ToString())
                   });



                   var dto = new PayUpsellOrderResponse
                   {
                       UpsellOrderGuid = payment.OrderId,
                       PaymentStatus = PaymentStatuses.Initiated,
                       RedirectUrl = payment.RedirectUrl,
                       PaymentSessionGuid = payment.PaymentSessionGuid,
                       ProviderTransactionId = payment.ProviderTransactionId,
                       Provider = payment.Provider
                   };
                


               /* var upsellCreatedRecordResponse = new CreateUpsellOrderResponse()
                {
                    PaymentStatus = PaymentStatuses.None,
                    Currency = "PLN",
                    TotalGross = totalGross,
                    UpsellOrderGuid = record.UpsellOrderGuid,
                };*/

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(dto), cancellationToken);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create upsell order for reservation token {ReservationToken}.", reservationToken);
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("Internal server error. " + ex.Message, cancellationToken);
                return response;
            }
        }


        [Function("GetUpsellOrderStatus")]
        public async Task<HttpResponseData> GetUpsellOrderStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "upsells/orders/{upsellOrderGuid}/status")] HttpRequestData req,
        string upsellOrderGuid,
        CancellationToken cancellationToken)
        {
            var response = req.CreateResponse();

            try
            {
                if (!Guid.TryParse(upsellOrderGuid, out var orderGuid))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Upsell order guid must be a valid GUID.", cancellationToken);
                    return response;
                }

                var order = await _upsellOrderStore.GetAsync(orderGuid, cancellationToken);
                if (order is null)
                {
                    response.StatusCode = HttpStatusCode.NotFound;
                    await response.WriteStringAsync("Upsell order not found.", cancellationToken);
                    return response;
                }

                

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(order), cancellationToken);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while getting upsell order status for {UpsellOrderGuid}.", upsellOrderGuid);
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
    }