using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.NearbyAttractions.Models
{
    // ---------- DTO (kontrakt dla Web/Api/StayWell) ----------

    public class NearbyAttractionDto
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int DistanceMeters { get; set; }
        public int WalkMinutes { get; set; }
        public string? Address { get; set; }
        public double? Rating { get; set; }
        public string? GoogleMapsUri { get; set; }
        public string? ExternalPlaceId { get; set; }
    }

    public class NearbyAttractionsResultDTO
    {
        public int ApartmentItemId { get; set; }
        public List<NearbyAttractionDto> Items { get; set; } = new();
        public DateTime? LastRefreshedUtc { get; set; }   // UTC
        public string Status { get; set; } = string.Empty; // ok / no-location / no-api-key / failed / (pusty = brak danych)
    }

    // ---------- Encje EF (read-only, schema "rentoom") ----------

    [Table("ApartmentNearbyAttractionsSets", Schema = "rentoom")]
    public class ApartmentNearbyAttractionsSet
    {
        [Key]
        public int ApartmentItemId { get; set; }
        public int ObjectId { get; set; }
        public DateTime? LastRefreshedUtc { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string LastRefreshStatus { get; set; } = string.Empty;

        public List<ApartmentNearbyAttraction> Attractions { get; set; } = new();
    }

    [Table("ApartmentNearbyAttractions", Schema = "rentoom")]
    public class ApartmentNearbyAttraction
    {
        [Key]
        public int Id { get; set; }
        public int ApartmentItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int DistanceMeters { get; set; }
        public int WalkMinutes { get; set; }
        public string? Address { get; set; }
        public double? Rating { get; set; }
        public string? GoogleMapsUri { get; set; }
        public string? ExternalPlaceId { get; set; }
    }
}
