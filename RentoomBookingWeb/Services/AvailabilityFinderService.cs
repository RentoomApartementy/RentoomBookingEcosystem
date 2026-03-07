using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using RentoomBookingWeb.Models;

namespace RentoomBookingWeb.Services
{
    public interface IAvailabilityFinderService
    {
        Task<AvailableTerm?> FindNextAvailableTermAsync(int apartmentId, string? startDateStr, string? endDateStr, int adults, int children);
        Task<Dictionary<int, AvailableTerm>> FindNextAvailableTermsAsync(List<int> apartmentIds, string? startDateStr, string? endDateStr, int adults, int children);
    }

    public class AvailabilityFinderService : IAvailabilityFinderService
    {
        private readonly IIdoOfferService _offerService;

        public AvailabilityFinderService(IIdoOfferService offerService)
        {
            _offerService = offerService;
        }

        public async Task<AvailableTerm?> FindNextAvailableTermAsync(int apartmentId, string? startDateStr, string? endDateStr, int adults, int children)
        {
            int desiredDuration = CalculateDaysCount(startDateStr, endDateStr);
            if (desiredDuration <= 0) return null;

            if (!DateTime.TryParse(startDateStr, out var targetDate))
            {
                targetDate = DateTime.Now;
            }

            var searchStart = targetDate.AddDays(-30);
            if (searchStart < DateTime.Now) searchStart = DateTime.Now;
            
            var searchEnd = targetDate.AddDays(30);

            var paramsSearch = new OfferAvailabilityAndPricesParamsSearchInternal
            {
                ObjectIds = new List<int> { apartmentId },
                ParamsSearch = new OfferAvailabilityAndPricesParamsSearch
                {
                    DateFrom = searchStart.ToString("yyyy-MM-dd"),
                    DateTo = searchEnd.ToString("yyyy-MM-dd"),
                    Language = "pol",
                    AdultsNumber = adults,
                    ChildrenNumber = children,
                    Currency = "PLN",
                    MinStay = true
                }
            };

            var availabilityResult = await _offerService.GetAvailabilityAndPricesForDaysAsync(paramsSearch);

            if (availabilityResult != null && availabilityResult.Any())
            {
                var candidates = FindPotentialAvailableTerms(availabilityResult.First(), desiredDuration, targetDate, 10);
                
                foreach (var candidate in candidates)
                {
                    if (await IsTermTrulyAvailableAsync(apartmentId, candidate, adults, children))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        public async Task<Dictionary<int, AvailableTerm>> FindNextAvailableTermsAsync(List<int> apartmentIds, string? startDateStr, string? endDateStr, int adults, int children)
        {
            var results = new Dictionary<int, AvailableTerm>();

            if (apartmentIds == null || !apartmentIds.Any()) return results;
            int desiredDuration = CalculateDaysCount(startDateStr, endDateStr);
            if (desiredDuration <= 0) return results;

            if (!DateTime.TryParse(startDateStr, out var targetDate))
            {
                targetDate = DateTime.Now;
            }

            var searchStart = targetDate.AddDays(-30);
            if (searchStart < DateTime.Now) searchStart = DateTime.Now;

            var searchEnd = targetDate.AddDays(30);

            var paramsSearch = new OfferAvailabilityAndPricesParamsSearchInternal
            {
                ObjectIds = apartmentIds,
                ParamsSearch = new OfferAvailabilityAndPricesParamsSearch
                {
                    DateFrom = searchStart.ToString("yyyy-MM-dd"),
                    DateTo = searchEnd.ToString("yyyy-MM-dd"),
                    Language = "pol",
                    AdultsNumber = adults,
                    ChildrenNumber = children,
                    Currency = "PLN",
                    MinStay = true
                }
            };

            var apiResult = await _offerService.GetAvailabilityAndPricesForDaysAsync(paramsSearch);

            if (apiResult != null)
            {
                foreach (var apartmentData in apiResult)
                {
                    var candidates = FindPotentialAvailableTerms(apartmentData, desiredDuration, targetDate, 3);
                    
                    foreach (var candidate in candidates)
                    {
                        if (await IsTermTrulyAvailableAsync(apartmentData.ObjectId, candidate, adults, children))
                        {
                            results[apartmentData.ObjectId] = candidate;
                            break;
                        }
                    }
                }
            }

            return results;
        }

        private async Task<bool> IsTermTrulyAvailableAsync(int apartmentId, AvailableTerm term, int adults, int children)
        {
            var pricingRequest = new RentoomBooking.SharedClasses.Models.IdoBooking.Public.PricingOffersRequest
            {
                ObjectIds = new List<int> { apartmentId },
                DateFrom = term.StartDate,
                DateTo = term.EndDate,
                NumberOfAdults = adults,
                NumberOfBigChildren = children,
                Currency = "PLN",
                Language = "pol"
            };

            var response = await _offerService.GetPricingOffersAsync(pricingRequest);
            
            return response?.Result?.PricingOffers != null && 
                   response.Result.PricingOffers.Any(o => o.ObjectId == apartmentId && o.Offers != null && o.Offers.Any());
        }

        private List<AvailableTerm> FindPotentialAvailableTerms(OfferAvailabilityObject apartment, int duration, DateTime targetDate, int maxCount)
        {
            var candidates = new List<AvailableTerm>();
            if (apartment.ObjectAvailability == null) return candidates;

            var availabilityCalendar = apartment.ObjectAvailability
                .Where(x => DateTime.TryParse(x.Date, out _))
                .Select(x => new { Data = x, DateParsed = DateTime.Parse(x.Date) })
                .OrderBy(x => x.DateParsed)
                .ToList();

            if (availabilityCalendar.Count < duration) return candidates;

            var potentialTerms = new List<(AvailableTerm Term, double Difference)>();

            for (int i = 0; i <= availabilityCalendar.Count - duration; i++)
            {
                var startEntry = availabilityCalendar[i];

               // if (startEntry.Data.ClosedToArrival == true) continue;

                if (i + duration < availabilityCalendar.Count)
                {
                    var checkoutEntry = availabilityCalendar[i + duration];
                    if (checkoutEntry.DateParsed == startEntry.DateParsed.AddDays(duration))
                    {
                 //       if (checkoutEntry.Data.ClosedToDeparture == true) continue;
                    }
                }

                bool isTermAvailable = true;

                for (int j = 0; j < duration; j++)
                {
                    var currentEntry = availabilityCalendar[i + j];

                    if (currentEntry.DateParsed != startEntry.DateParsed.AddDays(j))
                    {
                        isTermAvailable = false;
                        break;
                    }

                    if (currentEntry.Data.ItemsNumber <= 0)
                    {
                        isTermAvailable = false;
                        break;
                    }

                    if (currentEntry.Data.MinStay.HasValue && duration < currentEntry.Data.MinStay.Value)
                    {
                        isTermAvailable = false;
                        break;
                    }
                }

                if (isTermAvailable)
                {
                    double difference = Math.Abs((startEntry.DateParsed - targetDate).TotalDays);
                    potentialTerms.Add((new AvailableTerm
                    {
                        StartDate = startEntry.DateParsed.ToString("yyyy-MM-dd"),
                        EndDate = startEntry.DateParsed.AddDays(duration).ToString("yyyy-MM-dd")
                    }, difference));
                }
            }

            return potentialTerms
                .OrderBy(x => x.Difference)
                .Take(maxCount)
                .Select(x => x.Term)
                .ToList();
        }

        private int CalculateDaysCount(string? startDateStr, string? endDateStr)
        {
            if (string.IsNullOrEmpty(startDateStr) || string.IsNullOrEmpty(endDateStr)) return 0;
            if (DateTime.TryParse(startDateStr, out var start) && DateTime.TryParse(endDateStr, out var end))
            {
                return (end - start).Days;
            }
            return 0;
        }
    }
}