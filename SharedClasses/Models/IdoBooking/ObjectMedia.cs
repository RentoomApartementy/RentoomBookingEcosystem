using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.IdoBooking
{
    public class ObjectMediaRequestType
    {
        public AuthenticateType Authenticate { get; set; } = new AuthenticateType();
        public int? ObjectId { get; set; }
    }

    public class ObjectMediaResponseType
    {
        public AuthenticateType? Authenticate { get; set; }
        public List<GateErrorType>? Errors { get; set; }
        public bool? Success { get; set; }
        public List<ObjectMedium>? ObjectMedia { get; set; }
    }

    public class ObjectMedium
    {
        public int Id { get; set; }
        public int ObjectId { get; set; }
        public string? Type { get; set; }
        public string? Extension { get; set; }
        public int Position { get; set; }
        public string? Url { get; set; }
    }
}
