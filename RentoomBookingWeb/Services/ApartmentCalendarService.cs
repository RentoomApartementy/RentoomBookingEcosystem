using System.Globalization;
using RentoomBooking.SharedClasses.Models.IdoBooking;
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

        /// <summary>Lowest per-night price across available nights — the "from X zł/night" anchor.
        /// Sourced from the public-offer min price (same figure shown on the apartments list cards),
        /// since the availability-only endpoint carries no price data.</summary>
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

            var publicOffer = await _offerService.GetPublicOfferAsync(objectId, cancellationToken).ConfigureAwait(false);
            result.FromPriceGross = publicOffer?.MinimalPrice;

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
