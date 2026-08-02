using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using RentoomBookingWeb.Components.Features.Apartments.ViewModels;

namespace RentoomBookingWeb.Components.Features.Blog.Components;

public partial class BlogApartmentsListing : ComponentBase, IDisposable
{
    /// <summary>
    /// Apartment ids selected in RentoomApp, in author order. An empty list means "all active apartments".
    /// </summary>
    [Parameter] public IReadOnlyList<int> ApartmentIds { get; set; } = Array.Empty<int>();

    /// <summary>Search for and show suggested available terms on the cards.</summary>
    [Parameter] public bool ShowSuggestions { get; set; } = true;

    /// <summary>When true, always show only the cached backend "from" min-price on every card,
    /// skipping the dated-offer/suggestion cascade entirely.</summary>
    [Parameter] public bool ShowOnlyFromMinPrice { get; set; } = false;

    [Inject] private IApartmentsViewModel ViewModel { get; set; } = default!;
    [Inject] private ILogger<BlogApartmentsListing> Logger { get; set; } = default!;

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
        var signature = $"{ShowSuggestions}:{ShowOnlyFromMinPrice}:{string.Join(",", ApartmentIds)}";
        if (signature == _loadedSignature)
        {
            return; // Already resolved for this exact configuration.
        }

        _loadedSignature = signature;

        try
        {
            await ViewModel.InitializeForFixedApartmentsAsync(
                ApartmentIds,
                showSuggestions: ShowOnlyFromMinPrice ? false : ShowSuggestions,
                showPublicOffer: false,
                fetchDatedOffers: !ShowOnlyFromMinPrice);
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
