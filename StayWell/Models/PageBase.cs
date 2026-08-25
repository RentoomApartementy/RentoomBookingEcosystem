using Microsoft.AspNetCore.Components;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.StayWell.Services;
using RentoomBooking.StayWell.States;
using System.Globalization;
using RentoomBooking.StayWell.Pages;
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

        protected virtual bool DeferSecondaryData => false;

        private readonly string _instanceId = Guid.NewGuid().ToString("N");
        protected override async Task OnInitializedAsync()
        {

            Subscribe();

            // Zamykamy bramkę treścifna czas bootstrapu/redirectu — MainLayout pokazuje loader
            // zamiast @Body, dzięki czemu strona, z której zaraz przekierowujemy, nie błyśnie treścią.
            LayoutState.IsContentReady = false;

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
                FinishInitialization();
                return;
            }

            if (!string.IsNullOrEmpty(Token))
            {
                await LoadDataAsync();

                if (!IsInitializedSuccessfully)
                {
                    NavigationManager.NavigateTo(ResolveNotFoundUrl());
                    return;
                }

                if (ReservationState.IsCanceledReservation)
                {
                    NavigationManager.NavigateTo("/NotFound?reason=canceled");
                    return;
                }

                FinishInitialization();
            }
        }

        private string ResolveNotFoundUrl()
        {
            if (ReservationState.IsCanceledReservation)
            {
                return "/NotFound?reason=canceled";
            }

            if (ReservationState.IsExpiredReservation)
            {
                return "/NotFound?reason=expired";
            }

            return "/NotFound";
        }


        private void FinishInitialization()
        {
            if (RegistrationCardState.CurrentCard == null && !IsOnRegistrationFlowRoute())
            {
                NavigationManager.NavigateTo($"/reservation/{Token}/Prearrival", replace: true);
                return;
            }

            ShouldRenderContent = true;
            LayoutState.IsContentReady = true;
        }

        private bool IsOnRegistrationFlowRoute()
            => NavigationManager.Uri.Contains("/Prearrival", StringComparison.OrdinalIgnoreCase)
               || NavigationManager.Uri.Contains("/TermsPage", StringComparison.OrdinalIgnoreCase)
               || NavigationManager.Uri.Contains("Registration", StringComparison.OrdinalIgnoreCase);

        private async Task SetLanguageAsync()
        {
            var savedPreference = await GlobalizationService.LoadPreferenceAsync();

            if (!string.IsNullOrWhiteSpace(savedPreference))
            {
                GlobalizationService.ForceSetCulture(savedPreference);
                Console.WriteLine($"Language set from local preference: {savedPreference}");
                return;
            }

            var lang = ReservationState.CurrentReservation?.Reservation?.Client?.Language;


            if (string.Equals(lang, "eng", StringComparison.OrdinalIgnoreCase)
                || string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase)
                || string.Equals(lang, "en-us", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(lang))
            {
                GlobalizationService.ForceSetCulture("en-US");
            }
            else if (string.Equals(lang, "pol", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(lang, "pl", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(lang, "pl-pl", StringComparison.OrdinalIgnoreCase))
            {
                GlobalizationService.ForceSetCulture("pl-PL");
            }
            else if (string.Equals(lang, "de", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(lang, "deu", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(lang, "de-de", StringComparison.OrdinalIgnoreCase))
            {
                GlobalizationService.ForceSetCulture("de-DE");
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

                // Karta meldunkowa jest potrzebna do bramki nawigacyjnej (FinishInitialization).
                var cardTask = RegistrationCardState.GetCardAsync(Token);

                if (DeferSecondaryData)
                {
                    // Do renderu wystarczy karta — resztę danych ładujemy w tle (nie blokują
                    // pierwszego renderu). Scoped-state'y i tak wypełnią się dla dalszych ekranów,
                    // a subskrypcje OnChange odświeżą widok gdy dane dojdą.
                    await cardTask;
                    _ = LoadSecondaryDataAsync(reservation, item);
                }
                else
                {
                    // Karta ładuje się równolegle z danymi wtórnymi — nie blokuje odkrycia
                    // obrazu LCP (miniatura apartamentu pochodzi z MediaState).
                    await Task.WhenAll(cardTask, LoadSecondaryDataAsync(reservation, item));
                }

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

        private async Task LoadSecondaryDataAsync(Reservation reservation, ReservationItem item)
        {
            try
            {
                await Task.WhenAll(
                    MediaState.GetMediaAsync(item.objectId),
                    ApartmentState.GetApartmentByIdAsync(item.objectId),
                    AmenitiesState.GetAmenitiesForObjectsAsync(item.objectId),
                    ApartmentState.GetDefinedAddonsAsync(),
                    ApartmentState.GetQrMaintFormUrlAsync(item.objectItemId),
                    ApartmentState.GetWifiInfoAsync(item.objectItemId),
                    // ApartmentState.GetArrivalInstructionStepsAsync(item.objectItemId), //<< to ma być tu wyłączone - spowalnia ładowanie strony! ładują się na stronie instrukcji tylko. nie ma potrzeby ładować ich tutaj
                    LocksState.GetLocksAsync(reservation.id, item.objectItemId),
                    LocksState.GetApartmentItemCodesAsync(Token)
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadSecondaryDataAsync failed: {ex}");
            }
        }

        private void Subscribe()
        {
            ReservationState.OnChange += HandleStateChange;
            MediaState.OnChange += HandleStateChange;
            AmenitiesState.OnChange += HandleStateChange;
            ApartmentState.OnChange += HandleStateChange;
            LocksState.OnChange += HandleStateChange;
            GlobalizationService.OnChange += HandleStateChange;
            LayoutState.OnChange += HandleStateChange;
            TermsState.OnChange += HandleStateChange;
            RegistrationCardState.OnChange += HandleStateChange;
        }

        private void HandleStateChange() => _ = InvokeAsync(StateHasChanged);

        public virtual void Dispose()
        {
            ReservationState.OnChange -= HandleStateChange;
            MediaState.OnChange -= HandleStateChange;
            AmenitiesState.OnChange -= HandleStateChange;
            ApartmentState.OnChange -= HandleStateChange;
            GlobalizationService.OnChange -= HandleStateChange;
            LocksState.OnChange -= HandleStateChange;
            LayoutState.OnChange -= HandleStateChange;
            TermsState.OnChange -= HandleStateChange;
            RegistrationCardState.OnChange -= HandleStateChange;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            // Redirect do Prearrival jest teraz obsługiwany w FinishInitialization() (przed renderem),
            // co eliminuje błysk treści. Nie powielamy tu nawigacji.
        }
    }
}
