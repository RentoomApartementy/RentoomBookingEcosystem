using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
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

        private DotNetObjectReference<Apartments>? _objRef;
        private IJSObjectReference? _jsModule;
        private bool _isDisposed;
        private bool _isInteractive;
        private readonly CancellationTokenSource _initCts = new();

        private string? _lastStartDate;
        private string? _lastEndDate;
        private string? _lastAdults;
        private string? _lastChildren;

        private void HandleViewModelChange()
        {
            if (_isDisposed || !_isInteractive) return;
            InvokeAsync(StateHasChanged);
        }

        protected override async Task OnInitializedAsync()
        {
            ViewModel.OnChange += HandleViewModelChange;
            _offerLength = CalculateOfferLength();
            await ViewModel.InitializeAsync(_initCts.Token);
        }

        protected override async Task OnParametersSetAsync()
        {
            bool routeParamsChanged = 
                StartDate != _lastStartDate || 
                EndDate != _lastEndDate || 
                Adults != _lastAdults || 
                Children != _lastChildren;

            if (routeParamsChanged)
            {
                _lastStartDate = StartDate;
                _lastEndDate = EndDate;
                _lastAdults = Adults;
                _lastChildren = Children;

                // If we have dates in the route, we ensure they are pushed to the ViewModel 
                // BEFORE the re-initialization.
                if (!string.IsNullOrEmpty(StartDate) && !string.IsNullOrEmpty(EndDate))
                {
                    ViewModel.StartDate = StartDate;
                    ViewModel.EndDate = EndDate;
                    ViewModel.Adults = Adults ?? "2";
                    ViewModel.Children = Children ?? "0";

                    _offerLength = CalculateOfferLength();
                    await ViewModel.InitializeAsync(_initCts.Token);
                }
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
                await ViewModel.LoadMoreAsync();
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
                await _jsModule.InvokeVoidAsync("unregister", _objRef);
            }

            _objRef?.Dispose();
            if (_jsModule != null)
            {
                await _jsModule.DisposeAsync();
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
