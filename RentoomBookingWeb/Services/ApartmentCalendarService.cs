using System.Globalization;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.IdoBooking;

namespace RentoomBookingWeb.Services
{
    /// <summary>
    /// Per-day availability feed for a single apartment, built on top of IdoBooking's dedicated
    /// getAvailabilityForDays endpoint (availability only — no prices, no min-stay). This is the
    /// data source for the availability-aware booking calendar, which only ever needs to know
    /// which nights are bookable, not their price.
    /// Unlike <see cref="IAvailabilityFinderService"/> / AvailabilityFinderService2,
    /// it does NOT collapse the day grid into aggregate terms — it surfaces the raw
    /// per-night availability that a calendar needs.
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

        /// <summary>The "from X zł/night" anchor — the real, validated price of the nearest
        /// available term to today, sourced from the suggested-date mechanism
        /// (<see cref="IAvailabilityFinderService2"/>), since the availability-only endpoint
        /// carries no price data.</summary>
        public decimal? FromPriceGross { get; set; }

        public string Currency { get; set; } = "PLN";
    }

    public class ApartmentCalendarDay
    {
        public bool Available { get; set; }
        public decimal? PriceGross { get; set; }

        /// <summary>Always null — the availability-only endpoint carries no min-stay data.
        /// Callers (ApartmentBookingWidget.MinStayFor) already default to 1 night when this is null.</summary>
        public int? MinStay { get; set; }
    }

    public class ApartmentCalendarService : IApartmentCalendarService
    {
        private readonly IIdoOfferService _offerService;
        private readonly IAvailabilityFinderService2 _availabilityFinder;

        public ApartmentCalendarService(IIdoOfferService offerService, IAvailabilityFinderService2 availabilityFinder)
        {
            _offerService = offerService ?? throw new ArgumentNullException(nameof(offerService));
            _availabilityFinder = availabilityFinder ?? throw new ArgumentNullException(nameof(availabilityFinder));
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

            var payload = new OfferAvailabilityForDaysParamsSearchInternal
            {
                ObjectIds = new List<int> { objectId },
                ParamsSearch = new OfferAvailabilityForDaysParamsSearch
                {
                    DateFrom = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    DateTo = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    PersonsNumber = adults + children,
                    Language = "pol"
                }
            };

            var objects = await _offerService
                .GetAvailabilityForDaysAsync(payload, cancellationToken)
                .ConfigureAwait(false);

            var apartment = objects?.FirstOrDefault(o => o.ObjectId == objectId) ?? objects?.FirstOrDefault();
            if (apartment is not null)
            {
                foreach (var availabilityDate in apartment.ObjectAvailability ?? Enumerable.Empty<OfferAvailabilityForDaysDate>())
                {
                    if (!TryParseDate(availabilityDate.Date, out var date))
                    {
                        continue;
                    }

                    result.Days[date] = new ApartmentCalendarDay
                    {
                        Available = availabilityDate.ItemsNumber > 0,
                        PriceGross = null,
                        MinStay = null
                    };
                }
            }

            result.FromPriceGross = await GetNearestSuggestedPriceAsync(objectId, adults, children, cancellationToken).ConfigureAwait(false);

            return result;
        }

        /// <summary>Same flat-fee-adjusted per-night divisor used by the apartments list page's
        /// suggested-date price (see Apartment.razor's GetSuggestionFromPrice) — AvailableTerm.MinimalPrice
        /// is the TOTAL price for the whole stay, not a nightly rate, so it must go through this same
        /// calculator rather than being shown as-is.</summary>
        private const decimal SuggestionFlatFee = 139m;

        /// <summary>"From X zł/night" anchor — the real, validated price (via the same suggested-date
        /// mechanism used elsewhere for "no offer for these dates, try these" alternatives) of the
        /// nearest available term to today, not an estimate. A 1-night reference range is used purely
        /// to ask "what's the closest available date" — the underlying search still finds and prices
        /// the actual nearest available term regardless of its real length/min-stay.</summary>
        private async Task<decimal?> GetNearestSuggestedPriceAsync(int objectId, int adults, int children, CancellationToken cancellationToken)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var referenceStart = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var referenceEnd = today.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            var result = await _availabilityFinder
                .FindAvailableTermsForApartmentAsync(objectId, referenceStart, referenceEnd, adults, children, cancellationToken)
                .ConfigureAwait(false);

            var term = result.AvailableTerms?.FirstOrDefault(t => t.MinimalPrice.HasValue);
            if (term?.MinimalPrice is not decimal totalPrice)
            {
                return null;
            }

            if (!DateOnly.TryParse(term.StartDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
                !DateOnly.TryParse(term.EndDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            {
                return null;
            }

            var nights = end.DayNumber - start.DayNumber;
            if (nights <= 0)
            {
                return null;
            }

            var perNight = ((totalPrice - SuggestionFlatFee) / nights) + SuggestionFlatFee;
            return perNight > 0 ? perNight : null;
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
