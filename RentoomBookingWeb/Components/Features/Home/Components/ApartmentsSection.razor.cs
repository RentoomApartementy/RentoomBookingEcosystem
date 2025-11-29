using Microsoft.AspNetCore.Components;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Services;


namespace RentoomBookingWeb.Components.Features.Home.Components;

public partial class ApartmentsSection : ComponentBase
{
    [Inject] private IApartmentsService ApartmentService { get; set; } = default!;
    [Inject] private IRentoomOfferService RentoomOfferService { get; set; } = default!;
    
    private string? _token;
    private const int _pageSize = 12;

    public List<ApartmentObject> Apartments { get; private set; } = new();
    public List<PricingOffer> Offers { get; private set; } = new();
    public bool ApartmentsIsLoading = false;

    public string? Error { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        await HandleSearchAsync();
    }

    private async Task HandleSearchAsync()
    {
        Apartments.Clear();
        Offers.Clear();
        _token = null;
        
        await LoadMoreAsync();
        
        await GetFilteredOffers();

        SortItemsByOffers();

        ApartmentsIsLoading = false;
    }
    
    private async Task GetFilteredOffers()
    {
        try
        {
            List<int> ids = Apartments.Select(a => a.Id).ToList();

            var idoParams = new PricingOffersRequest
            {
                ObjectIds = ids,
                DateFrom = DateTime.Now.ToString("yyyy-MM-dd"),
                DateTo = DateTime.Now.AddDays(6).ToString("yyyy-MM-dd"),
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
    
    public async Task LoadMoreAsync()
    {
        if (ApartmentsIsLoading) return;

        ApartmentsIsLoading = true;
        Error = null;

        try
        {
            var page = await ApartmentService.GetApartmentsByPageAsync(_token, top: _pageSize);
            if (page?.Items is { Count: > 0 })
            {
                Apartments.AddRange(page.Items);
                _token = page.ContinuationToken;
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            ApartmentsIsLoading = false;
        }
    }
    
}