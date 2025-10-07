using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.IdoBooking
{
    public class ObjectAmenitiesRequestType : ObjectDefaultRequestType
    {

    }

    public class ObjectAmenitiesResponseType
    {
        public ObjectAmenitiesResponse Result { get; set; } = new();
        public string Id { get; set; } = string.Empty;
    }

    public class ObjectAmenitiesResponse
    {
        public AuthenticateType? Authenticate { get; set; }
        public List<GateErrorType>? Errors { get; set; }
        public bool? Success { get; set; }
        public List<ObjectAmenity>? ObjectAmenities { get; set; }
    }

    public class ObjectAmenity
    {
        private int objectId;

        public int Id { get; set; }
        public int ObjectId { 
            
            get => objectId; 
            set
                { 
                int x = Convert.ToInt32(value);
                objectId = x; 
                }  
        }
        public string? Name { get; set; }

        
    }
}
