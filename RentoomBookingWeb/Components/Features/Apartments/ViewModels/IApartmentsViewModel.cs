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
    bool IsSuggestionsLoading { get; }
    bool HasMore { get; }
    string? Error { get; }
    bool IsMapView { get; }
    bool IsSearch { get; }

    string StartDate { get; set; }
    string EndDate { get; set; }
    string Adults { get; set; }
    string Children { get; set; }
    int? FilterMinPrice { get; }
    int? FilterMaxPrice { get; }

    int MinOfferPrice { get; }
    int MaxOfferPrice { get; }
    int ScaleMinPrice { get; }
    int ScaleMaxPrice { get; }
    public Guid SliderResetKey { get; }
    PricingOffer? GetPricingOfferByObjectId(int objectId);
    PublicApartmentOffer? GetPublicOfferByObjectId(int objectId);
    SuggestionStatus GetSuggestionStatusByObjectId(int objectId);
    public IReadOnlyList<AvailableTerm>? GetSuggestionByObjectId(int objectId);
    public IReadOnlyList<AvailableTerm>? GetSuggestionsByObjectId(int objectId);

    Task InitializeAsync(CancellationToken ct = default);
    Task InitializeForSliderAsync(bool showSuggestions = true, bool showPublicOffer = false, bool fetchDatedOffers = true, CancellationToken ct = default);

    /// <summary>
    /// Loads a fixed, non-paginated set of apartments (an explicit id list, or all active apartments
    /// when the list is empty) with the same dated-offer/public-offer/suggestion pipeline used by
    /// <see cref="InitializeForSliderAsync"/>. Used by content blocks (e.g. the blog ApartmentsListing
    /// block) that need identical pricing behavior without infinite-scroll pagination.
    /// </summary>
    Task InitializeForFixedApartmentsAsync(
        IReadOnlyList<int> apartmentIds,
        bool showSuggestions = true,
        bool showPublicOffer = false,
        bool fetchDatedOffers = true,
        CancellationToken ct = default);

    Task LoadMoreAsync(CancellationToken cancellationToken = default);

    void ToggleView(bool isMap);
    Task HandleSearchAsync(Dictionary<string, string> query);
    Task HandleFiltersChangedAsync((ApartmentFilters Filters, int MinPrice, int MaxPrice) data);
    Task NavigateToApartmentAsync(int apartmentId, string? apartmentName, string listingSource, CancellationToken ct = default);
    int? GetOfferLengthDays();

    event Action? OnChange;
}