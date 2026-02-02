using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Database;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Enums;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Models;
using RentoomBooking.SharedClasses.Models.Upsell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.Upsell
{
    public interface IUpsellCatalogService
    {
        Task<IReadOnlyList<UpsellTileDto>> GetUpsellTilesForApartmentAsync(int apartmentId, string culture, CancellationToken cancellationToken = default);
    }

    public class UpsellCatalogService : IUpsellCatalogService
    {
        private readonly IDbContextFactory<RappPartnersDBContext> _dbContextFactory;

        public UpsellCatalogService(IDbContextFactory<RappPartnersDBContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<IReadOnlyList<UpsellTileDto>> GetUpsellTilesForApartmentAsync(int apartmentItemId, string culture, CancellationToken cancellationToken = default)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var services = await dbContext.PartnerServices
                .AsNoTracking()
                .Include(service => service.Partner)
                .Include(service => service.Translations)
                .Include(service => service.Banners)
                    .ThenInclude(banner => banner.MediaAsset)
                .Include(service => service.Targets)
                .Where(service => service.Status == PartnerServiceStatus.Active && service.Partner.Status == PartnerStatus.Active) 
                .OrderBy(service => service.Sequence)
                .ToListAsync(cancellationToken);

            var applicableServices = services.Where(service => AppliesToApartment(service, apartmentItemId)).ToList();

            var tiles = new List<UpsellTileDto>(applicableServices.Count);
            foreach (var service in applicableServices)
            {
                var translation = SelectTranslation(service, culture);
                var banners = SelectBanners(service, culture);

                tiles.Add(new UpsellTileDto
                {
                    PartnerPublicId = service.Partner?.PublicId.ToString() ?? string.Empty,
                    PartnerServiceId = service.Id.ToString(),
                    Title = translation?.Title ?? service.ServiceTitle,
                    ShortDescription = translation?.ShortDescription ?? string.Empty,
                    Price = service.BasePrice,
                    Currency = service.Currency,
                    Discount = service.DiscountType == PartnerServiceDiscountType.None ? null : service.DiscountValue,
                    PricingDiscountType = service.DiscountType.ToString(),
                    BannerUrls = banners,
                    StayBoundOnly = service.StayBoundOnly,
                    PricingModel = service.PricingModel.ToString(),
                    IsPersonalizable = service.IsPersonalizable,

                   
                });
            }

            return tiles;
        }

        internal static bool AppliesToApartment(PartnerService service, int apartmentId)
        {
            if (service.Targets == null || service.Targets.Count == 0)
            {
                return true;
            }

            return service.Targets.Any(target =>
                target.TargetType == PartnerServiceTargetType.Apartment &&
                target.TargetId == apartmentId);
        }

        internal static PartnerServiceTranslation? SelectTranslation(PartnerService service, string culture)
        {
            var translations = service.Translations;
            if (translations == null || translations.Count == 0)
            {
                return null;
            }

            var cultureMatch = translations.FirstOrDefault(t =>
                !string.IsNullOrWhiteSpace(t.Culture) &&
                string.Equals(t.Culture, culture, StringComparison.OrdinalIgnoreCase));
            if (cultureMatch != null)
            {
                return cultureMatch;
            }

            var defaultCulture = service.Partner?.DefaultLanguage;
            if (!string.IsNullOrWhiteSpace(defaultCulture))
            {
                var defaultMatch = translations.FirstOrDefault(t =>
                    !string.IsNullOrWhiteSpace(t.Culture) &&
                    string.Equals(t.Culture, defaultCulture, StringComparison.OrdinalIgnoreCase));
                if (defaultMatch != null)
                {
                    return defaultMatch;
                }
            }

            return translations.FirstOrDefault();
        }

        internal static Dictionary<string, string> SelectBanners(PartnerService service, string culture)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (service.Banners == null || service.Banners.Count == 0)
            {
                return result;
            }

            foreach (PartnerServiceBannerPlacementType placement in Enum.GetValues(typeof(PartnerServiceBannerPlacementType)))
            {
                var banner = SelectBannerForPlacement(service.Banners, placement, culture);
                if (banner?.MediaAsset?.StorageKey == null)
                {
                    continue;
                }

                result[placement.ToString()] = banner.MediaAsset.StorageKey;
            }

            return result;
        }

        internal static PartnerServiceBanner? SelectBannerForPlacement(IEnumerable<PartnerServiceBanner> banners, PartnerServiceBannerPlacementType placement, string culture)
        {
            var candidates = banners.Where(b => b.PlacementType == placement).ToList();
            if (candidates.Count == 0)
            {
                return null;
            }

            var cultureMatch = candidates.FirstOrDefault(b =>
                !string.IsNullOrWhiteSpace(b.Culture) &&
                string.Equals(b.Culture, culture, StringComparison.OrdinalIgnoreCase));
            if (cultureMatch != null)
            {
                return cultureMatch;
            }

            var defaultMatch = candidates.FirstOrDefault(b => b.Culture == null);
            if (defaultMatch != null)
            {
                return defaultMatch;
            }

            return candidates.FirstOrDefault();
        }
    }

}
