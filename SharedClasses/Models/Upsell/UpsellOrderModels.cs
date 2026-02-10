using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Models;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Enums;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Models;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using System;
using System.Collections.Generic;

namespace RentoomBooking.SharedClasses.Models.Upsell
{
    public class UpsellOrderRequest
    {
        public Guid? ReservationGuid { get; set; }
        public int ApartmentId { get; set; }
        [JsonConverter(typeof(DateOnlyJsonConverter))]
        public DateOnly StartDate { get; set; }
        [JsonConverter(typeof(DateOnlyJsonConverter))]
        public DateOnly EndDate { get; set; }
        public int Adults { get; set; }
        public int Children { get; set; }
        public string Currency { get; set; } = "PLN";
        public string? SuccessUrl { get; set; }
        public string? ErrorUrl { get; set; }
        public UpsellBuyerDto Buyer { get; set; } = new();
        public List<UpsellOrderLineRequest> SelectedUpsells { get; set; } = new();
    }

    public class UpsellBuyerDto
    {
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
    }

    public class UpsellOrderLineRequest
    {
        public int PartnerServiceId { get; set; }
        public int Quantity { get; set; }
    }

    public class UpsellOrderState
    {
        public UpsellOrderRequest? Request { get; set; }
        public decimal UpsellsTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public string? PaymentRedirectUrl { get; set; }
    }

    public static class UpsellLineStatuses
    {
        public const string Pending = "Pending";
        public const string Paid = "Paid";
        public const string Cancelled = "Cancelled";
        public const string Refunded = "Refunded";
    }

    public static class UpsellVoucherStatuses
    {
        public const string Active = "Active";
        public const string Expired = "Expired";
        public const string Cancelled = "Cancelled";
        public const string Completed = "Completed";
    }

    public class UpsellOrderRecord
    {
        public Guid UpsellOrderGuid { get; set; }
        public UpsellOrderState State { get; set; } = new();
        public List<UpsellOrderLineRecord> Lines { get; set; } = new();
        public Guid? PaymentSessionGuid { get; set; }
        public string PaymentStatus { get; set; } = ReservationWorkflow.PaymentStatuses.None;
        public string? Provider { get; set; }
        public string? ProviderTransactionId { get; set; }
        public DateTime? PaidAtUtc { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UpsellOrderLineRecord
    {
        public Guid UpsellOrderLineGuid { get; set; }
        public Guid UpsellOrderGuid { get; set; }
        public int PartnerServiceId { get; set; }
        public string TitleSnapshot { get; set; } = string.Empty;
        public PartnerServicePricingModel PricingModel { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPriceGross { get; set; }
        public int Nights { get; set; }
        public int TotalGuests { get; set; }
        public decimal LineTotalGross { get; set; }
        public string Currency { get; set; } = "PLN";
        public string LineStatus { get; set; } = UpsellLineStatuses.Pending;
        public int? BitrixProductId { get; set; }
        public string? BitrixLineId { get; set; }
        public bool IsFreeUnlimitedUses { get; set; }        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public UpsellVoucherDto? Voucher { get; set; }
        public UpsellTileDto UpsellDefinitionSnapshot { get; set; } = new();
    }

    public class UpsellPaymentInitResult
    {
        public Guid UpsellOrderGuid { get; set; }
        public Guid PaymentSessionGuid { get; set; }
        public string ProviderTransactionId { get; set; } = string.Empty;
        public string RedirectUrl { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
    }

    public class UpsellWebhookDto
    {
        public Guid UpsellOrderGuid { get; set; }
        public Guid PaymentSessionGuid { get; set; }
        public string ProviderTransactionId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
