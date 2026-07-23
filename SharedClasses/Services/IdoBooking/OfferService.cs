using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.IdoBooking
{

    public interface IIdoOfferService
    {
        Task<PricingOffersResponse?> GetPricingOffersAsync(PricingOffersRequest request,
            CancellationToken cancellationToken = default);

        Task<List<OfferAvailabilityObject>?> GetAvailabilityAndPricesForDaysAsync(OfferAvailabilityAndPricesParamsSearchInternal request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Fetches a single public offer for the given apartment id from public/offer/34/json.
        /// Returns null when the apartment has no usable public offer (missing price, error response, or fetch failure).
        /// Successful responses are cached server-side per apartment id for 10 minutes.
        /// </summary>
        Task<PublicApartmentOffer?> GetPublicOfferAsync(int apartmentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fetches public offers for many apartments with limited concurrency.
        /// A failed/error response for a single apartment does not affect the others; only successful offers appear in the result.
        /// </summary>
        Task<IReadOnlyDictionary<int, PublicApartmentOffer>> GetPublicOffersAsync(IEnumerable<int> apartmentIds, CancellationToken cancellationToken = default);
    }

    public class IdoOfferService : IIdoOfferService
    {
        private readonly IIdoBookingConnectService _idoBookingConnectService;
        private readonly ILogger<IdoOfferService> _logger;
        private readonly IMemoryCache _memoryCache;

        private const string PricingOffersEndpoint = "public/pricingOffers/34/json";
        private const string AvailabilityAndPricesForDaysEndpoint = "offer/getAvailabilityAndPricesForDays/34/json";
        private const string PublicOfferEndpoint = "public/offer/34/json";

        private static readonly TimeSpan PublicOfferCacheTtl = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan DatedOffersCacheTtl = TimeSpan.FromMinutes(5);
        private const int PublicOfferMaxConcurrency = 6;

        public IdoOfferService(IIdoBookingConnectService idoBookingConnectService, ILogger<IdoOfferService> logger, IMemoryCache memoryCache)
        {
            _idoBookingConnectService = idoBookingConnectService ?? throw new ArgumentNullException(nameof(idoBookingConnectService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        public async Task<PricingOffersResponse?> GetPricingOffersAsync(PricingOffersRequest request, CancellationToken cancellationToken = default)
        {
            var cacheKey = BuildPricingOffersCacheKey(request);
            if (_memoryCache.TryGetValue(cacheKey, out PricingOffersResponse? cachedResponse)
                && cachedResponse is not null)
            {
                return cachedResponse;
            }

            _logger.LogInformation("Requesting pricing offers for {ObjectCount} objects between {DateFrom} and {DateTo}.",
                request.ObjectIds?.Count ?? 0, request.DateFrom, request.DateTo);

            var response = await _idoBookingConnectService
                .PostAsync<PricingOffersRequest, PricingOffersResponse>(PricingOffersEndpoint, request, cancellationToken)
                .ConfigureAwait(false);

            if (response?.Errors != null)
            {
                _logger.LogWarning("Pricing offers request returned error {FaultCode}: {FaultString}.",
                    response.Errors.FaultCode, response.Errors.FaultString);
            }


            //MS 28.03 filtr żeby tylko pojawiały się nonrefundable offers (14 dni przed data pobytu) - ustalone z Bartkiem bo IDB coś nie zawsze idzie ok.
            //MS 29.03 zmiana na filtr pokazujący tylko ofertę z najwyższą ceną (14 dni przed data pobytu) - ustalone z Bartkiem, bo IDB coś nie zawsze idzie ok
            if (response?.Result?.PricingOffers != null && ShouldReturnOnlyNonRefundableOffers(request.DateFrom))
            {
                response.Result.PricingOffers = response.Result.PricingOffers
                    .Select(FilterToHigherPriceOffer)
                    .Where(offer => offer != null)
                    .Cast<PricingOffer>()
                    .ToList();
            }

            if (response?.Errors is null && response?.Result?.PricingOffers is not null)
            {
                _memoryCache.Set(cacheKey, response, DatedOffersCacheTtl);
            }

            return response;
        }

        public async Task<List<OfferAvailabilityObject>?> GetAvailabilityAndPricesForDaysAsync(
           OfferAvailabilityAndPricesParamsSearchInternal payload,
           CancellationToken cancellationToken = default)
        {

            var request = new OfferAvailabilityAndPricesForDaysRequest
            {
                Authenticate = _idoBookingConnectService.AuthObjectIdo(),
                ParamsSearch = payload.ParamsSearch,
                Result = new Models.ResultRequestPaging()

            };

            _logger.LogInformation(
                "Requesting availability and prices between {DateFrom} and {DateTo} for {Adults} adults and {Children} children.",
                request.ParamsSearch.DateFrom,
                request.ParamsSearch.DateTo,
                request.ParamsSearch.AdultsNumber,
                request.ParamsSearch.ChildrenNumber ?? 0);

            var response = await _idoBookingConnectService
                .PostAsync<OfferAvailabilityAndPricesForDaysRequest, OfferAvailabilityAndPricesForDaysResponseRoot>(
                    AvailabilityAndPricesForDaysEndpoint,
                    request,
                    cancellationToken)
                .ConfigureAwait(false);

            if (response?.Result.Errors != null)
            {
                _logger.LogWarning(
                    "Availability and prices request returned error {FaultCode}: {FaultString}.",
                    response.Result.Errors.FaultCode,
                    response.Result.Errors.FaultString);
            }


            var ret = response?.Result.OfferObjects;
            if (payload.ObjectIds != null && payload.ObjectIds.Any())
            {
                var idsHash = payload.ObjectIds.ToHashSet();

                ret = ret?.Where(o => idsHash.Contains(o.ObjectId)).ToList();
            }

            return ret;
        }

        public async Task<PublicApartmentOffer?> GetPublicOfferAsync(int apartmentId, CancellationToken cancellationToken = default)
        {
            if (apartmentId <= 0)
            {
                return null;
            }

            var cacheKey = BuildPublicOfferCacheKey(apartmentId);
            if (_memoryCache.TryGetValue(cacheKey, out PublicApartmentOffer? cached) && cached is not null)
            {
                return cached;
            }

            var offer = await FetchPublicOfferAsync(apartmentId, cancellationToken).ConfigureAwait(false);

            // Only cache a successful response (per spec). Errors/failures are not cached, so a transient
            // failure for one apartment can recover on the next render without waiting out the TTL.
            if (offer is not null)
            {
                _memoryCache.Set(cacheKey, offer, PublicOfferCacheTtl);
            }

            return offer;
        }

        public async Task<IReadOnlyDictionary<int, PublicApartmentOffer>> GetPublicOffersAsync(
            IEnumerable<int> apartmentIds,
            CancellationToken cancellationToken = default)
        {
            var ids = apartmentIds?.Where(id => id > 0).Distinct().ToList() ?? new List<int>();
            var result = new ConcurrentDictionary<int, PublicApartmentOffer>();
            if (ids.Count == 0)
            {
                return result;
            }

            using var throttler = new SemaphoreSlim(PublicOfferMaxConcurrency);
            var tasks = ids.Select(async id =>
            {
                await throttler.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var offer = await GetPublicOfferAsync(id, cancellationToken).ConfigureAwait(false);
                    if (offer is not null)
                    {
                        result[id] = offer;
                    }
                }
                finally
                {
                    throttler.Release();
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return result;
        }

        private async Task<PublicApartmentOffer?> FetchPublicOfferAsync(int apartmentId, CancellationToken cancellationToken)
        {
            try
            {
                var request = new PublicOfferRequest { OfferId = apartmentId };

                var response = await _idoBookingConnectService
                    .PostAsync<PublicOfferRequest, PublicOfferResponse>(PublicOfferEndpoint, request, cancellationToken)
                    .ConfigureAwait(false);

                if (response is null)
                {
                    return null;
                }

                // Errors may live at the root or inside result depending on the endpoint - treat either as "no offer".
                var error = response.Errors ?? response.Result?.Errors;
                if (error is not null)
                {
                    _logger.LogWarning(
                        "Public offer for apartment {ApartmentId} returned error {FaultCode}: {FaultString}.",
                        apartmentId, error.FaultCode, error.FaultString);
                    return null;
                }

                var minimalPrice = response.Result?.MinimalPrice;
                if (minimalPrice is null)
                {
                    return null;
                }

                return new PublicApartmentOffer
                {
                    ApartmentId = apartmentId,
                    MinimalPrice = minimalPrice.Value,
                    Currency = response.Result?.Currency,
                    ImageUrl = response.Result?.Images?.FirstOrDefault()?.Url
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // A single failing apartment must never break rendering of the other cards.
                _logger.LogWarning(ex, "Failed to fetch public offer for apartment {ApartmentId}.", apartmentId);
                return null;
            }
        }

        private static string BuildPublicOfferCacheKey(int apartmentId) => $"idobooking:public-offer:{apartmentId}";

        private static string BuildPricingOffersCacheKey(PricingOffersRequest request)
        {
            var ids = request.ObjectIds is { Count: > 0 }
                ? string.Join(',', request.ObjectIds.OrderBy(id => id))
                : "all";

            return string.Join(':',
                "idobooking:pricing-offers",
                ids,
                request.DateFrom ?? string.Empty,
                request.DateTo ?? string.Empty,
                request.Currency ?? string.Empty,
                request.NumberOfAdults?.ToString() ?? string.Empty,
                request.NumberOfBigChildren?.ToString() ?? string.Empty,
                request.Language ?? string.Empty);
        }

        private static bool ShouldReturnOnlyNonRefundableOffers(string? dateFrom)
        {
            if (!DateOnly.TryParse(dateFrom, out var startDate))
            {
                return false;
            }

            return startDate.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber <= 14;
        }

        private static PricingOffer? FilterToNonRefundableOffer(PricingOffer offer)
        {
            var nonRefundableOffers = offer.Offers?
                .Where(item => string.Equals(item.OfferType, "nonrefundable", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (nonRefundableOffers is null || nonRefundableOffers.Count == 0)
            {
                return null;
            }

            return new PricingOffer
            {
                ObjectId = offer.ObjectId,
                MinimalPrice = nonRefundableOffers.Min(item => item.Price),
                Offers = nonRefundableOffers
            };
        }

        private static PricingOffer? FilterToHigherPriceOffer(PricingOffer offer)
        {
                    
            var maxPriceItem = offer.Offers.MaxBy(x => x.Price);
            maxPriceItem.OfferType = "nonrefundable"; // MS 29.03 - zmiana typu oferty na nonrefundable, bo IDB coś nie zawsze idzie ok, a ustalenie było takie, że ma być tylko oferta nonrefundable (czyli z najwyższą ceną) - ustalone z Bartkiem
            return new PricingOffer
            {
                ObjectId = offer.ObjectId,
                MinimalPrice = maxPriceItem.Price,
                Offers = [maxPriceItem]
            };
        }

    }
}
