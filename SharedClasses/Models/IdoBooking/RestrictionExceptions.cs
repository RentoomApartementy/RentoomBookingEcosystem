using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.IdoBooking
{
    public class GetRestrictionsExceptionsRequestType
    {
        public AuthenticateType Authenticate { get; set; } = new();
        public GetRestrictionException GetRestrictionException { get; set; } = new();
    }

    public class GetRestrictionException
    {
        public List<int>? ObjectsIds { get; set; }
        public string? OfferType { get; set; }
        public string? RestrictionExceptionDateFrom { get; set; }
        public string? RestrictionExceptionDateTo { get; set; }
    }

    public class GetRestrictionsExceptionsResponseType
    {
        public GetRestrictionsExceptionsResponse Result { get; set; } = new();
        public string? Id { get; set; }
    }

    public class GetRestrictionsExceptionsResponse
    {
        public AuthenticateType? Authenticate { get; set; }
        public GateErrorType? Errors { get; set; }
        public bool? Success { get; set; }
        public List<RestrictionException>? GetRestrictionExceptions { get; set; }
    }

    public class RestrictionException
    {
        public int ObjectId { get; set; }
        public string OfferType { get; set; } = string.Empty;
        public string RestrictionExceptionDate { get; set; } = string.Empty;
        public bool ClosedToArrival { get; set; }
        public bool ClosedToDeparture { get; set; }
        public LengthSetting LengthSetting { get; set; } = new();
    }

    public class LengthSetting
    {
        public string LengthMode { get; set; } = string.Empty;
        public string? LengthType { get; set; }
        public int? LengthStay { get; set; }
    }
}
