using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.IdoBooking
{
    public class AmenitiesForObjectsRequestType
    {
        public AuthenticateType Authenticate { get; set; } = new AuthenticateType();
        public List<int>? ObjectTypesIds { get; set; }
    }

    public class AmenitiesForObjectsResponseType
    {
        public AmenitiesForObjectsResponse Result { get; set; } = new();
        public string Id { get; set; } = string.Empty;
    }

    public class AmenitiesForObjectsResponse
    {
        public AuthenticateType? Authenticate { get; set; }
        public List<GateErrorType>? Errors { get; set; }
        public bool? Success { get; set; }
        public List<ObjectTypesAmenities>? ObjectTypesAmenities { get; set; }
    }
    public class ObjectTypesAmenities
    {
        public ObjectType ObjectType { get; set; } = new();
        public List<Amenity>? ObjectAmenities { get; set; }
    }

    public class ObjectType
    {
        public int ObjectTypeId { get; set; }
        public string ObjectTypeName { get; set; } = string.Empty;
    }

    public class Amenity
    {
        public int AmenityId { get; set; }
        public List<AmenityNameItem>? AmenityNames { get; set; }
    }

    public class AmenityNameItem
    {
        public string LanguageCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
