namespace RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement
{
    public class ReservationEditRequest
    {
        public AuthenticateType Authenticate { get; set; } = new();
        public ReservationEditParams Params { get; set; } = new();
    }

    public class ReservationEditParams
    {
        public List<EditReservation> Reservations { get; set; } = new();
    }

    public class EditReservation
    {
        public int Id { get; set; }
        public string? DateFrom { get; set; }
        public string? DateTo { get; set; }
        public int? ClientId { get; set; }
        public string? ClientNote { get; set; }
        public string? ExternalNote { get; set; }
        public string? ApiNote { get; set; }
        public string? InternalNote { get; set; }
        public List<EditReservationItem>? Items { get; set; }
        public string? Notify { get; set; } = ReservationNotifyType.No;
        public string? NotifyService { get; set; } = ReservationNotifyType.No;
    }

    public class EditReservationItem
    {
        public int ObjectItemId { get; set; }
        public float? Price { get; set; }
        public float? PriceCorrection { get; set; }
        public float? Vat { get; set; }
        public int? NumberOfAdults { get; set; }
        public int? NumberOfBigChildren { get; set; }
        public int? NumberOfSmallChildren { get; set; }
        public List<EditReservationAddon>? Addons { get; set; }
    }

    public class EditReservationAddon
    {
        public int AddonId { get; set; }
        public int Persons { get; set; }
        public int Nights { get; set; }
        public int Quantity { get; set; }
        public float Price { get; set; }
        public float Vat { get; set; }
        public string? AddonName { get; set; } = null;
    }

    public class ReservationEditResponseType
    {
        public ReservationEditResponse? Result { get; set; }
        public string? Id { get; set; }
    }

    public class ReservationEditResponse
    {
        public GateErrorType? Errors { get; set; }
        public List<ReservationEditResult>? Reservations { get; set; }
    }

    public class ReservationEditResult
    {
        public bool? Success { get; set; }
        public int? ReservationId { get; set; }
        public GateErrorType? Error { get; set; }
    }
}