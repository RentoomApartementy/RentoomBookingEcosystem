using RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.Upsell.StayWell
{
    public class AvailableUpsellsResponseDto
    {
        public Guid ReservationGuid { get; set; }
        public ReservationPricingContext Context { get; set; } = new();
        public List<UpsellOfferDto> Available { get; set; } = new();
        public Dictionary<int, int>? AlreadyPurchased { get; set; }
    }

    public class UpsellOfferDto : UpsellTileDto
    {
        public bool CanBuyAgain { get; set; }
        public int AlreadyPurchasedCount { get; set; }
    }

    public class PurchasedUpsellsWithVouchersResponseDto
    {
        public Guid ReservationGuid { get; set; }
        public List<PurchasedUpsellDto> Items { get; set; } = new();
    }

    public class PurchasedUpsellDto
    {
        public int PartnerServiceId { get; set; }
        public string TitleSnapshot { get; set; } = string.Empty;
        public PartnerServicePricingModel PricingModel { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPriceGross { get; set; }
        public decimal LineTotalGross { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime? PaidAtUtc { get; set; }
        public PurchasedVoucherDto Voucher { get; set; } = new();
    }

    public class PurchasedVoucherDto
    {
        public string CodeShort { get; set; } = string.Empty;
        public string? QrPayloadUrl { get; set; }
        public int UsedCount { get; set; }
        public int? MaxUses { get; set; }
        public DateOnly ValidFrom { get; set; }
        public DateOnly ValidTo { get; set; }
        public string VoucherStatus { get; set; } = string.Empty;
    }
}
