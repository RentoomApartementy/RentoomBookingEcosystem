using RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Enums;
using System;
using System.Collections.Generic;

namespace RentoomBooking.SharedClasses.Models.Upsell
{
    public class UpsellTileDto
    {
        public string PartnerPublicId { get; set; } = string.Empty;
        public int PartnerServiceId { get; set; }
        public string PartnerServicePublicId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Currency { get; set; } = string.Empty;
        public decimal? Discount { get; set; }
        public Dictionary<string, string> BannerUrls { get; set; } = new();
        public bool StayBoundOnly { get; set; }
        public PartnerServicePricingModel PricingModel { get; set; }
        public PartnerServiceDiscountType PricingDiscountType { get; set; }
        public bool IsPersonalizable { get; set; }
    }

    public class UpsellSelectionDto
    {
        public string PartnerServiceId { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    public class UpsellQuoteDto
    {
        public string Currency { get; set; } = string.Empty;
        public decimal BaseAmount { get; set; }
        public decimal UpsellAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public List<UpsellQuoteLineItemDto> LineItems { get; set; } = new();
    }

    public class UpsellQuoteLineItemDto
    {
        public string PartnerServiceId { get; set; } = string.Empty;
        public string TitleSnapshot { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal { get; set; }
        public string? ServiceDateMode { get; set; }
    }

    public class UpsellPurchaseDto
    {
        public string PurchaseId { get; set; } = string.Empty;
        public string ReservationId { get; set; } = string.Empty;
        public string PartnerServiceId { get; set; } = string.Empty;
        public string PartnerPublicId { get; set; } = string.Empty;
        public string TitleSnapshot { get; set; } = string.Empty;
        public string PartnerNameSnapshot { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public int TotalVouchers { get; set; }
        public int RedeemedVouchers { get; set; }
    }

    public class UpsellVoucherDto
    {
        public string VoucherId { get; set; } = string.Empty;
        public string PurchaseId { get; set; } = string.Empty;
        public string CodeShort { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateOnly? ServiceDate { get; set; }
        public int Quantity { get; set; }
        public DateTime ValidFromUtc { get; set; }
        public DateTime ValidToUtc { get; set; }
    }
}
