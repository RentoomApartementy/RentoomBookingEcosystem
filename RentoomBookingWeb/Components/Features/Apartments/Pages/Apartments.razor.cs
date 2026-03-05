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

        protected override async Task OnInitializedAsync()
        {
            ViewModel.OnChange += () => InvokeAsync(StateHasChanged);
            await ViewModel.InitializeAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
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
            ViewModel.OnChange -= StateHasChanged;

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