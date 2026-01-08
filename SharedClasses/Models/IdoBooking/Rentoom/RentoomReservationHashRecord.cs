namespace RentoomBooking.SharedClasses.Models.IdoBooking.Rentoom
{
    public class RentoomReservationHashRecord
    {
        public ReservationResponseFromIdoSellAPI ReservationResponse { get; set; }
        public string resToken { get; set; } = string.Empty;
        
    }
}