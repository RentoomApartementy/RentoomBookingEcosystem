using System.Globalization;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Services.IdoBooking;

namespace RentoomBookingWeb.Services
{
    /// <summary>
    /// Per-day availability + per-night price feed for a single apartment,
    /// built on top of IdoBooking's getAvailabilityAndPricesForDays endpoint.
    /// This is the data source for the availability-aware booking calendar.
    /// Unlike <see cref="IAvailabilityFinderService"/> / AvailabilityFinderService2,
    /// it does NOT collapse the day grid into aggregate terms — it surfaces the raw
    /// per-night availability, price and min-stay that a calendar needs.
    /// </summary>
    public interface IApartmentCalendarService
    {
        Task<ApartmentCalendarDto> GetCalendarAsync(
            int objectId,
            DateOnly from,
            DateOnly to,
            int adults,
            int children,
            CancellationToken cancellationToken = default);
    }

    public class ApartmentCalendarDto
    {
        public Dictionary<DateOnly, ApartmentCalendarDay> Days { get; set; } = new();

        /// <summary>Lowest per-night price across available nights — the "from X zł/night" anchor.</summary>
        public decimal? FromPriceGross { get; set; }

        public string Currency { get; set; } = "PLN";
    }

    public class ApartmentCalendarDay
    {
        public bool Available { get; set; }
        public decimal? PriceGross { get; set; }
        public int? MinStay { get; set; }
    }

    public class ApartmentCalendarService : IApartmentCalendarService
    {
        private readonly IIdoOfferService _offerService;

        public ApartmentCalendarService(IIdoOfferService offerService)
        {
            _offerService = offerService ?? throw new ArgumentNullException(nameof(offerService));
        }

        public async Task<ApartmentCalendarDto> GetCalendarAsync(
            int objectId,
            DateOnly from,
            DateOnly to,
            int adults,
            int children,
            CancellationToken cancellationToken = default)
        {
            var result = new ApartmentCalendarDto();

            if (to < from)
            {
                return result;
            }

            var payload = new OfferAvailabilityAndPricesParamsSearchInternal
            {
                ObjectIds = new List<int> { objectId },
                ParamsSearch = new OfferAvailabilityAndPricesParamsSearch
                {
                    DateFrom = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    DateTo = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    AdultsNumber = adults,
                    ChildrenNumber = children > 0 ? children : null,
                    Currency = "PLN",
                    Language = "pol"
                }
            };

            var objects = await _offerService
                .GetAvailabilityAndPricesForDaysAsync(payload, cancellationToken)
                .ConfigureAwait(false);

            var apartment = objects?.FirstOrDefault(o => o.ObjectId == objectId) ?? objects?.FirstOrDefault();
            if (apartment is null)
            {
                return result;
            }

            // Per-night price lookup first, so availability rows can attach a price.
            var priceLookup = new Dictionary<DateOnly, decimal>();
            foreach (var priceDate in apartment.ObjectPricesDates ?? Enumerable.Empty<OfferPriceDate>())
            {
                if (TryParseDate(priceDate.Date, out var date))
                {
                    priceLookup[date] = priceDate.Price;
                }
            }

            foreach (var availabilityDate in apartment.ObjectAvailability ?? Enumerable.Empty<OfferAvailabilityDate>())
            {
                if (!TryParseDate(availabilityDate.Date, out var date))
                {
                    continue;
                }

                priceLookup.TryGetValue(date, out var price);

                result.Days[date] = new ApartmentCalendarDay
                {
                    Available = availabilityDate.ItemsNumber > 0,
                    PriceGross = priceLookup.ContainsKey(date) ? price : null,
                    MinStay = availabilityDate.MinStay
                };
            }

            result.FromPriceGross = result.Days.Values
                .Where(d => d.Available && d.PriceGross.HasValue)
                .Select(d => d.PriceGross!.Value)
                .DefaultIfEmpty()
                .Min();

            if (result.FromPriceGross == 0m && !result.Days.Values.Any(d => d.Available && d.PriceGross.HasValue))
            {
                result.FromPriceGross = null;
            }

            return result;
        }

        private static bool TryParseDate(string? value, out DateOnly date)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                return true;
            }

            date = default;
            return false;
        }
    }
}
