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
                    Currency = "PLN"
                }
            };

            var result = await _offerService.GetAvailabilityAndPricesForDaysAsync(paramsSearch);

            if (result != null && result.Any())
            {
                return FindClosestAvailableTerm(result.First(), desiredDuration, targetDate);
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
                    Currency = "PLN"
                }
            };

            var apiResult = await _offerService.GetAvailabilityAndPricesForDaysAsync(paramsSearch);

            if (apiResult != null)
            {
                foreach (var apartmentData in apiResult)
                {
                    var term = FindClosestAvailableTerm(apartmentData, desiredDuration, targetDate);

                    if (term != null)
                    {
                        results[apartmentData.ObjectId] = term;
                    }
                }
            }

            return results;
        }

        private AvailableTerm? FindClosestAvailableTerm(RentoomBooking.SharedClasses.Models.IdoBooking.OfferAvailabilityObject apartment, int duration, DateTime targetDate)
        {
            if (apartment.ObjectAvailability == null) return null;

            var availabilityCalendar = apartment.ObjectAvailability
                .Where(x => DateTime.TryParse(x.Date, out _))
                .OrderBy(x => DateTime.Parse(x.Date))
                .ToList();

            if (availabilityCalendar.Count < duration) return null;

            AvailableTerm? closestTerm = null;
            double minDifference = double.MaxValue;

            for (int i = 0; i <= availabilityCalendar.Count - duration; i++)
            {
                bool isTermAvailable = true;

                for (int j = 0; j < duration; j++)
                {
                    if (availabilityCalendar[i + j].ItemsNumber <= 0)
                    {
                        isTermAvailable = false;
                        break;
                    }
                }

                if (isTermAvailable)
                {
                    if (DateTime.TryParse(availabilityCalendar[i].Date, out var start))
                    {
                        double difference = Math.Abs((start - targetDate).TotalDays);

                        if (difference < minDifference)
                        {
                            minDifference = difference;
                            closestTerm = new AvailableTerm
                            {
                                StartDate = start.ToString("yyyy-MM-dd"),
                                EndDate = start.AddDays(duration).ToString("yyyy-MM-dd")
                            };
                        }
                    }
                }
            }

            return closestTerm;
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