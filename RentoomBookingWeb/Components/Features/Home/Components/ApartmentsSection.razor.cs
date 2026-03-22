using Microsoft.AspNetCore.Components;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Services;
using RentoomBookingWeb.Helpers;

namespace RentoomBookingWeb.Components.Features.Home.Components;
public partial class ApartmentsSection : ComponentBase
{
    [Inject] private IRentoomOfferService RentoomOfferService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    
    public List<ApartmentObject> Apartments { get; private set; } = new();
    public List<PricingOffer> Offers { get; private set; } = new();
    public bool ApartmentsIsLoading = true;

    public string? Error { get; private set; }

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
                DateFrom = DateTime.Now.ToString("yyyy-MM-dd"),
                DateTo = (DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday) ?  DateTime.Now.AddDays(2).ToString("yyyy-MM-dd"): DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"),
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

    public void GoToApartmentWithOffer(int apartmentId, string apartmentName)
    {
        Navigation.NavigateTo($"/apartamenty/{apartmentId}/{apartmentName.ToSlug()}/{DateTime.Now.ToString("yyyy-MM-dd")}/{DateTime.Now.AddDays(1).ToString("yyyy-MM-dd")}/2/0");
    }
}