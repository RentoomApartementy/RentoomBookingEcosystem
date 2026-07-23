using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.Blog;
using RentoomBooking.SharedClasses.Services.IdoBooking;

namespace RentoomBookingWeb.Components.Features.Blog.Components;

public partial class BlogApartmentsListing : ComponentBase
{
    /// <summary>
    /// Apartment ids selected in RentoomApp, in author order. An empty list means "all active apartments".
    /// </summary>
    [Parameter] public IReadOnlyList<int> ApartmentIds { get; set; } = Array.Empty<int>();

    /// <summary>
    /// When true (default), fetch and show the IdoBooking public offer price on the cards.
    /// When false, no public offer is fetched and the card shows a "price after choosing dates" hint instead.
    /// </summary>
    [Parameter] public bool ShowPublicOffer { get; set; } = true;

    [Inject] private IApartmentsService ApartmentsService { get; set; } = default!;
    [Inject] private IIdoOfferService OfferService { get; set; } = default!;
    [Inject] private IStringLocalizer<RentoomBookingWeb.Blog> BlogLocalizer { get; set; } = default!;
    [Inject] private ILogger<BlogApartmentsListing> Logger { get; set; } = default!;

    private IReadOnlyList<ApartmentObject> _apartments = Array.Empty<ApartmentObject>();
    private IReadOnlyDictionary<int, PublicApartmentOffer> _offers = new Dictionary<int, PublicApartmentOffer>();
    private bool _isLoading = true;
    private string? _loadedSignature;

    private string PriceHintText
    {
        get
        {
            var localized = BlogLocalizer["ApartmentsListingPriceAfterDate"];
            return localized.ResourceNotFound ? "Cena po wybraniu terminu" : localized.Value;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        var signature = $"{ShowPublicOffer}:{string.Join(",", ApartmentIds)}";
        if (!_isLoading && signature == _loadedSignature)
        {
            return; // Already resolved for this exact selection.
        }

        try
        {
            _isLoading = true;

            var allActive = (await ApartmentsService.GetAllApartmentsList()).Items ?? new List<ApartmentObject>();

            // Empty ids => all active in default order; explicit ids => those apartments in author order.
            _apartments = BlogApartmentSelection.SelectOrdered(allActive, ApartmentIds);

            // Public fallback prices are fetched server-side, cached per apartment, resilient to per-id failures.
            // Skipped entirely when the block is configured to hide public offers.
            _offers = ShowPublicOffer
                ? await OfferService.GetPublicOffersAsync(_apartments.Select(a => a.Id), CancellationToken.None)
                : new Dictionary<int, PublicApartmentOffer>();

            _loadedSignature = signature;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load apartments for blog ApartmentsListing block.");
            _apartments = Array.Empty<ApartmentObject>();
            _offers = new Dictionary<int, PublicApartmentOffer>();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private PublicApartmentOffer? GetOffer(int apartmentId)
        => _offers.TryGetValue(apartmentId, out var offer) ? offer : null;
}
