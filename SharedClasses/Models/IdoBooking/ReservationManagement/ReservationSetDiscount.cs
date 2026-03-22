namespace RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement
{
    public class ReservationSetDiscountRequest
    {
        public AuthenticateType Authenticate { get; set; } = new();

        public List<SetReservationDiscount> Reservations { get; set; } = new();
    }

    public class SetReservationDiscount
    {
        public int ReservationId { get; set; }

        public float PercentValue { get; set; }
    }

    public class ReservationSetDiscountResponseType
    {
        public ReservationSetDiscountResponse? Result { get; set; }

        public string? Id { get; set; }
    }

    public class ReservationSetDiscountResponse
    {
        public AuthenticateType? Authenticate { get; set; }

        public GateErrorType? Errors { get; set; }

        public List<SetReservationDiscount>? Reservations { get; set; }
    }
}
