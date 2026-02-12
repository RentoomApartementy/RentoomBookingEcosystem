namespace RentoomBooking.StayWell.Models
{
    public class UpsellVoucherLookupRequestDto
    {
        public string? CodeShort { get; set; }
        public string? QrToken { get; set; }
        public string? ReservationToken { get; set; }
    }
}
