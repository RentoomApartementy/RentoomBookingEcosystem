using RentoomBooking.SharedClasses.Models;

namespace RentoomBooking.SharedClasses.Models.IdoBooking
{
    public class GetAvailabilityLocksRequestPayload
    {
        public ResultRequestPaging? Result { get; set; }
        public GetAvailabilityLocksParamsSearch? ParamsSearch { get; set; }
    }

    public class GetAvailabilityLocksRequestType
    {
        public AuthenticateType Authenticate { get; set; } = new();
        public ResultRequestPaging? Result { get; set; }
        public GetAvailabilityLocksParamsSearch? ParamsSearch { get; set; }
    }

    public class GetAvailabilityLocksParamsSearch
    {
        public int? Id { get; set; }
        public int? ItemId { get; set; }
        public int? ObjectId { get; set; }
        public string? UserLogin { get; set; }
        public string? DateFrom { get; set; }
        public string? DateTo { get; set; }
    }

    public class GetAvailabilityLocksResponseType
    {
        public GetAvailabilityLocksResponse Result { get; set; } = new();
        public string? Id { get; set; }
    }

    public class GetAvailabilityLocksResponse
    {
        public AuthenticateType? Authenticate { get; set; }
        public GateErrorType? Errors { get; set; }
        public List<AvailabilityLock>? AvailabilityLocks { get; set; }
        public bool? Success { get; set; }
    }

    public class AvailabilityLock
    {
        public int? Id { get; set; }
        public int? ItemId { get; set; }
        public int? ObjectId { get; set; }
        public int? UserId { get; set; }
        public string? Note { get; set; } = string.Empty;
        public string? DateFrom { get; set; } = string.Empty;
        public string? DateTo { get; set; } = string.Empty;
        public string? DateAdd { get; set; } = string.Empty;
    }
}
