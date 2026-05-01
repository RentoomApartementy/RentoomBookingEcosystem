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
                    return $"Wyniki wyszukiwania: {ViewModel.Offers.Count} ofert - Rentoom";
                
                return "Brak dostępnych apartamentów w tym terminie - Rentoom";
            }
            return "Luksusowe Apartamenty na Wynajem - Rentoom";
        }

        private string GetSeoDescription()
        {
            if (ViewModel.IsSearch && !string.IsNullOrEmpty(ViewModel.StartDate))
            {
                return $"Sprawdź dostępne apartamenty w terminie {ViewModel.StartDate} - {ViewModel.EndDate}. " +
                       $"Znaleziono {ViewModel.Offers.Count} ofert idealnych dla Ciebie. Rezerwuj bezpiecznie online.";
            }
            return "Odkryj szeroką ofertę apartamentów Rentoom. Komfort, świetne lokalizacje i wysoki standard. Idealne na wakacje i podróże służbowe.";
        }

        private string GetSeoImage()
        {
            return $"{NavManager.BaseUri}assets/images/header-bg-contact.jpeg";
        }
    }
    
}