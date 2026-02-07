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

        public UpsellPurchasedSummaryService(IUpsellOrderStore upsellOrderStore)
        {
            _upsellOrderStore = upsellOrderStore ?? throw new ArgumentNullException(nameof(upsellOrderStore));
        }

        public async Task<UpsellPurchasedSummaryDto> GetPurchasedSummaryAsync(Guid reservationGuid, CancellationToken cancellationToken = default)
        {
            var orders = await _upsellOrderStore.GetByReservationGuidAsync(reservationGuid, cancellationToken);
            var paidOrders = orders
                .Where(order => string.Equals(order.PaymentStatus, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return new UpsellPurchasedSummaryDto
            {
                ReservationGuid = reservationGuid,
                Orders = paidOrders.Select(order => new UpsellPurchasedOrderDto
                {
                    UpsellOrderGuid = order.UpsellOrderGuid,
                    PaymentStatus = order.PaymentStatus,
                    PaidAtUtc = order.PaidAtUtc,
                    TotalGross = order.Lines.Sum(line => line.LineTotalGross),
                    Currency = order.Lines.FirstOrDefault()?.Currency ?? order.State.Request?.Currency ?? "PLN",
                    Lines = order.Lines
                        .Where(line => string.Equals(line.LineStatus, UpsellLineStatuses.Paid, StringComparison.OrdinalIgnoreCase))
                        .Select(line => new UpsellPurchasedLineDto
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
                            LineStatus = line.LineStatus
                        })
                        .ToList()
                }).ToList()
            };
        }
    }
}
