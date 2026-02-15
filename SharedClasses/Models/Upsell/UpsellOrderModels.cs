using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Models;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Enums;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Models;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
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
        public string? NotificationUrl { get; set; }
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
        public const string Inactive = "Inactive";//Created but not yet valid
        public const string Active = "Active";
        public const string Expired = "Expired";
        public const string Cancelled = "Cancelled";
        public const string Completed = "Completed";//Redeemed and fully used
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

    public static class UpsellOrderMapper
    {
        public static UpsellOrderRecord MapToRecord(UpsellOrderRecordEntity entity)
        {
            var state = string.IsNullOrWhiteSpace(entity.UpsellOrderJson)
                ? new UpsellOrderState()
                : JsonConvert.DeserializeObject<UpsellOrderState>(entity.UpsellOrderJson) ?? new UpsellOrderState();

            return new UpsellOrderRecord
            {
                UpsellOrderGuid = entity.UpsellOrderGuid,
                State = state,
                Lines = new List<UpsellOrderLineRecord>(),
                PaymentSessionGuid = entity.PaymentSessionGuid,
                PaymentStatus = entity.PaymentStatus ?? Models.ReservationWorkflow.PaymentStatuses.None,
                Provider = entity.Provider,
                ProviderTransactionId = entity.ProviderTransactionId,
                PaidAtUtc = entity.PaidAtUtc,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        public static UpsellOrderLineRecord MapLineToRecord(UpsellOrderLineEntity entity)
        {
            return new UpsellOrderLineRecord
            {
                UpsellOrderLineGuid = entity.UpsellOrderLineGuid,
                UpsellOrderGuid = entity.UpsellOrderGuid,
                PartnerServiceId = entity.PartnerServiceId,
                TitleSnapshot = entity.TitleSnapshot,
                PricingModel = entity.PricingModel,
                Quantity = entity.Quantity,
                UnitPriceGross = entity.UnitPriceGross,
                Nights = entity.Nights,
                TotalGuests = entity.TotalGuests,
                LineTotalGross = entity.LineTotalGross,
                Currency = entity.Currency,
                LineStatus = entity.LineStatus,
                BitrixProductId = entity.BitrixProductId,
                BitrixLineId = entity.BitrixLineId,
                IsFreeUnlimitedUses = entity.IsFreeUnlimitedUses,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                UpsellDefinitionSnapshot = entity.UpsellDefinitionSnapshot,
                Voucher = entity.UpsellVoucher is null ? null : MapVoucherToDto(entity.UpsellVoucher)
            };
        }

        public static UpsellOrderLineEntity MapLineToEntity(Guid upsellOrderGuid, UpsellOrderLineRecord line)
        {
            return new UpsellOrderLineEntity
            {
                UpsellOrderLineGuid = line.UpsellOrderLineGuid == Guid.Empty ? Guid.NewGuid() : line.UpsellOrderLineGuid,
                UpsellOrderGuid = upsellOrderGuid,
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
                BitrixProductId = line.BitrixProductId,
                BitrixLineId = line.BitrixLineId,
                IsFreeUnlimitedUses = line.IsFreeUnlimitedUses,
                UpsellDefinitionSnapshot = line.UpsellDefinitionSnapshot,
                CreatedAt = line.CreatedAt == default ? DateTime.UtcNow : line.CreatedAt,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public static UpsellVoucherDto MapVoucherToDto(UpsellVoucherEntity voucher)
        {
            return new UpsellVoucherDto
            {
                VoucherGuid = voucher.UpsellVoucherGuid,
                OrderLineGuid = voucher.UpsellOrderLineGuid,
                ReservationGuid = voucher.ReservationGuid,
                //PartnerServiceId = line.PartnerServiceId,
                CodeShort = voucher.CodeShort,
                QrToken = voucher.QrToken,
                UsedCount = voucher.UsedCount,
                MaxUses = voucher.MaxUses,
                ValidFrom = voucher.ValidFrom,
                ValidTo = voucher.ValidTo,
                Status = voucher.Status,
                //TitleSnapshot = line.TitleSnapshot,
                // Currency = line.Currency
            };
        }
    }
}
