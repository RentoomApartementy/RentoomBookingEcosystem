using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.IdoBooking.ObjectLocationDTO
{
    public class GetObjectLocationRequestType
    {  
        public AuthenticateType Authenticate { get; set; } = new AuthenticateType();
        public ParamsSearchObjectLocationType? ParamsSearchObjectLocation { get; set; } = null;
    }

    public class ParamsSearchObjectLocationType
    { 
        public List<ObjectLocationRequestItem>? Objects { get; set; }= null;
    }
    public class ObjectLocationRequestItem
    {
      public int Id { get; set; }
    }

    public class GetObjectLocationResult
    {
       public GetObjectLocationResponseType Result = new();

    }

    public class GetObjectLocationResponseType
    {
        public AuthenticateType? Authenticate { get; set; }
        public GateErrorType? Errors { get; set; }
        public bool Success { get; set; }
        public List<ObjectLocation>? ObjectLocations { get; set; }
    }

    public class ObjectLocation
    {
        public int ObjectId { get; set; }
        public int LocationId { get; set; }
        public string? Address { get; set; }
        public LocalizationItem? LocalizationItem { get; set; }
    }
}
