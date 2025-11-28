using RentoomBooking.SharedClasses.Models.IdoBooking.Client;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.RentoomBooking
{
    public class WebsiteCreateReservationRequest
    {
        public ClientAddRequestClient Client { get; set; } = new();
        public NewReservation Reservation { get; set; } = new();
    }

    public class WebsiteCreateReservationResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int? ClientId { get; set; }
        public int? BitrixClientId { get; set; }
        public int? ReservationId { get; set; }
        public string? ResToken { get; set; }
        public string? StayWellLink { get; set; }
        public string? ClientError { get; set; }
        public string? ReservationError { get; set; }
    }
}
