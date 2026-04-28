using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Utils;

namespace RentoomBooking.SharedClasses.Models.IdoBooking
{

    public class LockRequestType 
    {
        public AuthenticateType Authenticate { get; set; } = new AuthenticateType();
        public ResultRequestPaging Result { get; set; } = new ResultRequestPaging();
        public LockParamsSearch ParamsSearch { get; set; } = new LockParamsSearch();
    }

    public class LockParamsSearch
    {
        public int ReservationId { get; set; }
        public int ItemId { get; set; }
    }

    public class LockResponseType
    {
        public LockResponse Result { get; set; } = new LockResponse();
        public string? Id { get; set; }
    }

    public class LockResponse
    {
        public AuthenticateType? Authenticate { get; set; }
        [JsonConverter(typeof(SingleOrArrayConverter<GateErrorType>))]
        public List<GateErrorType>? Errors { get; set; }
        public bool? Success { get; set; }
        public List<Lock>? Locks { get; set; }
    }

    public class Lock
    {
        public int ReservationId { get; set; }
        public int ItemId { get; set; }
        public string? Code { get; set; }
    }
}
