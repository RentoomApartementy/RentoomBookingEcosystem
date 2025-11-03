using Microsoft.AspNetCore.Components;
using RentoomBooking.SharedClasses.Models.IdoBooking;
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
        protected LocksState LocksState { get; set; } = default!;
        [Inject]
        protected ModalService ModalService { get; set; } = default!;
        [Inject]
        protected NavigationManager NavigationManager { get;set;} = default!;

        [Parameter]
        public string? Token { get; set; }

        protected bool IsLoading { get; set; } = true;

        //protected Data? Data => new() //Pozostałość po alpejskich kombinacjach. Do usunięcia.
        //{
        //    Reservation = ReservationState.CurrentReservation,
        //    Media = MediaState.CurrentMedia,
        //    Amenities = AmenitiesState.CurrentAmenities,
        //    Apartment = ApartmentState.CurrentApartment,
        //    Token = ReservationState.CurrentToken,
        //};

        protected override async Task OnInitializedAsync()
        {
            ReservationState.OnChange += StateHasChanged;
            MediaState.OnChange += StateHasChanged;
            AmenitiesState.OnChange += StateHasChanged;
            ApartmentState.OnChange += StateHasChanged;
            LocksState.OnChange += StateHasChanged;
            GlobalizationService.OnChange += StateHasChanged;

            if (ReservationState.CurrentReservation == null && !string.IsNullOrEmpty(Token))
            {
                await LoadDataAsync();
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
            IsLoading = true;
            try
            {
                if (!string.IsNullOrEmpty(Token))
                {
                    await ReservationState.GetReservationAsync(Token);
                    var reservation = ReservationState.CurrentReservation?.Reservation;
                    
                    if(!ReservationState.IsValidReservation || reservation == null)
                    {
                        NavigationManager.NavigateTo("/ReservationPage");
                        return;
                    }

                    SetLanguage();
                    var item = reservation.Items?.FirstOrDefault();
                    if (item != null)
                    {
                        await Task.WhenAll(
                            MediaState.GetMediaAsync(item.objectId),
                            ApartmentState.GetApartmentByIdAsync(item.objectId),
                            AmenitiesState.GetAmenitiesForObjectsAsync(item.objectId),
                            LocksState.GetLocksAsync(reservation.id, item.itemId)
                        );
                    }
                }
            }
            finally
            {
            IsLoading = false;
            StateHasChanged();
            }
        }

        public void Dispose()
        {
            ReservationState.OnChange -= StateHasChanged;
            MediaState.OnChange -= StateHasChanged;
            AmenitiesState.OnChange -= StateHasChanged;
            ApartmentState.OnChange -= StateHasChanged;
            GlobalizationService.OnChange -= StateHasChanged;
            LocksState.OnChange -= StateHasChanged;
        }

    }
}
