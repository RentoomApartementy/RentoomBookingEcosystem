using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.Database
{
    
        public class ApartmentAmenitiesDocument
        {
            public string Id { get; set; } = string.Empty;

            [JsonProperty("partitionKey")]
            [JsonPropertyName("partitionKey")]
            public string PartitionKey { get; set; } = string.Empty;

            public string? ApartmentId { get; set; }

            //public ApartmentObject? Apartment { get; set; }

            public List<ObjectAmenity>? Amenities { get; set; }
        }


    
}
