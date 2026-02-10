using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Models.Upsell;
using System;
using System.Linq;

namespace RentoomBooking.SharedClasses.Services.Upsell
{


    public interface IUpsellPurchasedSummaryService
    {
        Task<UpsellPurchasedSummaryDto> GetPurchasedSummaryAsync(Guid reservationGuid, CancellationToken cancellationToken = default);
    }

    public class UpsellPurchasedSummaryService : IUpsellPurchasedSummaryService
    {
        private readonly IUpsellOrderStore _upsellOrderStore;
        private const string DefaultQrPayloadTemplate = "https://booking.rentoom.pl/redeem?t={qr_token}";

        private readonly IConfiguration _configuration;
        ILogger<UpsellPurchasedSummaryService> _logger;
        public UpsellPurchasedSummaryService(IUpsellOrderStore upsellOrderStore, IConfiguration configuration, ILogger<UpsellPurchasedSummaryService> logger)
        {
            _upsellOrderStore = upsellOrderStore ?? throw new ArgumentNullException(nameof(upsellOrderStore));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<UpsellPurchasedSummaryDto> GetPurchasedSummaryAsync(Guid reservationGuid, CancellationToken cancellationToken = default)
        {
            var orders = await _upsellOrderStore.GetByReservationGuidAsync(reservationGuid, cancellationToken);
            var paidOrders = orders
                .Where(order => string.Equals(order.PaymentStatus, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
                .Where(order => order.Lines.Any(line => string.Equals(line.LineStatus, UpsellLineStatuses.Paid, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            return new UpsellPurchasedSummaryDto
            {
                ReservationGuid = reservationGuid,
                PurchasedUpsellsWithVouchers = paidOrders.SelectMany(po => po.Lines).ToList() /* paidOrders.Select(order => new UpsellPurchasedOrderDto
                {
                    UpsellOrderGuid = order.UpsellOrderGuid,
                    PaymentStatus = order.PaymentStatus,
                    PaidAtUtc = order.PaidAtUtc,
                    TotalGross = order.Lines.Sum(line => line.LineTotalGross),
                    Currency = order.Lines.FirstOrDefault()?.Currency ?? order.State.Request?.Currency ?? "PLN",
                    Lines = order.Lines
                        .Where(line => string.Equals(line.LineStatus, UpsellLineStatuses.Paid, StringComparison.OrdinalIgnoreCase))
                        .Select(line => {
                            
                            var upsell = new UpsellPurchasedLineDto
                            {
                                PartnerServiceId = line.PartnerServiceId,
                                TitleSnapshot = line.TitleSnapshot,
                                PricingModel = line.PricingModel,
                                Quantity = line.Quantity,
                                UnitPriceGross = line.UnitPriceGross,
                                Nights = line.Nights,
                                TotalGuests = line.TotalGuests,
                                LineTotalGross = line.LineTotalGross,
                                Currency = line.Currency,
                                LineStatus = line.LineStatus,
                                IsFreeUnlimitedUses = line.IsFreeUnlimitedUses,
                            };

                            upsell.Voucher = line.Voucher;
                            return upsell;
                        })
                        .ToList()
                }).ToList()*/
            };
        }



        private string? BuildQrPayloadUrl(string? qrToken, string? partnerPublicId)
        {
            if (string.IsNullOrWhiteSpace(qrToken))
            {
                return null;
            }

            var template = ResolveQrPayloadTemplate(partnerPublicId);
            if (string.IsNullOrWhiteSpace(template))
            {
                template = DefaultQrPayloadTemplate;
            }

            var encodedToken = Uri.EscapeDataString(qrToken.Trim());
            var url = template.Replace("{qr_token}", encodedToken, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(partnerPublicId))
            {
                url = url.Replace("{partnerPublicId}", Uri.EscapeDataString(partnerPublicId.Trim()), StringComparison.OrdinalIgnoreCase);
            }

            return url;
        }

        private string ResolveQrPayloadTemplate(string? partnerPublicId)
        {
            var templateWithPartner = _configuration["Upsell:QrPayloadUrlTemplateWithPartner"]
                ?? _configuration["UpsellQrPayloadUrlTemplateWithPartner"];
            var templateDefault = _configuration["Upsell:QrPayloadUrlTemplate"]
                ?? _configuration["UpsellQrPayloadUrlTemplate"];

            if (!string.IsNullOrWhiteSpace(partnerPublicId) && !string.IsNullOrWhiteSpace(templateWithPartner))
            {
                return templateWithPartner;
            }

            return !string.IsNullOrWhiteSpace(templateDefault) ? templateDefault : DefaultQrPayloadTemplate;
        }

    }
}
