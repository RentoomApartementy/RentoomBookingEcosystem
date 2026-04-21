using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement;
using System;

namespace RentoomBooking.SharedClasses.Models.ReservationWorkflow
{
    public class StayWellReservationLookupResponse
    {
        public RentoomReservation? Reservation { get; set; }
        public StayWellReservationRecordDto? ReservationRecord { get; set; }
    }

    public class StayWellReservationRecordDto
    {
        public Guid ReservationGuid { get; set; }
        public string PaymentStatus { get; set; } = PaymentStatuses.None;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Status { get; set; } =string.Empty;
        public StayWellReservationRecordStateDto State { get; set; } = new();

        public static StayWellReservationRecordDto FromRecord(ReservationRecord record)
        {
            if (record is null) throw new ArgumentNullException(nameof(record));

            return new StayWellReservationRecordDto
            {
                ReservationGuid = record.ReservationGuid,
                PaymentStatus = record.PaymentStatus,
                CreatedAt = record.CreatedAt,
                UpdatedAt = record.UpdatedAt,
                Status = record.IdoStatus ?? string.Empty,
                State = new StayWellReservationRecordStateDto
                {
                    GoogleMapsLink = record.State.GoogleMapsLink,
                    ParkingMapUrl = record.State.ParkingMapUrl,
                    StayWellLink = record.State.StayWellLink,
                }
            };
        }
    }

    public class StayWellReservationRecordStateDto
    {
        public string GoogleMapsLink { get; set; } = string.Empty;
        public string ParkingMapUrl { get; set; } = string.Empty;
        public string StayWellLink { get; set; } = string.Empty;
    }
}
