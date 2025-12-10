using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using RentoomBookingWeb.Models;

namespace RentoomBookingWeb.Services
{
    public interface IAvailabilityFinderService
    {
        Task<AvailableTerm?> FindNextAvailableTermAsync(int apartmentId, string? startDateStr, string? endDateStr, int adults, int children);
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

    var searchStart = DateTime.Now;
    var searchEnd = DateTime.Now.AddDays(30);

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
        var apartment = result.First();
        
        var availabilityCalendar = apartment.ObjectAvailability?
            .Where(x => DateTime.TryParse(x.Date, out _))
            .OrderBy(x => DateTime.Parse(x.Date))
            .ToList();

        if (availabilityCalendar != null && availabilityCalendar.Count >= desiredDuration)
        {
            for (int i = 0; i <= availabilityCalendar.Count - desiredDuration; i++)
            {
                bool isTermAvailable = true;

                for (int j = 0; j < desiredDuration; j++)
                {
                    var day = availabilityCalendar[i + j];
                    if (day.ItemsNumber <= 0)
                    {
                        isTermAvailable = false;
                        break; 
                    }
                }

                if (isTermAvailable)
                {
                    var firstDayStr = availabilityCalendar[i].Date;

                    if (DateTime.TryParse(firstDayStr, out var start))
                    {
                        return new AvailableTerm
                        {
                            StartDate = start.ToString("yyyy-MM-dd"),
                            EndDate = start.AddDays(desiredDuration).ToString("yyyy-MM-dd")
                        };
                    }
                }
            }
        }
    }

    return null;
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