using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.ReservationWorkflow
{
    public class StartReservationRequest
    {
        public int ObjectId { get; set; }
        public int ObjectItemId { get; set; }
        [JsonConverter(typeof(DateOnlyJsonConverter))]
        public DateOnly StartDate { get; set; }
        [JsonConverter(typeof(DateOnlyJsonConverter))]
        public DateOnly EndDate { get; set; }
        public TimeOnly CheckInTime { get; set; } = new TimeOnly(15, 0); //15:00
        public TimeOnly CheckOutTime { get; set; } = new TimeOnly(11, 0); //11:00
        public int Adults { get; set; }
        public int Children { get; set; }
        public string? SelectedOfferType { get; set; }
        public decimal? OfferPrice { get; set; }
        public string Currency { get; set; } = "PLN";
        public List<SelectedAddonDto> SelectedAddons { get; set; } = new();
    }

    public class ClientInfoDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public string CountryCode { get; set; } = "pl";
    }

    public class InvoiceInfoDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public string TaxNumber { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class ReservationSummaryDto
    {
        public Guid ReservationGuid { get; set; }
        public StartReservationRequest? StartRequest { get; set; }
        public ClientInfoDto? Client { get; set; }
       
        public InvoiceInfoDto? Invoice { get; set; }
        public int? IdoReservationId { get; set; }
        public string? IdoStatus { get; set; }
        public decimal? OfferPrice { get; set; }
        public string Currency { get; set; } = "PLN";
        public string PaymentStatus { get; set; } = PaymentStatuses.None;
    }

    public class PaymentInitResult
    {
        public Guid ReservationGuid { get; set; }
        public Guid PaymentSessionGuid { get; set; }
        public string ProviderTransactionId { get; set; } = string.Empty;
        public string RedirectUrl { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
    }

    public class PaymentStateDto
    {
        public Guid ReservationGuid { get; set; }
        public string PaymentStatus { get; set; } = PaymentStatuses.None;
        public Guid? PaymentSessionGuid { get; set; }
        public string? ProviderTransactionId { get; set; }
        public string? Provider { get; set; }
        public string? RedirectUrl { get; set; }
        public string? IdoStatus { get; set; }
    }

    public class TpayWebhookDto
    {
        public Guid ReservationGuid { get; set; }
        public Guid PaymentSessionGuid { get; set; }
        public string ProviderTransactionId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }

    public class ReservationState
    {
        public StartReservationRequest? StartRequest { get; set; }
        public ClientInfoDto? Client { get; set; }
        public InvoiceInfoDto? Invoice { get; set; }

      //  public List<TermsAndConditionsAcceptanceInfo> {get;set;}
        public string? PaymentRedirectUrl { get; set; }
    }

    public class ReservationRecord
    {
        public Guid ReservationGuid { get; set; }
        public ReservationState State { get; set; } = new();
        public int? IdoReservationId { get; set; }
        public string? IdoStatus { get; set; }
        public int? ClientBitrixId { get; set; }
        public int? DealBitrixId { get; set; }
        public string? DealBitrixSentConfirmationEmailId { get; set; } // ma wartosc gdy email poszedl.. 
        public Guid? PaymentSessionGuid { get; set; }
        public string PaymentStatus { get; set; } = PaymentStatuses.None;
        public string? Provider { get; set; }
        public string? ProviderTransactionId { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }


    public class SelectedAddonDto //: NewReservationAddon
    {
        public int AddonId { get; set; }
        public int Persons { get; set; }
        public int Nights { get; set; }
        public int Quantity { get; set; }
        public float Price { get; set; }
        public float Vat { get; set; }
    }

    public static class PaymentStatuses
    {
        public const string None = "None";
        public const string Initiated = "Initiated";
        public const string Paid = "Paid";
        public const string Failed = "Failed";
    }

    public class DealEmailActivityDto  //Bitrix xEmailActivity class
    {
        public string? Id { get; set; }
        public string? Subject { get; set; }
        public string? ProviderId { get; set; }
        public string? ProviderTypeId { get; set; }
        public string? Status { get; set; }
        public string? Completed { get; set; }
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public DateTimeOffset? Created { get; set; }
        public DateTimeOffset? LastUpdated { get; set; }
        public string? Direction { get; set; }
    }

    public class DealEmailStatusDto
    {
        public bool EmailSent { get; set; }
        public bool HasActivities { get; set; }
        public DealEmailActivityDto? LatestActivity { get; set; }
        public List<DealEmailActivityDto> Activities { get; set; } = new();
    }


    public class DateOnlyJsonConverter : JsonConverter<DateOnly>
    {
        private const string Format = "yyyy-MM-dd";

        public override void WriteJson(JsonWriter writer, DateOnly value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString(Format));
        }

        public override DateOnly ReadJson(JsonReader reader, Type objectType, DateOnly existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var stringValue = reader.Value?.ToString();
            return DateOnly.TryParse(stringValue, out var parsed) ? parsed : default;
        }
    }
}
