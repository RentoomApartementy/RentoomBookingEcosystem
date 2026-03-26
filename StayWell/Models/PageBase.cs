using Microsoft.AspNetCore.Components;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.StayWell.Services;
using RentoomBooking.StayWell.States;
using System.Globalization;
using ApartmentState = RentoomBooking.StayWell.States.ApartmentState;

namespace RentoomBooking.StayWell.Models
{
    public abstract class PageBase : ComponentBase, IDisposable
    {
        [Inject]
        protected ReservationState ReservationState { get; set; } = default!;
        [Inject]
        protected MediaState MediaState { get; set; } = default!;
        [Inject]
        protected AmenitiesState AmenitiesState { get; set; } = default!;
        [Inject]
        protected ApartmentState ApartmentState { get; set; } = default!;
        [Inject]
        protected GlobalizationService GlobalizationService { get; set; } = default!;
        [Inject]
        protected BitrixService BitrixService { get; set; } = default!;
        [Inject]
        protected LocksState LocksState { get; set; } = default!;
        [Inject]
        protected LayoutState LayoutState { get; set; } = default!;
        [Inject]
        protected TermsState TermsState { get; set; } = default!;
        [Inject]
        protected RegistrationCardState RegistrationCardState { get; set; } = default!;
        [Inject]
        protected ModalService ModalService { get; set; } = default!;
        [Inject]
        protected ToastService ToastService { get; set; } = default!;
        [Inject]
        protected NavigationManager NavigationManager { get; set; } = default!;

        [Parameter]
        public string? Token { get; set; }

        [Parameter]
        public bool IsDisabled { get; set; } = false;
        protected bool IsLoading { get; set; } = true;

        protected bool ShouldRenderContent { get; private set; } = false;
        protected bool IsInitializedSuccessfully { get; private set; } = false;

        private readonly string _instanceId = Guid.NewGuid().ToString("N");

        protected override async Task OnInitializedAsync()
        {

            Subscribe();

            if (IsDisabled)
            {
                NavigationManager.NavigateTo($"/reservation/{Token}/");
                //ToastService.ShowToast("Nie masz dostepu do tej strony.");
                return;
            }

            if (ReservationState.CurrentReservation != null)
            {
                IsLoading = false;
                IsInitializedSuccessfully = true;
                //SetLanguage();
                return;
            }

            if (!string.IsNullOrEmpty(Token))
            {
                await LoadDataAsync();

                if (!IsInitializedSuccessfully)
                {
                    NavigationManager.NavigateTo("/Error");
                    return;
                }
                
                if (RegistrationCardState.CurrentCard == null)
                {
                    NavigationManager.NavigateTo($"/reservation/{Token}/Prearrival");
                }
            }
        }

        private async Task SetLanguageAsync()
        {
            var savedPreference = await GlobalizationService.LoadPreferenceAsync();

            if (!string.IsNullOrWhiteSpace(savedPreference))
            {
                GlobalizationService.SetCulture(savedPreference);
                Console.WriteLine($"Language set from local preference: {savedPreference}");
                return;
            }

            var lang = ReservationState.CurrentReservation?.Reservation?.Client?.Language;


            if (string.Equals(lang, "eng", StringComparison.OrdinalIgnoreCase)
                || string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase)
                || string.Equals(lang, "en-us", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(lang))
            {
                GlobalizationService.SetCulture("en-US");
            }
            else if (string.Equals(lang, "pol", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(lang, "pl", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(lang, "pl-pl", StringComparison.OrdinalIgnoreCase))
            {
                GlobalizationService.SetCulture("pl-PL");
            }
            else if (string.Equals(lang, "de", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(lang, "deu", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(lang, "de-de", StringComparison.OrdinalIgnoreCase))
            {
                GlobalizationService.SetCulture("de-DE");
            }

            //Console.WriteLine($"Language set from API: {lang} → {CultureInfo.CurrentCulture.Name}");
        }

        private async Task LoadDataAsync()
        {
            Console.WriteLine($"LoadDataAsync {_instanceId}, Token={Token}");
            IsLoading = true;
            try
            {
                if (string.IsNullOrEmpty(Token))
                {
                    NavigationManager.NavigateTo("/Error");
                    return;
                }

                await ReservationState.GetReservationAsync(Token);
                var reservation = ReservationState.CurrentReservation?.Reservation;

                if (ReservationState.CurrentStatus == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine("There is no reservation with such token");
                    return;
                }

                if (!ReservationState.IsValidReservation || reservation == null)
                {
                    Console.WriteLine("Rezerwacja wygasła");
                    return;
                }

                await SetLanguageAsync();

                var item = reservation.Items?.FirstOrDefault();
                if (item is null)
                {
                    return;
                }

                await Task.WhenAll(
                    //TermsState.GetTermsAsync(Token),
                    RegistrationCardState.GetCardAsync(Token)
                );

                await Task.WhenAll(
                    ApartmentState.GetApartmentByIdAsync(item.objectId),
                    MediaState.GetMediaAsync(item.objectId),
                    AmenitiesState.GetAmenitiesForObjectsAsync(item.objectId)
                );

                await Task.WhenAll(
                    ApartmentState.GetDefinedAddonsAsync(),
                    ApartmentState.GetQrMaintFormUrlAsync(item.objectItemId),
                    ApartmentState.GetWifiInfoAsync(item.objectItemId),
                    // ApartmentState.GetArrivalInstructionStepsAsync(item.objectItemId), //<< to ma być tu wyłączone - spowalnia ładowanie strony! ładują się na stronie instrukcji tylko. nie ma potrzeby ładować ich tutaj
                    LocksState.GetLocksAsync(reservation.id, item.objectItemId),
                    LocksState.GetApartmentItemCodesAsync(Token)
                );

                //await Task.WhenAll(
                //    TermsState.GetTermsAsync(Token),
                //    RegistrationCardState.GetCardAsync(Token),
                //    MediaState.GetMediaAsync(item.objectId),
                //    ApartmentState.GetApartmentByIdAsync(item.objectId),
                //    ApartmentState.GetDefinedAddonsAsync(),
                //    ApartmentState.GetQrMaintFormUrlAsync(item.objectId),
                //    ApartmentState.GetWifiInfoAsync(item.objectId),
                //    ApartmentState.GetArrivalInstructionStepsAsync(item.objectItemId),
                //    AmenitiesState.GetAmenitiesForObjectsAsync(item.objectId),
                //    LocksState.GetLocksAsync(reservation.id, item.itemId),
                //    LocksState.GetApartmentItemCodesAsync(Token)
                //);
                IsInitializedSuccessfully = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadDataAsync failed: {ex}");
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }

        private void Subscribe()
        {
            ReservationState.OnChange += StateHasChanged;
            MediaState.OnChange += StateHasChanged;
            AmenitiesState.OnChange += StateHasChanged;
            ApartmentState.OnChange += StateHasChanged;
            LocksState.OnChange += StateHasChanged;
            GlobalizationService.OnChange += StateHasChanged;
            LayoutState.OnChange += StateHasChanged;
            TermsState.OnChange += StateHasChanged;
            RegistrationCardState.OnChange += StateHasChanged;
        }
        public virtual void Dispose()
        {
            ReservationState.OnChange -= StateHasChanged;
            MediaState.OnChange -= StateHasChanged;
            AmenitiesState.OnChange -= StateHasChanged;
            ApartmentState.OnChange -= StateHasChanged;
            GlobalizationService.OnChange -= StateHasChanged;
            LocksState.OnChange -= StateHasChanged;
            LayoutState.OnChange -= StateHasChanged;
            TermsState.OnChange -= StateHasChanged;
            RegistrationCardState.OnChange -= StateHasChanged;
        }
    }
}
