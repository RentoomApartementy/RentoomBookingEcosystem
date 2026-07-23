using RentoomBooking.SharedClasses.Models.AvailableTerms;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Services.IdoBooking;

namespace RentoomBooking.SharedClasses.Services
{
    public interface IAvailabilityFinderService2
    {
        Task<List<ApartmentAvailableTermsResult>> FindAvailableTermsAsync(
            List<int>? apartmentIds,
            string? startDateStr,
            string? endDateStr,
            int adults,
            int children,
            CancellationToken cancellationToken = default);

        Task<ApartmentAvailableTermsResult> FindAvailableTermsForApartmentAsync(
            int apartmentId,
            string? startDateStr,
            string? endDateStr,
            int adults,
            int children,
            CancellationToken cancellationToken = default);
    }

    public class AvailabilityFinderService2 : IAvailabilityFinderService2
    {
        private const int RestrictionDaysBeforeStart = 7; //od kiedy pokazywac restrykcje(-7 lub od dzis)
        private const int RestrictionDaysAfterEnd = 14; // do kiedy (do+ X dni)
        private const int MaxEarlierArrivalDays = 0; // ile max dni przed poczatkiem zeby przyjezdzac (w sumie to zaweza bardziej RestrictionDaysBeforeStart)
        private const int TopTermsPerApartment = 3; //ile pokazywac dodatkowych termin�w

        private readonly IIdoOfferService _offerService;
        private readonly IdoSellService _idoSellService;

        public AvailabilityFinderService2(IIdoOfferService offerService, IdoSellService idoSellService)
        {
            _offerService = offerService;
            _idoSellService = idoSellService;
        }

        public async Task<ApartmentAvailableTermsResult> FindAvailableTermsForApartmentAsync(
            int apartmentId,
            string? startDateStr,
            string? endDateStr,
            int adults,
            int children,
            CancellationToken cancellationToken = default)
        {
            var results = await FindAvailableTermsAsync(
                new List<int> { apartmentId },
                startDateStr,
                endDateStr,
                adults,
                children,
                cancellationToken);

            return results.FirstOrDefault() ?? new ApartmentAvailableTermsResult
            {
                ApartmentId = apartmentId
            };
        }

        public async Task<List<ApartmentAvailableTermsResult>> FindAvailableTermsAsync(
            List<int>? apartmentIds,
            string? startDateStr,
            string? endDateStr,
            int adults,
            int children,
            CancellationToken cancellationToken = default)
        {
            if (!TryParseRequestedRange(startDateStr, endDateStr, out var requestedStart, out var requestedEnd, out var requestedNights))
            {
                return new List<ApartmentAvailableTermsResult>();
            }

            var uniqueApartmentIds = apartmentIds?
                .Distinct()
                .ToList();

            var restrictionsFrom = (new[] { DateTime.Now, requestedStart.AddDays(-RestrictionDaysBeforeStart) }).Max();
            var restrictionsTo = requestedEnd.AddDays(RestrictionDaysAfterEnd);

            cancellationToken.ThrowIfCancellationRequested();

            var restrictions = await FetchRestrictionsAsync(uniqueApartmentIds, restrictionsFrom, restrictionsTo, cancellationToken);
            var globalMaxLengthStay = restrictions
                .Select(r => r.LengthSetting?.LengthStay ?? 0)
                .Where(x => x > 0)
                .DefaultIfEmpty(0)
                .Max();

            var candidateStartFrom = requestedStart.AddDays(-MaxEarlierArrivalDays);
            if (candidateStartFrom < DateTime.Today)
            {
                candidateStartFrom = DateTime.Today;
            }

            var candidateStartTo = requestedEnd.AddDays(RestrictionDaysAfterEnd);
            if (candidateStartTo < candidateStartFrom)
            {
                candidateStartTo = candidateStartFrom;
            }

            var availabilityFrom = restrictionsFrom;
            var availabilityTo = restrictionsTo.AddDays(Math.Max(requestedNights, globalMaxLengthStay));

            cancellationToken.ThrowIfCancellationRequested();

            var availabilityObjects = await FetchAvailabilityAsync(uniqueApartmentIds, availabilityFrom, availabilityTo, adults, children, cancellationToken);

            var apartmentIdsToProcess = ResolveApartmentIdsToProcess(uniqueApartmentIds, availabilityObjects);
            if (!apartmentIdsToProcess.Any())
            {
                return new List<ApartmentAvailableTermsResult>();
            }

            var restrictionsLookup = BuildRestrictionsLookup(restrictions, apartmentIdsToProcess);
            var availabilityLookup = BuildAvailabilityLookup(availabilityObjects);

            var topCandidates = new Dictionary<int, List<AvailableTerm>>();

            foreach (var apartmentId in apartmentIdsToProcess)
            {
                cancellationToken.ThrowIfCancellationRequested();
                restrictionsLookup.TryGetValue(apartmentId, out var apartmentRestrictions);
                availabilityLookup.TryGetValue(apartmentId, out var availableNights);

                var candidates = BuildCandidatesForApartment(
                    apartmentRestrictions,
                    availableNights ?? new HashSet<DateTime>(),
                    candidateStartFrom,
                    candidateStartTo,
                    requestedStart,
                    requestedNights)
                    .Take(TopTermsPerApartment)
                    .ToList();

                topCandidates[apartmentId] = candidates;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var validated = await ValidateTopCandidatesWithPricingAsync(topCandidates, adults, children, cancellationToken);

            return apartmentIdsToProcess
                .OrderBy(id => id)
                .Select(id => new ApartmentAvailableTermsResult
                {
                    ApartmentId = id,
                    AvailableTerms = validated.TryGetValue(id, out var terms) ? terms : new List<AvailableTerm>()
                })
                .ToList();
        }

        private static bool TryParseRequestedRange(
            string? startDateStr,
            string? endDateStr,
            out DateTime requestedStart,
            out DateTime requestedEnd,
            out int requestedNights)
        {
            requestedStart = default;
            requestedEnd = default;
            requestedNights = 0;

            if (!DateTime.TryParse(startDateStr, out var parsedStart) || !DateTime.TryParse(endDateStr, out var parsedEnd))
            {
                return false;
            }

            requestedStart = parsedStart.Date;
            requestedEnd = parsedEnd.Date;
            requestedNights = (requestedEnd - requestedStart).Days;

            return requestedNights > 0;
        }

        private async Task<List<RestrictionException>> FetchRestrictionsAsync(
            List<int>? apartmentIds,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken)
        {
            var request = new GetRestrictionException
            {
                ObjectsIds = apartmentIds,
                RestrictionExceptionDateFrom = from.ToString("yyyy-MM-dd"),
                RestrictionExceptionDateTo = to.ToString("yyyy-MM-dd"),
                OfferType="nonrefundable" // - cxzy brac wszystkie czy tylk non-refund?
            };

            var restrictions = await _idoSellService.FetchRestrictionsExceptionsAsync(request, cancellationToken) ?? new List<RestrictionException>();
            if (apartmentIds == null || apartmentIds.Count == 0)
            {
                return restrictions;
            }

            var apartmentSet = apartmentIds.ToHashSet();
            return restrictions.Where(r => apartmentSet.Contains(r.ObjectId)).ToList();
        }

        private async Task<List<OfferAvailabilityObject>> FetchAvailabilityAsync(
            List<int>? apartmentIds,
            DateTime from,
            DateTime to,
            int adults,
            int children,
            CancellationToken cancellationToken)
        {
            var request = new OfferAvailabilityAndPricesParamsSearchInternal
            {
                ObjectIds = apartmentIds,
                ParamsSearch = new OfferAvailabilityAndPricesParamsSearch
                {
                    DateFrom = from.ToString("yyyy-MM-dd"),
                    DateTo = to.ToString("yyyy-MM-dd"),
                    AdultsNumber = adults,
                    ChildrenNumber = children > 0 ? children : null,
                    Currency = "PLN",
                    Language = "pol"
                    //MinStay = true
                }
            };

            return await _offerService.GetAvailabilityAndPricesForDaysAsync(request, cancellationToken) ?? new List<OfferAvailabilityObject>();
        }

        private static List<int> ResolveApartmentIdsToProcess(
            List<int>? apartmentIdsFilter,
            List<OfferAvailabilityObject> availabilityObjects)
        {
            if (apartmentIdsFilter != null && apartmentIdsFilter.Count > 0)
            {
                return apartmentIdsFilter;
            }

            return availabilityObjects
                .Select(o => o.ObjectId)
                .Distinct()
                .ToList();
        }

        private static Dictionary<int, Dictionary<DateTime, List<RestrictionException>>> BuildRestrictionsLookup(
            List<RestrictionException> restrictions,
            IEnumerable<int> apartmentIdsToProcess)
        {
            var result = apartmentIdsToProcess.ToDictionary(id => id, _ => new Dictionary<DateTime, List<RestrictionException>>());
            var apartmentSet = apartmentIdsToProcess.ToHashSet();

            foreach (var restriction in restrictions)
            {
                if (!apartmentSet.Contains(restriction.ObjectId))
                {
                    continue;
                }

                if (!DateTime.TryParse(restriction.RestrictionExceptionDate, out var parsedDate))
                {
                    continue;
                }

                var dateKey = parsedDate.Date;
                var apartmentRestrictions = result[restriction.ObjectId];

                if (!apartmentRestrictions.TryGetValue(dateKey, out var dayRestrictions))
                {
                    dayRestrictions = new List<RestrictionException>();
                    apartmentRestrictions[dateKey] = dayRestrictions;
                }

                dayRestrictions.Add(restriction);
            }

            return result;
        }

        private static Dictionary<int, HashSet<DateTime>> BuildAvailabilityLookup(List<OfferAvailabilityObject> availabilityObjects)
        {
            var result = new Dictionary<int, HashSet<DateTime>>();

            foreach (var apartmentAvailability in availabilityObjects)
            {
                var availableNights = new HashSet<DateTime>();

                foreach (var day in apartmentAvailability.ObjectAvailability ?? Enumerable.Empty<OfferAvailabilityDate>())
                {
                    if (day.ItemsNumber <= 0)
                    {
                        continue;
                    }

                    if (!DateTime.TryParse(day.Date, out var parsedDate))
                    {
                        continue;
                    }

                    availableNights.Add(parsedDate.Date);
                }

                result[apartmentAvailability.ObjectId] = availableNights;
            }

            return result;
        }

        private static IEnumerable<AvailableTerm> BuildCandidatesForApartment(
            Dictionary<DateTime, List<RestrictionException>>? restrictionsByDay,
            HashSet<DateTime> availableNights,
            DateTime candidateStartFrom,
            DateTime candidateStartTo,
            DateTime requestedStart,
            int requestedNights)
        {
            var rawCandidates = new List<(AvailableTerm Term, int DistanceDays, DateTime StartDate)>();

            for (var candidateStart = candidateStartFrom; candidateStart <= candidateStartTo; candidateStart = candidateStart.AddDays(1))
            {
                if (HasClosedToArrival(restrictionsByDay, candidateStart))
                {
                    continue;
                }

                var minLengthStay = GetMinLengthStay(restrictionsByDay, candidateStart);
                var effectiveNights = Math.Max(requestedNights, minLengthStay);
                if (effectiveNights <= 0)
                {
                    continue;
                }

                var candidateEnd = candidateStart.AddDays(effectiveNights);
                if (!IsRangeAvailable(availableNights, candidateStart, effectiveNights))
                {
                    continue;
                }

                var term = new AvailableTerm
                {
                    StartDate = candidateStart.ToString("yyyy-MM-dd"),
                    EndDate = candidateEnd.ToString("yyyy-MM-dd")
                };

                var distanceDays = Math.Abs((candidateStart - requestedStart).Days);
                rawCandidates.Add((term, distanceDays, candidateStart));
            }

            return rawCandidates
                .OrderBy(x => x.DistanceDays)
                .ThenBy(x => x.StartDate)
                .Select(x => x.Term);
        }

        private static bool HasClosedToArrival(
            Dictionary<DateTime, List<RestrictionException>>? restrictionsByDay,
            DateTime date)
        {
            if (restrictionsByDay == null || !restrictionsByDay.TryGetValue(date.Date, out var dayRestrictions))
            {
                return false;
            }

            return dayRestrictions.Any(r => r.ClosedToArrival);
        }

        private static int GetMinLengthStay(
            Dictionary<DateTime, List<RestrictionException>>? restrictionsByDay,
            DateTime date)
        {
            if (restrictionsByDay == null || !restrictionsByDay.TryGetValue(date.Date, out var dayRestrictions))
            {
                return 0;
            }

            return dayRestrictions
                .Select(r => r.LengthSetting?.LengthStay ?? 0)
                .Where(x => x > 0)
                .DefaultIfEmpty(0)
                .Max();
        }

        private static bool IsRangeAvailable(HashSet<DateTime> availableNights, DateTime startDate, int nights)
        {
            for (var offset = 0; offset < nights; offset++)
            {
                if (!availableNights.Contains(startDate.AddDays(offset)))
                {
                    return false;
                }
            }

            return true;
        }

        private async Task<Dictionary<int, List<AvailableTerm>>> ValidateTopCandidatesWithPricingAsync(
            Dictionary<int, List<AvailableTerm>> topCandidates,
            int adults,
            int children,
            CancellationToken cancellationToken)
        {
            if (topCandidates.Count == 0)
            {
                return new Dictionary<int, List<AvailableTerm>>();
            }

            var validatedTermKeys = new HashSet<TermKey>();
            var groupedByRange = topCandidates
                .SelectMany(kvp => kvp.Value.Select(term => new { ApartmentId = kvp.Key, Term = term }))
                .GroupBy(x => new DateRangeKey(x.Term.StartDate, x.Term.EndDate));

            foreach (var rangeGroup in groupedByRange)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var apartmentIds = rangeGroup
                    .Select(x => x.ApartmentId)
                    .Distinct()
                    .ToList();

                var pricingRequest = new PricingOffersRequest
                {
                    ObjectIds = apartmentIds,
                    DateFrom = rangeGroup.Key.DateFrom,
                    DateTo = rangeGroup.Key.DateTo,
                    NumberOfAdults = adults,
                    NumberOfBigChildren = children,
                    Currency = "PLN",
                    Language = "pol"
                };

                var response = await _offerService.GetPricingOffersAsync(pricingRequest, cancellationToken);
                var offersByApartmentId = response?.Result?.PricingOffers?
                    .Where(o => o.Offers != null && o.Offers.Any())
                    .GroupBy(o => o.ObjectId)
                    .ToDictionary(group => group.Key, group => group.First())
                    ?? new Dictionary<int, PricingOffer>();

                foreach (var item in rangeGroup)
                {
                    if (offersByApartmentId.TryGetValue(item.ApartmentId, out var offer))
                    {
                        validatedTermKeys.Add(new TermKey(item.ApartmentId, item.Term.StartDate, item.Term.EndDate));
                        item.Term.MinimalPrice = offer.MinimalPrice;
                    }
                }
            }

            return topCandidates.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value
                    .Where(term => validatedTermKeys.Contains(new TermKey(kvp.Key, term.StartDate, term.EndDate)))
                    .ToList());
        }

        private readonly record struct DateRangeKey(string DateFrom, string DateTo);
        private readonly record struct TermKey(int ApartmentId, string StartDate, string EndDate);
    }
}

