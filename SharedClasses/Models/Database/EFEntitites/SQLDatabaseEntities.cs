using RentoomBooking.SharedClasses.Models.IdoBooking.Payments;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Models.StayWell;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Enums;
using RentoomBooking.SharedClasses.Models.Upsell;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.Database.EFEntitites
{
    public class ApartmentInfoEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("payload", TypeName = "jsonb")]
        public string Payload { get; set; } = string.Empty;

        [Column("updated_at")] 
        public DateTime UpdatedAt { get; set; }
            = DateTime.UtcNow;
    }

    public class ApartmentAmenityEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("payload", TypeName = "jsonb")]
        public string Payload { get; set; } = string.Empty;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
            = DateTime.UtcNow;
    }

    public class ApartmentHashEntity
    {
        [Key]
        [Column("id")]
        public string Id { get; set; } = string.Empty;

        [Column("payload", TypeName = "jsonb")]
        public string Payload { get; set; } = string.Empty;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
            = DateTime.UtcNow;
    }

    public class ReservationEntity
    {
        [Key]
        [Column("res_token")]
        public string ResToken { get; set; } = string.Empty;

        [Column("reservation_id")]
        public int ReservationId { get; set; }

        [Column("payload", TypeName = "jsonb")]
        public string Payload { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
            = DateTime.UtcNow;
    }

    [Table("reservation_templates")]
    public class ReservationTemplateEntity
    {
        [Key]
        [Column("template_key")]
        public string TemplateKey { get; set; } = string.Empty;

        [Column("payload", TypeName = "jsonb")]
        public string Payload { get; set; } = string.Empty;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("reservation_records")]
    public class ReservationRecordEntity
    {
        [Key]
        [Column("reservation_guid")]
        public Guid ReservationGuid { get; set; }

        [Column("reservation_json", TypeName = "jsonb")]
        public string ReservationJson { get; set; } = string.Empty;

        [Column("ido_reservation_id")]
        public int? IdoReservationId { get; set; }

        [Column("ido_status")]
        public string? IdoStatus { get; set; }
        
        [Column("client_bitrix_id")]
        public int? ClientBitrixId { get; set; }

        [Column("deal_bitrix_id")]
        public int? DealBitrixId { get; set; }

        [Column("confirmation_email_bitrix_id")]
        public string? ConfirmationEmailBitrixId { get; set; }



        [Column("payment_session_guid")]
        public Guid? PaymentSessionGuid { get; set; }

        [Column("payment_status")]
        public string PaymentStatus { get; set; } = PaymentStatuses.None;

        [Column("provider")]
        public string? Provider { get; set; }

        [Column("provider_transaction_id")]
        public string? ProviderTransactionId { get; set; }

        [Timestamp]
        [Column("row_version")]
        public byte[]? RowVersion { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("upsell_order_records")]
    public class UpsellOrderRecordEntity
    {
        [Key]
        [Column("upsell_order_guid")]
        public Guid UpsellOrderGuid { get; set; }

        [Column("upsell_order_json", TypeName = "jsonb")]
        public string UpsellOrderJson { get; set; } = string.Empty;

        [Column("reservation_guid")]
        public Guid? ReservationGuid { get; set; }

        [Column("payment_session_guid")]
        public Guid? PaymentSessionGuid { get; set; }

        [Column("payment_status")]
        public string PaymentStatus { get; set; } = PaymentStatuses.None;

        [Column("provider")]
        public string? Provider { get; set; }

        [Column("provider_transaction_id")]
        public string? ProviderTransactionId { get; set; }

        [Column("paid_at_utc")]
        public DateTime? PaidAtUtc { get; set; }

        [Timestamp]
        [Column("row_version")]
        public byte[]? RowVersion { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("upsell_order_lines")]
    public class UpsellOrderLineEntity
    {
        [Key]
        [Column("upsell_order_line_guid")]
        public Guid UpsellOrderLineGuid { get; set; }

        [Column("upsell_order_guid")]
        public Guid UpsellOrderGuid { get; set; }

        [Column("partner_service_id")]
        public int PartnerServiceId { get; set; }

        [Column("title_snapshot")]
        public string TitleSnapshot { get; set; } = string.Empty;

        [Column("pricing_model")]
        public PartnerServicePricingModel PricingModel { get; set; }

        [Column("quantity")]
        public int Quantity { get; set; }

        [Column("unit_price_gross")]
        public decimal UnitPriceGross { get; set; }

        [Column("nights")]
        public int Nights { get; set; }

        [Column("total_guests")]
        public int TotalGuests { get; set; }

        [Column("line_total_gross")]
        public decimal LineTotalGross { get; set; }

        [Column("currency")]
        public string Currency { get; set; } = "PLN";

        [Column("line_status")]
        public string LineStatus { get; set; } = UpsellLineStatuses.Pending;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("defined_addons")]
    public class DefinedAddonEntity
    {
        [Key]
        [Column("idobooking_id")]
        public int IdoBookingId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("payment_type")]
        public AddonPaymentType PaymentType { get; set; }

        [Column("addon_type")]
        public string AddonType { get; set; } = string.Empty;


        [Column("price_gross")]
        public decimal PriceGross { get; set; }

        [Column("vat")]
        public decimal Vat { get; set; }

        [Column("addon_definition", TypeName = "jsonb")]
        public DefinedAddonDefinition AddonDefinition { get; set; } = new();
        
    }



    public class SearchFiltersEntity
    {
        [Key]
        [Column("filter_groupname")]
        public string FilterGroupName { get; set; } = string.Empty;
        
        [Column("payload", TypeName = "jsonb")]
        public string Payload { get; set; } = string.Empty;

    }

    public class TermsEntity
    {
        [Key]
        [Column("res_token")]
        public string ResToken { get; set; } = string.Empty;
        [Column("version_accepted")]
        public string VersionAccepted { get; set; } = string.Empty;
        [Column("type_accepted")]
        public string TypeAccepted { get; set; } = string.Empty;
        [Column("accepted_at")]
        public DateTime AcceptedAt { get; set; }
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
            = DateTime.UtcNow;
    }

    public class RegistrationCardEntity
    {
        [Key]
        [Column("res_token")]
        public string ResToken { get; set; } = string.Empty;

        [Column("contact_email")]
        public string ContactEmail { get; set; } = string.Empty;

        //[Column("contact_phone")]
        //public string ContactPhone { get; set; } = string.Empty;
        //[Column("phone_country_code")]
        //public string PhoneCountryCode { get; set; } = string.Empty;
        [Column("check_in_time")]
        public DateTime CheckInTime { get; set; }
        [Column("guests_data")]
        public List<RegistrationCardGuestModel> GuestsData { get; set; } = new();

    }

}
