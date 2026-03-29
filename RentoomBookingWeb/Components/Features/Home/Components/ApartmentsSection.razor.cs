using Microsoft.AspNetCore.Components;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Services;
using RentoomBookingWeb.Helpers;
using RentoomBookingWeb.Services;

namespace RentoomBookingWeb.Components.Features.Home.Components;
public partial class ApartmentsSection : ComponentBase
{
    [Inject] private IRentoomOfferService RentoomOfferService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ReservationWorkflowTelemetry WorkflowTelemetry { get; set; } = default!;
    [Inject] private GoogleAnalyticsService GoogleAnalytics { get; set; } = default!;
    
    public List<ApartmentObject> Apartments { get; private set; } = new();
    public List<PricingOffer> Offers { get; private set; } = new();
    public bool ApartmentsIsLoading = true;

    public string? Error { get; private set; }

    public DateOnly _dateFrom { get; private set; } = DateOnly.FromDateTime(DateTime.Now);
    public DateOnly _dateTo { get; private set; } = DateOnly.FromDateTime((DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday) ? DateTime.Now.AddDays(2) : DateTime.Now.AddDays(1));



    protected override async Task OnInitializedAsync()
    {
        await HandleSearchAsync();
    }

    private async Task HandleSearchAsync()
    {
        Apartments.Clear();
        Offers.Clear();
        
        await GetFilteredOffers();

        SortItemsByOffers();

        ApartmentsIsLoading = false;
    }
    
    private async Task GetFilteredOffers()
    {

      
        try
        {
            var idoParams = new PricingOffersRequest
            {
                ObjectIds = [],
                DateFrom = _dateFrom.ToString("yyyy-MM-dd"),
                DateTo = _dateTo.ToString("yyyy-MM-dd"),
                NumberOfAdults = 2,
                NumberOfBigChildren = 0,
            };

            var filters = new RentoomQueryOffer
            {
                IdoOfferParams = idoParams,
                ApartmentFilterParams = new ApartmentFilters()
            };

            var filteredOffers = await RentoomOfferService.getOfferWitFilter(filters);
            if (filteredOffers?.PricingOffers != null)
            {
                Apartments.AddRange(filteredOffers.ApartmentObjects);
                Offers.AddRange(filteredOffers.PricingOffers);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Wystąpił błąd: {ex.Message}");
        }
    }
    
    private void SortItemsByOffers()
    {
        Apartments.Sort((a, b) =>
        {
            var offerA = GetPricingOfferByObjectId(a.Id);
            var offerB = GetPricingOfferByObjectId(b.Id);
        
            if (offerA != null && offerB == null) return -1;
        
            if (offerA == null && offerB != null) return 1;

            if (offerA != null && offerB != null)
            {
                return offerB.MinimalPrice.CompareTo(offerA.MinimalPrice);
            }

            return 0; 
        });
    }
    
    public PricingOffer? GetPricingOfferByObjectId(int objectId)
        => Offers.Find(o => o.ObjectId == objectId);

    public async Task GoToApartmentWithOffer(int apartmentId, string apartmentName)
    {
        WorkflowTelemetry.TrackEvent(
            "HomeApartmentClicked",
            new Dictionary<string, string?>
            {
                ["ApartmentId"] = apartmentId.ToString(),
                ["ApartmentName"] = apartmentName,
                ["StartDate"] = _dateFrom.ToString("yyyy-MM-dd"),
                ["EndDate"] = _dateTo.ToString("yyyy-MM-dd"),
                ["Adults"] = "2",
                ["Children"] = "0"
            });
        await GoogleAnalytics.TrackEventAsync("home_apartment_click", new Dictionary<string, object?>
        {
            ["apartment_id"] = apartmentId,
            ["apartment_name"] = apartmentName,
            ["listing_source"] = "home-carousel",
            ["start_date"] = _dateFrom.ToString("yyyy-MM-dd"),
            ["end_date"] = _dateTo.ToString("yyyy-MM-dd"),
            ["adults"] = 2,
            ["children"] = 0
        });
        Navigation.NavigateTo($"/apartamenty/{apartmentId}/{apartmentName.ToSlug()}/{_dateFrom:yyyy-MM-dd}/{_dateTo:yyyy-MM-dd}/2/0");
    }
}
