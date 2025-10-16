using RentoomBooking.SharedClasses.Models.IdoBooking;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class ApartmentAmenitiesDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("partitionKey")]
    public string PartitionKey { get; set; } = string.Empty;

    [JsonPropertyName("apartmentId")]
    public string? ApartmentId { get; set; }

    [JsonPropertyName("amenities")]
    public List<ObjectAmenity>? Amenities { get; set; }
}
