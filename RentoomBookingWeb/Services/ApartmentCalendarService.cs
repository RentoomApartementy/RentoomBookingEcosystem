using System.Globalization;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
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
            bool applyMandatoryAddonsFee = true,
            IReadOnlyList<MandatoryAddonCharge>? mandatoryAddonCharges = null,
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

        /// <summary>
        /// The real term from which <see cref="FromPriceGross"/> was calculated. Unlike the
        /// calendar day feed, this comes from a fully priced availability suggestion.
        /// </summary>
        public ApartmentFromOfferDto? FromOffer { get; set; }

        public string Currency { get; set; } = "PLN";
    }

    public sealed class ApartmentFromOfferDto
    {
        public decimal PricePerNightGross { get; init; }
        public string Currency { get; init; } = "PLN";
        public DateOnly AvailabilityStarts { get; init; }
        public DateOnly AvailabilityEnds { get; init; }
        public int Nights { get; init; }
        public int Adults { get; init; }
        public int Children { get; init; }
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
            bool applyMandatoryAddonsFee = true,
            IReadOnlyList<MandatoryAddonCharge>? mandatoryAddonCharges = null,
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

            var today = DateOnly.FromDateTime(DateTime.Today);
            var nearestAvailableDay = result.Days
                .Where(day => day.Key >= today && day.Value.Available)
                .Select(day => (DateOnly?)day.Key)
                .OrderBy(day => day)
                .FirstOrDefault() ?? today;

            result.FromOffer = await GetNearestSuggestedOfferAsync(
                objectId,
                nearestAvailableDay,
                adults,
                children,
                applyMandatoryAddonsFee,
                mandatoryAddonCharges,
                cancellationToken).ConfigureAwait(false);
            result.FromPriceGross = result.FromOffer?.PricePerNightGross;
            result.Currency = result.FromOffer?.Currency ?? "PLN";

            return result;
        }

        /// <summary>"From X zł/night" anchor — the real, validated price (via the same suggested-date
        /// mechanism used elsewhere for "no offer for these dates, try these" alternatives) of the
        /// nearest available term to today, not an estimate. A 1-night reference range is used purely
        /// to ask "what's the closest available date" — the underlying search still finds and prices
        /// the actual nearest available term regardless of its real length/min-stay.
        /// AvailableTerm.MinimalPrice is the TOTAL price for the whole stay, not a nightly rate — when
        /// applyMandatoryAddonsFee is set (same flat-fee-adjusted divisor used by the apartments list
        /// page's suggested-date price, see Apartment.razor's UpdateSuggestionFromPriceAsync), the
        /// apartment's mandatory-addons total is added back per night rather than shown as a raw average.</summary>
        private async Task<ApartmentFromOfferDto?> GetNearestSuggestedOfferAsync(
            int objectId,
            DateOnly referenceDate,
            int adults,
            int children,
            bool applyMandatoryAddonsFee,
            IReadOnlyList<MandatoryAddonCharge>? mandatoryAddonCharges,
            CancellationToken cancellationToken)
        {
            var referenceStart = referenceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var referenceEnd = referenceDate.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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

            var fee = mandatoryAddonCharges?.Sum(c => AddonPricingCalculator.CalculateTotal(c.PaymentType, c.PriceGross, nights, adults + children, quantity: 1)) ?? 0m;

            if (!applyMandatoryAddonsFee)
            {
                var plainPerNight = (totalPrice - fee) / nights;
                return plainPerNight > 0
                    ? CreateFromOffer(plainPerNight, start, end, nights, adults, children)
                    : null;
            }

            var perNight = ((totalPrice - fee) / nights) + fee;
            return perNight > 0
                ? CreateFromOffer(perNight, start, end, nights, adults, children)
                : null;
        }

        private static ApartmentFromOfferDto CreateFromOffer(
            decimal pricePerNight,
            DateOnly start,
            DateOnly end,
            int nights,
            int adults,
            int children)
            => new()
            {
                PricePerNightGross = pricePerNight,
                Currency = "PLN",
                AvailabilityStarts = start,
                AvailabilityEnds = end,
                Nights = nights,
                Adults = adults,
                Children = children
            };

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
