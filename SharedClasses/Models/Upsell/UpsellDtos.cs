using RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Models;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Enums;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Models;
using RentoomBooking.SharedClasses.Models.Storage;
using RentoomBooking.SharedClasses.Models.Upsell.StayWell;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace RentoomBooking.SharedClasses.Models.Upsell
{
    public class UpsellTileDto
    {
        public string PartnerPublicId { get; set; } = string.Empty;
        public int PartnerServiceId { get; set; }
        public string PartnerServicePublicId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public string? LongDescription { get; set; }
        public string? Terms { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = string.Empty;
        public decimal? Discount { get; set; }
        public Dictionary<PartnerServiceBannerPlacementType, string> BannerUrls { get; set; } = new();
        public bool StayBoundOnly { get; set; }
        public PartnerServicePricingModel PricingModel { get; set; }
        public PartnerServiceDiscountType PricingDiscountType { get; set; }
        public bool IsPersonalizable { get; set; }
     
       // public PartnerServiceSnapshot PartnerServiceInfo { get; set; } 

        //TODO add Partner object dto the tile if needed, currently we only have PartnerPublicId which is not enough to get that information without another call to the database

    }

    public class UpsellVoucherDto
    {
        public Guid VoucherGuid { get; set; }
        public Guid OrderLineGuid { get; set; }
        public Guid ReservationGuid { get; set; }
        //public int PartnerServiceId { get; set; }
        public string CodeShort { get; set; } = string.Empty;
        public string? QrToken { get; set; }
        public int UsedCount { get; set; }
        public int? MaxUses { get; set; }
        public DateOnly ValidFrom { get; set; }
        public DateOnly ValidTo { get; set; }
        public string Status { get; set; } = string.Empty;
        //public string TitleSnapshot { get; set; } = string.Empty;
        //public string Currency { get; set; } = string.Empty;
    }

    public class RedeemResultDto
    {
        public bool Success { get; set; }
        public string? FailureReason { get; set; }
        public int UpdatedUsedCount { get; set; }
        public int? MaxUses { get; set; }
        public Guid ReservationGuid { get; set; }
        public int PartnerServiceId { get; set; }
        public string TitleSnapshot { get; set; } = string.Empty;
        public UpsellVoucherDto? Voucher { get; set; }
    }

    public class UpsellPurchasedSummaryDto
    {
        public Guid ReservationGuid { get; set; }
        public List<UpsellOrderLineRecord> PurchasedUpsellsWithVouchers { get; set; } = new();
    }

    public class UpsellPurchasedOrderDto
    {
        public Guid UpsellOrderGuid { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime? PaidAtUtc { get; set; }
        public decimal TotalGross { get; set; }
        public string Currency { get; set; } = "PLN";
        public List<UpsellOrderLineRecord> Lines { get; set; } = new();
    }

    public class UpsellPurchasedLineDto : UpsellOrderLineRecord
    {
      /*  public int PartnerServiceId { get; set; }
        public string TitleSnapshot { get; set; } = string.Empty;
        public PartnerServicePricingModel PricingModel { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPriceGross { get; set; }
        public int Nights { get; set; }
        public int TotalGuests { get; set; }
        public decimal LineTotalGross { get; set; }
        public string Currency { get; set; } = "PLN";
        public string LineStatus { get; set; } = string.Empty;
        public bool IsFreeUnlimitedUses { get; set; }
        public PurchasedVoucherDto? Voucher { get; set; } = new();
      */
    }

    public class PurchasedVoucherDto :UpsellVoucherDto
    {
       // public string CodeShort { get; set; } = string.Empty;
        public string? QrPayloadUrl { get; set; }
      //  public int UsedCount { get; set; }
      //  public int? MaxUses { get; set; }
       // public DateOnly ValidFrom { get; set; }
      //  public DateOnly ValidTo { get; set; }
        //public string Status { get; set; } = string.Empty;
    }

}
