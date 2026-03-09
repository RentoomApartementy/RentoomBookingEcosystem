using RentoomBooking.SharedClasses.Models.AvailableTerms;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Models.RentoomBooking;

namespace RentoomBookingWeb.Components.Features.Apartments.ViewModels;

public interface IApartmentsViewModel
{
    List<ApartmentObject> Items { get; }
    List<PricingOffer> Offers { get; }
    long? ApartmentsCount { get; }
    bool IsLoading { get; }
    bool ApartmentsIsLoading { get; }
    bool HasMore { get; }
    string? Error { get; }
    bool IsMapView { get; }
    bool IsSearch { get; }

    string StartDate { get; set; }
    string EndDate { get; set; }
    string Adults { get; set; }
    string Children { get; set; }
    string Rooms { get; set; }
    int? FilterMinPrice { get; }
    int? FilterMaxPrice { get; }

    int MinOfferPrice { get; }
    int MaxOfferPrice { get; }
    int ScaleMinPrice { get; }
    int ScaleMaxPrice { get; }
    public Guid SliderResetKey { get; }
    PricingOffer? GetPricingOfferByObjectId(int objectId);
    public IReadOnlyList<AvailableTerm>? GetSuggestionByObjectId(int objectId);
    public IReadOnlyList<AvailableTerm>? GetSuggestionsByObjectId(int objectId);

    Task InitializeAsync();
    Task LoadMoreAsync();
    void ToggleView(bool isMap);
    Task HandleSearchAsync(Dictionary<string, string> query);
    Task HandleFiltersChangedAsync((ApartmentFilters Filters, int MinPrice, int MaxPrice) data);

    event Action? OnChange;
}
