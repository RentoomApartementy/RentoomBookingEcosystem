using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement
{
    public class ReservationAddRequest
    {
        public AuthenticateType Authenticate { get; set; } = new();
        public ReservationAddParams Params { get; set; } = new();
    }

    public class ReservationAddParams
    {
        public List<NewReservation> Reservations { get; set; } = new();
    }

    public class NewReservation
    {
        public Guid? RentoomResrvationID { get; set; } = null; // dodatkowe pole do mapowania rezerwacji w RentoomBooking, nie jest wymagane przez IdoSell, dodawanie on demand zawsze.
        public string DateFrom { get; set; } = string.Empty;
        public string DateTo { get; set; } = string.Empty;
        public float? Price { get; set; }
        public int? ClientId { get; set; }
        public ClientWithGuest? ClientData { get; set; }
        public string? ApiNote { get; set; }
        public string? ClientNote { get; set; }
        public string? ExternalNote { get; set; }
        public string? InternalNote { get; set; }
        public string? Status { get; set; }
        public string? InternalSource { get; set; }
        public List<NewReservationPackage>? Packages { get; set; }
        public List<NewReservationItem> Items { get; set; } = new();
        public string Currency { get; set; } = string.Empty;
        public string? Notify { get; set; } = ReservationNotifyType.No; // wartości "n", "y"
        public string? NotifyService { get; set; } = ReservationNotifyType.No; // wartości "n", "y"
    }

    public class NewReservationPackage
    {
        public int PackageId { get; set; }
        public float? Price { get; set; }
    }

    public class NewReservationItem
    {
        public int ObjectItemId { get; set; }
        public float? Price { get; set; }
        public float? Vat { get; set; }
        public int? NumberOfAdults { get; set; }
        public int? NumberOfBigChildren { get; set; }
        public int? NumberOfSmallChildren { get; set; }
        public List<NewReservationAddon>? Addons { get; set; }
    }

    public class NewReservationAddon
    {
        public int AddonId { get; set; }
        public int Persons { get; set; }
        public int Nights { get; set; }
        public int Quantity { get; set; }
        public float Price { get; set; }
        public float Vat { get; set; }
    }

    public class ReservationAddResponseType
    {
        public ReservationAddResponse? Result { get; set; }
        public string? Id { get; set; }
    }

    public class ReservationAddResponse
    {
        public AuthenticateType? Authenticate { get; set; }
        public GateErrorType? Errors { get; set; }
        public List<ReservationChangeResult>? Reservations { get; set; }
    }

    public class ReservationChangeResult
    {
        public bool Success { get; set; }
        public GateErrorType? Error { get; set; }
        public int? ReservationId { get; set; }
    }

    public static class ReservationNotifyType
    {
        public const string Yes = "y";
        public const string No = "n";
    }

    public static class ReservationStatusType
    {
        public const string Unconfirmed = "unconfirmed";
        public const string Confirmed = "confirmed";
        public const string PaymentInProgress = "paymentInProgress";
        public const string WaitingForPayment = "waitingForPayment";
        public const string Completed = "completed";
        public const string Accepted = "accepted";
        public const string InProgress = "inProgress";
        public const string Canceled = "canceled";
        public const string Withdrawn = "withdrawn";
        public const string InvalidCardNumber = "invalidCardNumber";
        public const string ToClarify = "toClarify";
    }

    public static class ReservationInternalSourceType
    {
        public const string Other = "other";
        public const string Email = "email";
        public const string Phone = "phone";
        public const string FaceToFaceConversation = "faceToFaceConversation";
        public const string SocialMedia = "socialMedia";
    }
}
