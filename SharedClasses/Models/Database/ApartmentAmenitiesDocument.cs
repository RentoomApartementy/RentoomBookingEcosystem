using RentoomBooking.SharedClasses.Models.IdoBooking;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class ApartmentAmenitiesDocument
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("apartmentId")]
    public int ApartmentId { get; set; }

    [JsonPropertyName("amenities")]
    public List<ObjectAmenity>? Amenities { get; set; }
}
