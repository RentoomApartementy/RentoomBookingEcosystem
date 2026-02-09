using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Models.Upsell;
using RentoomBooking.SharedClasses.Models.Upsell.StayWell;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;
using RentoomBooking.SharedClasses.Services.Upsell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.Api.Upsell
{
    public class GetPurchasedUpsellsWithVouchersFunction
    {
        private const string DefaultQrPayloadBaseUrl = "https://booking.rentoom.pl/redeem?t={qr_token}";
        private const string DefaultQrPayloadWithPartnerBaseUrl = "https://booking.rentoom.pl/p/{partnerPublicId}/redeem?t={qr_token}";

        private readonly IUpsellVoucherQueryService _voucherQueryService;
        private readonly IUpsellOrderStore _upsellOrderStore;
        private readonly IReservationStore _reservationStore;
        private readonly PostgresBookingDatabase _bookingDatabase;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GetPurchasedUpsellsWithVouchersFunction> _logger;

        public GetPurchasedUpsellsWithVouchersFunction(
            IUpsellVoucherQueryService voucherQueryService,
            IUpsellOrderStore upsellOrderStore,
            IReservationStore reservationStore,
            PostgresBookingDatabase bookingDatabase,
            IConfiguration configuration,
            ILogger<GetPurchasedUpsellsWithVouchersFunction> logger)
        {
            _voucherQueryService = voucherQueryService ?? throw new ArgumentNullException(nameof(voucherQueryService));
            _upsellOrderStore = upsellOrderStore ?? throw new ArgumentNullException(nameof(upsellOrderStore));
            _reservationStore = reservationStore ?? throw new ArgumentNullException(nameof(reservationStore));
            _bookingDatabase = bookingDatabase ?? throw new ArgumentNullException(nameof(bookingDatabase));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Function("GetPurchasedUpsellsWithVouchers")]
        public async Task<HttpResponseData> GetPurchasedUpsellsWithVouchers(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reservations/{reservationTokenGuid}/upsells/purchased")]
        HttpRequestData req,
            string reservationTokenGuid,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("GetPurchasedUpsellsWithVouchers started at: {time}", DateTime.UtcNow);
            var res = req.CreateResponse();

            try
            {
                if (string.IsNullOrWhiteSpace(reservationTokenGuid))
                {
                    res.StatusCode = HttpStatusCode.BadRequest;
                    await res.WriteStringAsync("Provide reservationTokenGuid in path (/reservations/{reservationTokenGuid}/upsells/purchased).", cancellationToken);
                    return res;
                }

                if (!Guid.TryParse(reservationTokenGuid, out var parsedTokenGuid))
                {
                    res.StatusCode = HttpStatusCode.BadRequest;
                    await res.WriteStringAsync("Reservation token must be a valid GUID.", cancellationToken);
                    return res;
                }

                var reservationGuid = await ResolveReservationGuidAsync(reservationTokenGuid, parsedTokenGuid, cancellationToken);
                var reservationRecord = await _reservationStore.GetAsync(reservationGuid, cancellationToken);
                if (reservationRecord is null)
                {
                    res.StatusCode = HttpStatusCode.NotFound;
                    await res.WriteStringAsync($"Reservation with token {reservationTokenGuid} not found.", cancellationToken);
                    return res;
                }

                var vouchers = await _voucherQueryService.GetByReservationAsync(reservationGuid);
                var orders = await _upsellOrderStore.GetByReservationGuidAsync(reservationGuid, cancellationToken);
                
                var paidOrders = orders
                    .Where(order => string.Equals(order.PaymentStatus, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var lineLookup = paidOrders
                    .SelectMany(order => order.Lines
                        .Where(line => string.Equals(line.LineStatus, UpsellLineStatuses.Paid, StringComparison.OrdinalIgnoreCase))
                        .Select(line => new
                        {
                            line.UpsellOrderLineGuid,
                            OrderPaidAtUtc = order.PaidAtUtc,
                            Line = line
                        }))
                    .ToDictionary(entry => entry.UpsellOrderLineGuid, entry => entry);

                var items = new List<PurchasedUpsellDto>();
                foreach (var voucher in vouchers)
                {
                    if (!lineLookup.TryGetValue(voucher.OrderLineGuid, out var lineEntry))
                    {
                        continue;
                    }

                    var qrPayloadUrl = BuildQrPayloadUrl(voucher.QrToken, partnerPublicId: null);
                    items.Add(new PurchasedUpsellDto
                    {
                        PartnerServiceId = lineEntry.Line.PartnerServiceId,
                        TitleSnapshot = lineEntry.Line.TitleSnapshot,
                        PricingModel = lineEntry.Line.PricingModel,
                        Quantity = lineEntry.Line.Quantity,
                        UnitPriceGross = lineEntry.Line.UnitPriceGross,
                        LineTotalGross = lineEntry.Line.LineTotalGross,
                        Currency = lineEntry.Line.Currency,
                        PaidAtUtc = lineEntry.OrderPaidAtUtc,
                        Voucher = new PurchasedVoucherDto
                        {
                            CodeShort = voucher.CodeShort,
                            QrPayloadUrl = qrPayloadUrl,
                            UsedCount = voucher.UsedCount,
                            MaxUses = voucher.MaxUses,
                            ValidFrom = voucher.ValidFrom,
                            ValidTo = voucher.ValidTo,
                            VoucherStatus = voucher.Status
                        }
                    });
                }

                var responseDto = new PurchasedUpsellsWithVouchersResponseDto
                {
                    ReservationGuid = reservationGuid,
                    Items = items
                };

                res.StatusCode = HttpStatusCode.OK;
                res.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await res.WriteStringAsync(JsonConvert.SerializeObject(responseDto), cancellationToken);
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during GetPurchasedUpsellsWithVouchers.");
                res.StatusCode = HttpStatusCode.InternalServerError;
                await res.WriteStringAsync("Internal server error.", cancellationToken);
                return res;
            }
            finally
            {
                _logger.LogInformation("GetPurchasedUpsellsWithVouchers finished at: {time}", DateTime.UtcNow);
            }
        }

        private async Task<Guid> ResolveReservationGuidAsync(string reservationTokenGuid, Guid parsedTokenGuid, CancellationToken cancellationToken)
        {
            var rentoomReservation = await _bookingDatabase.GetRentoomReservationByResTokenAsync(
                reservationTokenGuid,
                _logger,
                cancellationToken);

            return rentoomReservation?.Reservation?.RentoomReservationId ?? parsedTokenGuid;
        }

        private string? BuildQrPayloadUrl(string? qrToken, string? partnerPublicId)
        {
            if (string.IsNullOrWhiteSpace(qrToken))
            {
                return null;
            }

            var baseUrl = GetSetting("Upsell:VoucherQrPayloadUrlBase", "Upsell__VoucherQrPayloadUrlBase", "UpsellVoucherQrPayloadUrlBase")
                          ?? DefaultQrPayloadBaseUrl;
            var baseUrlWithPartner = GetSetting("Upsell:VoucherQrPayloadUrlWithPartnerBase", "Upsell__VoucherQrPayloadUrlWithPartnerBase", "UpsellVoucherQrPayloadUrlWithPartnerBase")
                                     ?? DefaultQrPayloadWithPartnerBaseUrl;

            var usePartner = !string.IsNullOrWhiteSpace(partnerPublicId);
            var template = usePartner ? baseUrlWithPartner : baseUrl;

            if (!usePartner && template.Contains("{partnerPublicId}", StringComparison.OrdinalIgnoreCase))
            {
                template = baseUrl;
            }

            return template
                .Replace("{qr_token}", Uri.EscapeDataString(qrToken), StringComparison.OrdinalIgnoreCase)
                .Replace("{partnerPublicId}", Uri.EscapeDataString(partnerPublicId ?? string.Empty), StringComparison.OrdinalIgnoreCase);
        }

        private string? GetSetting(string primaryKey, string envKey, string flatEnvKey)
        {
            return _configuration[primaryKey]
                   ?? Environment.GetEnvironmentVariable(envKey)
                   ?? Environment.GetEnvironmentVariable(flatEnvKey);
        }
    }
}
