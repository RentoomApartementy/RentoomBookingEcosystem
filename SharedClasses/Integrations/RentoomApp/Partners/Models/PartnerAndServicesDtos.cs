using RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Enums;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Models
{

    public class PartnerInfoDto
    {
        public int Id { get; set; }
        public Guid PublicId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? Region { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public string? LogoBannerUrl { get; set; }
    }
    public class PartnerServiceSnapshot
    {
        public int Id { get; set; }
        public int PartnerId { get; set; }
        public Guid PublicServiceId { get; set; }
        public int Sequence { get; set; }
        public string ServiceTitle { get; set; } = string.Empty;
        public PartnerServiceStatus Status { get; set; }
        public PartnerServiceCategory Category { get; set; }
        public bool StayBoundOnly { get; set; }
        public bool IsPersonalizable { get; set; }
        public bool VisibleInStayWell { get; set; }
        public bool VisibleInRentoomBooking { get; set; }

        public PartnerServicePricingModel PricingModel { get; set; }
        public string Currency { get; set; } = "PLN";
        public decimal BasePrice { get; set; }
        public decimal? TaxRate { get; set; }
        public PartnerServiceDiscountType DiscountType { get; set; }
        public decimal? DiscountValue { get; set; }
        public DateTime? DiscountValidFrom { get; set; }
        public DateTime? DiscountValidTo { get; set; }

        public List<PartnerServiceTranslationDto> Translations { get; set; } = new();
        public List<PartnerServiceBannerDto> Banners { get; set; } = new();
        public List<PartnerServiceTargetDto> Targets { get; set; } = new();
    }

    public class PartnerServiceTranslationDto
    {
        public string Culture { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string? LongDescription { get; set; }
        public string? Terms { get; set; }
    }

    public class PartnerServiceBannerDto
    {
        public PartnerServiceBannerPlacementType PlacementType { get; set; }
        public string? Culture { get; set; }
        public int MediaAssetId { get; set; }
        public string? MediaAssetStorageKey { get; set; }
        public string? MediaAssetFileName { get; set; }
        public string? MediaAssetContentType { get; set; }
    }

    public class PartnerServiceTargetDto
    {
        public PartnerServiceTargetType TargetType { get; set; }
        public int TargetId { get; set; }
    }

    // Mapper extensions for converting domain entities to DTOs
    public static class PartnerServiceMappings
    {
        public static PartnerServiceSnapshot ToDto(this PartnerService src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            return new PartnerServiceSnapshot
            {
                Id = src.Id,
                PartnerId = src.PartnerId,
                PublicServiceId = src.PublicServiceId,
                Sequence = src.Sequence,
                ServiceTitle = src.ServiceTitle,
                Status = src.Status,
                Category = src.Category,
                StayBoundOnly = src.StayBoundOnly,
                IsPersonalizable = src.IsPersonalizable,
                VisibleInStayWell = src.VisibleInStayWell,
                VisibleInRentoomBooking = src.VisibleInRentoomBooking,
                PricingModel = src.PricingModel,
                Currency = src.Currency,
                BasePrice = src.BasePrice,
                TaxRate = src.TaxRate,
                DiscountType = src.DiscountType,
                DiscountValue = src.DiscountValue,
                DiscountValidFrom = src.DiscountValidFrom,
                DiscountValidTo = src.DiscountValidTo,
                Translations = src.Translations?.Select(t => new PartnerServiceTranslationDto
                {
                    Culture = t.Culture,
                    Title = t.Title,
                    ShortDescription = t.ShortDescription,
                    LongDescription = t.LongDescription,
                    Terms = t.Terms
                }).ToList() ?? new List<PartnerServiceTranslationDto>(),
                Banners = src.Banners?.Select(b => new PartnerServiceBannerDto
                {
                    PlacementType = b.PlacementType,
                    Culture = b.Culture,
                    MediaAssetId = b.MediaAssetId,
                    MediaAssetStorageKey = b.MediaAsset?.StorageKey,
                    MediaAssetFileName = b.MediaAsset?.FileName,
                    MediaAssetContentType = b.MediaAsset?.ContentType
                }).ToList() ?? new List<PartnerServiceBannerDto>(),
                Targets = src.Targets?.Select(t => new PartnerServiceTargetDto
                {
                    TargetType = t.TargetType,
                    TargetId = t.TargetId
                }).ToList() ?? new List<PartnerServiceTargetDto>()
            };
        }
    }
}
