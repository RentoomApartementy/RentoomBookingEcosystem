using Microsoft.Extensions.Caching.Memory;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.IdoBooking;

namespace RentoomBookingWeb.Services
{
    /// <summary>The apartment's lowest daily price (2 adults / 0 children) over the cached window,
    /// with and without the mandatory-addons flat fee added on top.</summary>
    public readonly record struct ApartmentMinPrice(decimal RawPrice, decimal PriceWithMandatoryFee);

    /// <summary>Backend-computed, site-wide-shared "from" price per apartment, sourced from IdoBooking's
    /// getPricesForDays endpoint and cached for 60 minutes (min daily prices don't change often enough to
    /// warrant a per-visitor fetch). Used as the cheap default "from" price on ApartmentsSection,
    /// BlogApartmentsListing, and the /apartamenty search page before the visitor picks dates/guests.</summary>
    public interface IApartmentMinPriceService
    {
        Task<IReadOnlyDictionary<int, ApartmentMinPrice>> GetMinPricesAsync(
            DateOnly? dateFrom = null,
            DateOnly? dateTo = null,
            CancellationToken cancellationToken = default);
    }

    public class ApartmentMinPriceService : IApartmentMinPriceService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(60);

        private readonly IIdoOfferService _offerService;
        private readonly IApartmentsService _apartmentsService;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<ApartmentMinPriceService> _logger;

        public ApartmentMinPriceService(
            IIdoOfferService offerService,
            IApartmentsService apartmentsService,
            IMemoryCache memoryCache,
            ILogger<ApartmentMinPriceService> logger)
        {
            _offerService = offerService ?? throw new ArgumentNullException(nameof(offerService));
            _apartmentsService = apartmentsService ?? throw new ArgumentNullException(nameof(apartmentsService));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyDictionary<int, ApartmentMinPrice>> GetMinPricesAsync(
            DateOnly? dateFrom = null,
            DateOnly? dateTo = null,
            CancellationToken cancellationToken = default)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var from = dateFrom ?? today;
            var to = dateTo ?? today.AddMonths(1);
            var cacheKey = $"apartments:min-prices:{from:yyyy-MM-dd}:{to:yyyy-MM-dd}:2:0";

            var result = await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                return await BuildMinPricesAsync(from, to, cancellationToken);
            });

            return result ?? new Dictionary<int, ApartmentMinPrice>();
        }

        private async Task<Dictionary<int, ApartmentMinPrice>> BuildMinPricesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            var payload = new OfferPricesForDaysParamsSearchInternal
            {
                ParamsSearch = new OfferPricesForDaysParamsSearch
                {
                    DateFrom = from.ToString("yyyy-MM-dd"),
                    DateTo = to.ToString("yyyy-MM-dd"),
                    AdultsNumber = 2,
                    ChildrenNumber = null,
                    Language = "pol",
                    Currency = "PLN"
                }
            };

            var offerObjects = await _offerService.GetPricesForDaysAsync(payload, cancellationToken) ?? new List<OfferPricesForDaysObject>();

            var allApartments = await _apartmentsService.GetAllApartmentsList();
            var apartmentsById = allApartments.Items.ToDictionary(a => a.Id);

            var result = new Dictionary<int, ApartmentMinPrice>();
            foreach (var offerObject in offerObjects)
            {
                var pricedDays = offerObject.ObjectPricesDates?.Where(d => d.Price > 0).ToList();
                if (pricedDays is not { Count: > 0 })
                {
                    continue;
                }

                var rawMin = pricedDays.Min(d => d.Price);
                var fee = 0m;

                if (apartmentsById.TryGetValue(offerObject.ObjectId, out var apartment))
                {
                    try
                    {
                        fee = await _apartmentsService.GetMandatoryAddonsTotalAsync(apartment, nights: 1, totalGuests: 2, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to resolve mandatory addons fee for apartment {ApartmentId} while building min-price cache.", offerObject.ObjectId);
                    }
                }

                result[offerObject.ObjectId] = new ApartmentMinPrice(rawMin, rawMin + fee);
            }

            _logger.LogInformation("Built min-price cache for {ApartmentCount} apartments, window {DateFrom}..{DateTo}.", result.Count, from, to);

            return result;
        }
    }
}
