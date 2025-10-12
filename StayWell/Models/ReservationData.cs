using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking;

namespace RentoomBooking.StayWell.Models
{
    public class Data
    {
        public RentoomReservation? Reservation { get; set; }
        public ApartmentObject? Apartment { get; set; }
        public List<ObjectMedium>? Media { get; set; }
        public List<ObjectAmenity>? Amenities { get; set; }
        public string? Token { get; set; }
    }
}
