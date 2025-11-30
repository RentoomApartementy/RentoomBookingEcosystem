using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement
{
    public class EditReservationsStatusRequestType
    {
        public AuthenticateType Authenticate { get; set; } = new();

        public List<EditReservationsStatusRequest> Reservations { get; set; } = new();
    }

    public class EditReservationsStatusRequest
    {
        public int ReservationId { get; set; }

        public string Status { get; set; } = string.Empty;

        public string? Notify { get; set; } = ReservationNotifyType.No;

        public string? NotifyService { get; set; } = ReservationNotifyType.No;
    }

    public class ChangeReservationsStatusResponseType
    {
        public ChangeReservationsStatusResponse? Result { get; set; }

        public string? Id { get; set; }
    }

    public class ChangeReservationsStatusResponse
    {
        public AuthenticateType? Authenticate { get; set; }

        public GateErrorType? Errors { get; set; }

        public List<ReservationStatusChangeResult>? Reservations { get; set; }
    }

    public class ReservationStatusChangeResult
    {
        public bool? Success { get; set; }

        public GateErrorType? Error { get; set; }

        public int? ReservationId { get; set; }
    }
}
