using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking;

//Pozostałość po alpejskich kombinacjach. Do usunięcia.
namespace RentoomBooking.StayWell.Models
{
    public class Data
    {
        public RentoomReservation? Reservation { get; set; }
        public List<ObjectMedium>? Media { get; set; }
        public List<ObjectAmenity>? Amenities { get; set; }
        public string? Token { get; set; }
    }
}
