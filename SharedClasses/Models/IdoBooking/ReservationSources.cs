namespace RentoomBooking.SharedClasses.Models.IdoBooking
{
    public class ReservationSourcesRequest
    {
        public AuthenticateType Authenticate { get; set; } = new();
        public ReservationSourcesResultRequest? Result { get; set; }
    }

    public class ReservationSourcesResultRequest
    {
        public int? Page { get; set; }
        public int? Number { get; set; }
    }

    public class ReservationSourcesResponseType
    {
        public ReservationSourcesResponse? Result { get; set; }
        public string? Id { get; set; }
    }

    public class ReservationSourcesResponse
    {
        public AuthenticateType? Authenticate { get; set; }
        public GateErrorType? Errors { get; set; }
        public List<ReservationSourceDescription>? Sources { get; set; }
    }

    public class ReservationSourceDescription
    {
        public int ReservationSourceTypeId { get; set; }
        public string ReservationSourceTypeName { get; set; } = string.Empty;
        public int ReservationSourceId { get; set; }
        public string ReservationSourceName { get; set; } = string.Empty;
    }
}
