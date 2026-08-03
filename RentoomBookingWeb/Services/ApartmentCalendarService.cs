using System.Globalization;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
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
        /// <summary>How far ahead of today the "from X zł/night" search looks for the cheapest
        /// available night — matches the window used by <see cref="ApartmentMinPriceService"/> for
        /// the listing-card price, so both "from" prices are computed the same way.</summary>
        private const int PriceSearchWindowMonths = 1;

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

            result.FromOffer = await GetCheapestAvailableOfferAsync(
                objectId,
                adults,
                children,
                applyMandatoryAddonsFee,
                mandatoryAddonCharges,
                cancellationToken).ConfigureAwait(false);
            result.FromPriceGross = result.FromOffer?.PricePerNightGross;
            result.Currency = result.FromOffer?.Currency ?? "PLN";

            return result;
        }

        /// <summary>"From X zł/night" anchor — the cheapest available (ItemsNumber &gt; 0) night within
        /// the next <see cref="PriceSearchWindowMonths"/> month(s) from today, sourced directly from
        /// IdoBooking's per-day availability+price feed. Uses the same window and availability-filter
        /// methodology as <see cref="ApartmentMinPriceService"/> (the listing-card "from" price), so
        /// both numbers are computed the same way instead of one being "nearest available date" and
        /// the other "cheapest in window".</summary>
        private async Task<ApartmentFromOfferDto?> GetCheapestAvailableOfferAsync(
            int objectId,
            int adults,
            int children,
            bool applyMandatoryAddonsFee,
            IReadOnlyList<MandatoryAddonCharge>? mandatoryAddonCharges,
            CancellationToken cancellationToken)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var windowEnd = today.AddMonths(PriceSearchWindowMonths);

            var payload = new OfferAvailabilityAndPricesParamsSearchInternal
            {
                ObjectIds = new List<int> { objectId },
                ParamsSearch = new OfferAvailabilityAndPricesParamsSearch
                {
                    DateFrom = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    DateTo = windowEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    AdultsNumber = adults,
                    ChildrenNumber = children > 0 ? children : null,
                    Language = "pol",
                    Currency = "PLN"
                }
            };

            var offerObjects = await _offerService
                .GetAvailabilityAndPricesForDaysAsync(payload, cancellationToken)
                .ConfigureAwait(false);

            var apartment = offerObjects?.FirstOrDefault(o => o.ObjectId == objectId);
            if (apartment is null)
            {
                return null;
            }

            var availableDates = (apartment.ObjectAvailability ?? Enumerable.Empty<OfferAvailabilityDate>())
                .Where(a => a.ItemsNumber > 0 && a.Date != null)
                .Select(a => a.Date!)
                .ToHashSet();

            var cheapestDay = (apartment.ObjectPricesDates ?? Enumerable.Empty<OfferPriceDate>())
                .Where(d => d.Price > 0 && d.Date != null && availableDates.Contains(d.Date!))
                .OrderBy(d => d.Price)
                .FirstOrDefault();

            if (cheapestDay is null || !TryParseDate(cheapestDay.Date, out var start))
            {
                return null;
            }

            var end = start.AddDays(1);
            var fee = mandatoryAddonCharges?.Sum(c => AddonPricingCalculator.CalculateTotal(c.PaymentType, c.PriceGross, nights: 1, adults + children, quantity: 1)) ?? 0m;
            var pricePerNight = applyMandatoryAddonsFee ? cheapestDay.Price + fee : cheapestDay.Price;

            return pricePerNight > 0
                ? CreateFromOffer(pricePerNight, start, end, nights: 1, adults, children)
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
