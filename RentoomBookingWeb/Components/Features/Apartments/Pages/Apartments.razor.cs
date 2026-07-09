using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
using RentoomBookingWeb.Components.Features.Apartments.ViewModels;

namespace RentoomBookingWeb.Components.Features.Apartments.Pages
{
    public partial class Apartments : ComponentBase, IAsyncDisposable
    {
        [Inject]
        public IApartmentsViewModel ViewModel { get; set; } = default!;

        [Inject]
        public IJSRuntime JS { get; set; } = default!;
        
        [Inject]
        public NavigationManager NavManager { get; set; } = default!;

        [Inject]
        public ILogger<Apartments> Logger { get; set; } = default!;

        private DotNetObjectReference<Apartments>? _objRef;
        private IJSObjectReference? _jsModule;
        private bool _isDisposed;
        private bool _isInteractive;
        private readonly CancellationTokenSource _initCts = new();

        private string? _lastStartDate;
        private string? _lastEndDate;
        private string? _lastAdults;
        private string? _lastChildren;
        private bool _isFirstParametersSet = true;

        private void HandleViewModelChange()
        {
            if (_isDisposed || !_isInteractive) return;
            InvokeAsync(StateHasChanged);
        }

        protected override async Task OnInitializedAsync()
        {
            ViewModel.OnChange += HandleViewModelChange;
        }

        protected override async Task OnParametersSetAsync()
        {
            bool routeParamsChanged = 
                _isFirstParametersSet ||
                StartDate != _lastStartDate || 
                EndDate != _lastEndDate || 
                Adults != _lastAdults || 
                Children != _lastChildren;

            if (routeParamsChanged)
            {
                _isFirstParametersSet = false;
                _lastStartDate = StartDate;
                _lastEndDate = EndDate;
                _lastAdults = Adults;
                _lastChildren = Children;

                // Push new route params to the ViewModel (will clear parameters if null/empty)
                ViewModel.StartDate = StartDate ?? string.Empty;
                ViewModel.EndDate = EndDate ?? string.Empty;
                ViewModel.Adults = Adults ?? "2";
                ViewModel.Children = Children ?? "0";

                await ViewModel.InitializeAsync(_initCts.Token);
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                _isInteractive = true;
                _objRef = DotNetObjectReference.Create(this);
                _jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "./js/infiniteScroll.js");
                await _jsModule.InvokeVoidAsync("init", _objRef);
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

            if (_jsModule is not null && _objRef is not null)
            {
                try
                {
                    await _jsModule.InvokeVoidAsync("unregister", _objRef);
                }
                catch (JSDisconnectedException)
                {
                }
                catch (Exception ex)
                {
                    Logger.LogDebug(ex, "Failed to unregister apartments infinite scroll JS hook.");
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
                    Logger.LogDebug(ex, "Failed to dispose apartments infinite scroll JS module.");
                }
            }
        }
        
        private string GetSeoTitle()
        {
            if (ViewModel.IsSearch)
            {
                if (ViewModel.Offers.Count > 0)
                    return Localizer["Apartments_SeoTitleSearchWithOffers", ViewModel.Offers.Count];
                
                return Localizer["Apartments_SeoTitleNoOffers"];
            }
            return Localizer["Apartments_SeoTitleDefault"];
        }

        private string GetSeoDescription()
        {
            if (ViewModel.IsSearch && !string.IsNullOrEmpty(ViewModel.StartDate))
            {
                return Localizer[
                    "Apartments_SeoDescriptionSearch",
                    ViewModel.StartDate ?? string.Empty,
                    ViewModel.EndDate ?? string.Empty,
                    ViewModel.Offers.Count];
            }

            return Localizer["Apartments_SeoDescriptionDefault"];
        }

        private string GetSeoImage()
        {
            return $"{NavManager.BaseUri}assets/images/header-bg-contact.jpeg";
        }
    }
    
}
