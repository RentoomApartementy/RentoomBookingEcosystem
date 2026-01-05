using Microsoft.AspNetCore.Components;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.StayWell.Services;
using RentoomBooking.StayWell.States;
using System.Globalization;
using ApartmentState = RentoomBooking.StayWell.States.ApartmentState;

namespace RentoomBooking.StayWell.Models
{
    public abstract class PageBase : ComponentBase,IDisposable
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
        protected StayWell.Services.GlobalizationService GlobalizationService { get; set; } = default!;
        [Inject]
        protected StayWell.Services.BitrixService BitrixService{ get; set; } = default!;
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
        protected NavigationManager NavigationManager { get;set;} = default!;

        [Parameter]
        public string? Token { get; set; }

        protected bool IsLoading { get; set; } = true;

        private readonly string _instanceId = Guid.NewGuid().ToString("N");

        protected override async Task OnInitializedAsync()
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

            if (ReservationState.CurrentReservation == null && !string.IsNullOrEmpty(Token))
            {
                await LoadDataAsync();
                if (TermsState.CurrentTerms == null)
                {
                    NavigationManager.NavigateTo($"/reservation/{Token}/TermsPage");
                }
                //else
                //{
                //    // temporary
                //    NavigationManager.NavigateTo($"/reservation/{Token}/HomePage");
                //}
            }


        }

        private void SetLanguage()
        {
            var lang = ReservationState.CurrentReservation?.Reservation.Client.Language;

            if(lang == "eng" || lang == null)
            {
                GlobalizationService.SetCulture("en-US");
            }
            else if(lang == "pol")
            {
                GlobalizationService.SetCulture("pl-PL");
            }

            Console.WriteLine("Language set to: " + lang?.ToString() + " " + CultureInfo.CurrentCulture.Name);

        }

        private async Task LoadDataAsync()
        {
            Console.WriteLine($"LoadDataAsync {_instanceId}, Token={Token}");
            IsLoading = true;
            try
            {
                if (!string.IsNullOrEmpty(Token))
                {
                    await ReservationState.GetReservationAsync(Token);
                    var reservation = ReservationState.CurrentReservation?.Reservation;

                    if (ReservationState.CurrentStatus == System.Net.HttpStatusCode.NotFound)
                    {
                        Console.WriteLine("There is no reservation with such token");
                    }

                    if (!ReservationState.IsValidReservation || reservation == null)
                    {
                        Console.WriteLine("Rezerwacja wygasła");
                        //NavigationManager.NavigateTo("/ReservationPage");
                        //return;
                    }

                    SetLanguage();
                    var item = reservation.Items?.FirstOrDefault();
                    if (item != null)
                    {
                        await Task.WhenAll(
                            TermsState.GetTermsAsync(ReservationState.CurrentToken),
                            RegistrationCardState.GetCardAsync(ReservationState.CurrentToken),
                            MediaState.GetMediaAsync(item.objectId),
                            ApartmentState.GetApartmentByIdAsync(item.objectId),
                            ApartmentState.GetQrMaintFormUrlAsync(item.objectId),
                            AmenitiesState.GetAmenitiesForObjectsAsync(item.objectId),
                            LocksState.GetLocksAsync(reservation.id, item.itemId)
                        );
                    }
                }
            }catch
            {
                throw;
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
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
