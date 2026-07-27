using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
using RentoomBookingWeb.Components.Features.Apartments;
using RentoomBookingWeb.Components.Features.Apartments.ViewModels;

namespace RentoomBookingWeb.Components.Features.Home.Components;

public partial class ApartmentsSection : ComponentBase, IAsyncDisposable
{
    [Inject] public IApartmentsViewModel ViewModel { get; set; } = default!;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Inject] private ILogger<ApartmentsSection> Logger { get; set; } = default!;

    /// <summary>Fetch and show the IdoBooking public offer price on the cards.</summary>
    [Parameter] public bool ShowPublicOffer { get; set; } = false;

    /// <summary>How the public offer price is applied. Default: only as a fallback when there is no dated offer.</summary>
    [Parameter] public ShowPublicOfferTypes ShowPublicOfferType { get; set; } = ShowPublicOfferTypes.AsFallback;

    /// <summary>Search for and show suggested available terms on the cards.</summary>
    [Parameter] public bool ShowSuggestions { get; set; } = true;

    // Enforce => always use the public price, so the dated offer is not fetched at all.
    private bool EnforcePublicOffer => ShowPublicOffer && ShowPublicOfferType == ShowPublicOfferTypes.Enforce;

    private ElementReference _carouselWrapper;
    private DotNetObjectReference<ApartmentsSection>? _objRef;
    private IJSObjectReference? _jsModule;
    private bool _isDisposed;
    private bool _isInteractive;
    private readonly CancellationTokenSource _initCts = new();

    private void HandleViewModelChange()
    {
        if (_isDisposed || !_isInteractive) return;
        InvokeAsync(StateHasChanged);
    }

    protected override async Task OnInitializedAsync()
    {
        ViewModel.OnChange += HandleViewModelChange;
        await ViewModel.InitializeForSliderAsync(
            showSuggestions: ShowSuggestions,
            showPublicOffer: ShowPublicOffer,
            fetchDatedOffers: !EnforcePublicOffer,
            _initCts.Token);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _isInteractive = true;
            _objRef = DotNetObjectReference.Create(this);
            _jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "./js/apartmentsSectionScroll.js");
            await _jsModule.InvokeVoidAsync("init", _objRef, _carouselWrapper);
        }
    }

    [JSInvokable]
    public async Task LoadMoreOnScroll()
    {
        if (!ViewModel.ApartmentsIsLoading && ViewModel.HasMore)
        {
            await ViewModel.LoadMoreAsync(_initCts.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _isDisposed = true;
        _initCts.Cancel();
        _initCts.Dispose();
        ViewModel.OnChange -= HandleViewModelChange;

        if (_jsModule is not null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("unregister");
            }
            catch (JSDisconnectedException)
            {
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to unregister apartments section infinite scroll JS hook.");
            }
        }

        _objRef?.Dispose();
        if (_jsModule != null)
        {
            try
            {
                await _jsModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to dispose apartments section infinite scroll JS module.");
            }
        }
    }
}