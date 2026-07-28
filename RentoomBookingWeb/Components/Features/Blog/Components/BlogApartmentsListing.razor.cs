using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using RentoomBookingWeb.Components.Features.Apartments;
using RentoomBookingWeb.Components.Features.Apartments.ViewModels;

namespace RentoomBookingWeb.Components.Features.Blog.Components;

public partial class BlogApartmentsListing : ComponentBase, IDisposable
{
    /// <summary>
    /// Apartment ids selected in RentoomApp, in author order. An empty list means "all active apartments".
    /// </summary>
    [Parameter] public IReadOnlyList<int> ApartmentIds { get; set; } = Array.Empty<int>();

    /// <summary>Fetch and show the IdoBooking public offer price on the cards.</summary>
    [Parameter] public bool ShowPublicOffer { get; set; } = false;

    /// <summary>How the public offer price is applied. Default: only as a fallback when there is no dated offer.</summary>
    [Parameter] public ShowPublicOfferTypes ShowPublicOfferType { get; set; } = ShowPublicOfferTypes.AsFallback;

    /// <summary>Search for and show suggested available terms on the cards.</summary>
    [Parameter] public bool ShowSuggestions { get; set; } = true;

    [Inject] private IApartmentsViewModel ViewModel { get; set; } = default!;
    [Inject] private ILogger<BlogApartmentsListing> Logger { get; set; } = default!;

    // Enforce => always use the public price, so the dated offer is not fetched at all.
    private bool EnforcePublicOffer => ShowPublicOffer && ShowPublicOfferType == ShowPublicOfferTypes.Enforce;

    private string? _loadedSignature;
    private bool _hasLoadedOnce;
    private bool _isDisposed;

    protected override void OnInitialized()
    {
        ViewModel.OnChange += HandleViewModelChange;
    }

    private void HandleViewModelChange()
    {
        if (_isDisposed) return;
        InvokeAsync(StateHasChanged);
    }

    protected override async Task OnParametersSetAsync()
    {
        var signature = $"{ShowPublicOffer}:{ShowPublicOfferType}:{ShowSuggestions}:{string.Join(",", ApartmentIds)}";
        if (signature == _loadedSignature)
        {
            return; // Already resolved for this exact configuration.
        }

        _loadedSignature = signature;

        try
        {
            await ViewModel.InitializeForFixedApartmentsAsync(
                ApartmentIds,
                showSuggestions: ShowSuggestions,
                showPublicOffer: ShowPublicOffer,
                fetchDatedOffers: !EnforcePublicOffer);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load apartments for blog ApartmentsListing block.");
        }
        finally
        {
            _hasLoadedOnce = true;
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
        ViewModel.OnChange -= HandleViewModelChange;
    }
}
