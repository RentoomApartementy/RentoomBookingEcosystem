using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;

namespace RentoomBooking.SharedClasses.Services
{
    public interface IApartmentsService
    {
        Task<PagedResult<ApartmentObject>?> GetApartmentsByPageAsync(
           string? continuationToken = null,
           int top = 50,
           CancellationToken ct = default);

        Task<long> GetApartmentsCountAsync();

        Task<ApartmentObject?> GetApartmentByIdAsync(int objectId);
        Task<List<ApartmentObject>> GetApartmentsByFilterAsync(ApartmentQueryFilter filter, CancellationToken ct = default);
        Task<PagedResult<ApartmentObject>> GetAllApartmentsList();
        Task<List<DefinedAddonEntity>> GetDefinedAddonsAsync(CancellationToken cancellationToken = default);
        Task<List<ApartmentDefinedAmenityDto>> GetDefinedAmenitiesAsync(string? lang, int? amenityId, CancellationToken cancellationToken = default);
        Task<List<ApartmentDefinedAmenityDto>> GetApartmentAmenitiesAsync(string? lang, int? objectId, CancellationToken cancellationToken = default);

        /// <summary>Mandatory (Optional == false) addons of the apartment, resolved against the defined-addons
        /// catalog for their price/payment model. No nights/guests context - use with AddonPricingCalculator
        /// once those are known (see GetMandatoryAddonsTotalAsync for the common case).</summary>
        Task<IReadOnlyList<MandatoryAddonCharge>> GetMandatoryAddonChargesAsync(ApartmentObject apartment, CancellationToken cancellationToken = default);

        /// <summary>Sum of the apartment's mandatory addon charges for a given stay length/guest count,
        /// via AddonPricingCalculator.CalculateTotal per addon's PaymentType.</summary>
        Task<decimal> GetMandatoryAddonsTotalAsync(ApartmentObject apartment, int nights, int totalGuests, CancellationToken cancellationToken = default);
    }

    public class ApartmentsService : IApartmentsService
    {
        private static readonly TimeSpan ApartmentAmenitiesDisplayCacheTtl = TimeSpan.FromMinutes(30);

        private readonly ApartmentRepository _apartmentsRepository;
        private readonly IMemoryCache _memoryCache;

        public ApartmentsService(ApartmentRepository apartmentsRepository, IMemoryCache memoryCache)
        {
            _apartmentsRepository = apartmentsRepository ?? throw new ArgumentNullException(nameof(apartmentsRepository));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        public async Task<PagedResult<ApartmentObject>?> GetApartmentsByPageAsync(
           string? continuationToken = null,
           int top = 50,
           CancellationToken ct = default)
        {
            return await _apartmentsRepository.QueryApartmentsAsync(continuationToken, top);
        }

        public async Task<long> GetApartmentsCountAsync()
        {
            return await _apartmentsRepository.GetApartmentCountAsync();
        }

        public async Task<ApartmentObject?> GetApartmentByIdAsync(int objectId)
        {
            return _apartmentsRepository.FindApartmentInPostgres(objectId);
        }

        public async Task<List<ApartmentObject>> GetApartmentsByFilterAsync(ApartmentQueryFilter filter, CancellationToken ct = default)
        {
            return await _apartmentsRepository.GetApartmentsByFilterAsync(filter, ct) ?? [];
        }

        public async Task<PagedResult<ApartmentObject>> GetAllApartmentsList()
        {
            var allResults = new List<ApartmentObject>();
            string? continuationToken = null;
            long totalCount = await _apartmentsRepository.GetApartmentCountAsync();

            do
            {
                var page = await _apartmentsRepository.QueryApartmentsAsync(continuationToken, 100);
                allResults.AddRange(page.Items);
                continuationToken = page.ContinuationToken;
            } while (continuationToken != null);

            return new PagedResult<ApartmentObject>(
                allResults,
                null,
                allResults.Count,
                totalCount
            );
        }

        public async Task<List<DefinedAddonEntity>> GetDefinedAddonsAsync(CancellationToken cancellationToken = default)
        {
            return await _apartmentsRepository.GetDefinedAddonsAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<MandatoryAddonCharge>> GetMandatoryAddonChargesAsync(ApartmentObject apartment, CancellationToken cancellationToken = default)
        {
            var mandatoryIds = apartment.Addons?
                .Where(a => a.Optional == false && a.Id.HasValue)
                .Select(a => a.Id!.Value)
                .ToHashSet();

            if (mandatoryIds is not { Count: > 0 })
            {
                return Array.Empty<MandatoryAddonCharge>();
            }

            var definedAddons = await GetDefinedAddonsAsync(cancellationToken);
            return definedAddons
                .Where(d => mandatoryIds.Contains(d.IdoBookingId))
                .Select(d => new MandatoryAddonCharge(d.PaymentType, d.PriceGross))
                .ToList();
        }

        public async Task<decimal> GetMandatoryAddonsTotalAsync(ApartmentObject apartment, int nights, int totalGuests, CancellationToken cancellationToken = default)
        {
            var charges = await GetMandatoryAddonChargesAsync(apartment, cancellationToken);
            if (charges.Count == 0)
            {
                return 0m;
            }

            var effectiveNights = Math.Max(nights, 1);
            var effectiveGuests = Math.Max(totalGuests, 1);
            return charges.Sum(c => AddonPricingCalculator.CalculateTotal(c.PaymentType, c.PriceGross, effectiveNights, effectiveGuests, quantity: 1));
        }

        public async Task<List<ApartmentDefinedAmenityDto>> GetDefinedAmenitiesAsync(string? lang, int? amenityId, CancellationToken cancellationToken = default)
        {
            var amenities = await _apartmentsRepository.GetDefinedAmenitiesAsync(NormalizeLanguage(lang), cancellationToken);

            if (!amenityId.HasValue)
            {
                return amenities;
            }

            return amenities.Where(x => x.AmenityId == amenityId.Value).ToList();
        }

        public async Task<List<ApartmentDefinedAmenityDto>> GetApartmentAmenitiesAsync(string? lang, int? objectId, CancellationToken cancellationToken = default)
        {
            if (!objectId.HasValue || objectId.Value <= 0)
            {
                return [];
            }

            var normalizedLang = NormalizeLanguage(lang);
            var cacheKey = BuildApartmentAmenitiesDisplayCacheKey(objectId.Value, normalizedLang, _apartmentsRepository.GetApartmentAmenitiesCacheVersion());

            var cachedAmenities = await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = ApartmentAmenitiesDisplayCacheTtl;

                var apartmentAmenitiesDocument = await _apartmentsRepository.GetApartmentAmenitiesDocumentAsync(objectId.Value, cancellationToken);
                if (apartmentAmenitiesDocument?.Amenities == null || apartmentAmenitiesDocument.Amenities.Count == 0)
                {
                    return new List<ApartmentDefinedAmenityDto>();
                }

                var amenityIdsInOrder = apartmentAmenitiesDocument.Amenities
                    .Where(x => x != null && x.Id > 0)
                    .Select(x => x.Id)
                    .Distinct()
                    .ToList();

                if (amenityIdsInOrder.Count == 0)
                {
                    return new List<ApartmentDefinedAmenityDto>();
                }

                var localizedDefinitions = await GetDefinedAmenitiesAsync(normalizedLang, null, cancellationToken);
                var localizedByAmenityId = localizedDefinitions
                    .GroupBy(x => x.AmenityId)
                    .ToDictionary(g => g.Key, g => g.First());

                Dictionary<int, ApartmentDefinedAmenityDto>? englishByAmenityId = null;
                if (!string.Equals(normalizedLang, "en", StringComparison.OrdinalIgnoreCase))
                {
                    var englishDefinitions = await GetDefinedAmenitiesAsync("en", null, cancellationToken);
                    englishByAmenityId = englishDefinitions
                        .GroupBy(x => x.AmenityId)
                        .ToDictionary(g => g.Key, g => g.First());
                }

                var result = new List<ApartmentDefinedAmenityDto>(amenityIdsInOrder.Count);
                foreach (var amenityId in amenityIdsInOrder)
                {
                    if (localizedByAmenityId.TryGetValue(amenityId, out var localized))
                    {
                        result.Add(CloneAmenityDto(localized));
                        continue;
                    }

                    if (englishByAmenityId != null && englishByAmenityId.TryGetValue(amenityId, out var english))
                    {
                        result.Add(CloneAmenityDto(english));
                    }
                }

                return result;
            });

            return cachedAmenities ?? [];
        }

        private static ApartmentDefinedAmenityDto CloneAmenityDto(ApartmentDefinedAmenityDto source)
        {
            return new ApartmentDefinedAmenityDto
            {
                Id = source.Id,
                AmenityId = source.AmenityId,
                AmenityTypeName = source.AmenityTypeName,
                AmenityName = source.AmenityName,
                Lang = source.Lang,
                IconSource = source.IconSource
            };
        }

        private static string BuildApartmentAmenitiesDisplayCacheKey(int objectId, string lang, long version)
        {
            return $"apartments:amenities-display:v{version}:{objectId}:{lang}";
        }

        private static string NormalizeLanguage(string? lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                return "pl";
            }

            try
            {
                return CultureInfo.GetCultureInfo(lang).TwoLetterISOLanguageName.ToLowerInvariant();
            }
            catch (CultureNotFoundException)
            {
                return lang.Trim().ToLowerInvariant();
            }
        }
    }
}
