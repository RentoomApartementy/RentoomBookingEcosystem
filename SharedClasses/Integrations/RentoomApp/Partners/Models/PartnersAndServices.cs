using RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Models
{
    [Table("Partner", Schema = "rentoom")]
    public class Partner
    {
        public int Id { get; set; }
        public Guid PublicId { get; private set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public PartnerStatus Status { get; set; }
        public PartnerType PartnerType { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? Region { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public string DefaultLanguage { get; set; } = "pl";
        public int? LogoBannerMediaAssetId { get; set; }
        public MediaAsset? LogoBannerMediaAsset { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        public ICollection<PartnerSupportedLanguage> SupportedLanguages { get; set; } = new List<PartnerSupportedLanguage>();
        public ICollection<PartnerService> Services { get; set; } = new List<PartnerService>();
    }
    
    [Table("PartnerSupportedLanguage", Schema = "rentoom")]
    public class PartnerSupportedLanguage
    {
        public int Id { get; set; }
        public int PartnerId { get; set; }
        public string Culture { get; set; } = "pl";
        public Partner Partner { get; set; } = null!;
    }

    [Table("PartnerService", Schema = "rentoom")]
    public class PartnerService
    {
        public int Id { get; set; }
        public int PartnerId { get; set; }
        public Partner Partner { get; set; } = null!;
        public int Sequence { get; set; }
        public string ServiceTitle { get; set; } = string.Empty;
        public PartnerServiceStatus Status { get; set; }
        public PartnerServiceCategory Category { get; set; }
        public bool StayBoundOnly { get; set; }
        public bool IsPersonalizable { get; set; }
        public PartnerServicePricingModel PricingModel { get; set; }
        public string Currency { get; set; } = "PLN";
        public decimal BasePrice { get; set; }
        public decimal? TaxRate { get; set; }
        public PartnerServiceDiscountType DiscountType { get; set; }
        public decimal? DiscountValue { get; set; }
        public DateTime? DiscountValidFrom { get; set; }
        public DateTime? DiscountValidTo { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        public ICollection<PartnerServiceTranslation> Translations { get; set; } = new List<PartnerServiceTranslation>();
        public ICollection<PartnerServiceBanner> Banners { get; set; } = new List<PartnerServiceBanner>();
        public ICollection<PartnerServiceTarget> Targets { get; set; } = new List<PartnerServiceTarget>();
    }

    [Table("PartnerServiceTranslation", Schema = "rentoom")]
    public class PartnerServiceTranslation
    {
        public int Id { get; set; }
        public int ServiceId { get; set; }
        public PartnerService Service { get; set; } = null!;
        public string Culture { get; set; } = "en";
        public string Title { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string? LongDescription { get; set; }
        public string? Terms { get; set; }
    }

    [Table("MediaAsset", Schema = "rentoom")]
    public class MediaAsset
    {
        public int Id { get; set; }
        public string StorageKey { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string? Checksum { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    [Table("PartnerServiceBanner", Schema = "rentoom")]
    public class PartnerServiceBanner
    {
        public int Id { get; set; }
        public int ServiceId { get; set; }
        public PartnerService Service { get; set; } = null!;
        public PartnerServiceBannerPlacementType PlacementType { get; set; }
        public string? Culture { get; set; }
        public int MediaAssetId { get; set; }
        public MediaAsset MediaAsset { get; set; } = null!;
    }

    [Table("PartnerServiceTarget", Schema = "rentoom")]
    public class PartnerServiceTarget
    {
        public int Id { get; set; }
        public int ServiceId { get; set; }
        public PartnerService Service { get; set; } = null!;
        public PartnerServiceTargetType TargetType { get; set; }
        public int TargetId { get; set; }
    }
}
