using Microsoft.AspNetCore.Components;
using RentoomBooking.StayWell.States;
using ApartmentState = RentoomBooking.StayWell.States.ApartmentState;

namespace RentoomBooking.StayWell.Models
{
    public abstract class PageBase : ComponentBase
    {
        [Inject]
        protected ReservationState ReservationState { get; set; } = default!;
        [Inject]
        protected MediaState MediaState { get; set; } = default!;
        [Inject]
        protected AmenitiesState AmenitiesState { get; set; } = default!;
        [Inject]
        protected ApartmentState ApartmentState { get; set; } = default!;

        [Parameter]
        public string? Token { get; set; }

        protected bool IsLoading { get; set; } = true;
        protected Data? Data => new() //Pozostałość po alpejskich kombinacjach. Do usunięcia.
        {
            Reservation = ReservationState.CurrentReservation,
            Media = MediaState.CurrentMedia,
            Amenities = AmenitiesState.CurrentAmenities,
            Apartment = ApartmentState.CurrentApartment,
            Token = ReservationState.CurrentToken,
        };

        protected override async Task OnInitializedAsync()
        {
            if(ReservationState.CurrentReservation == null || MediaState.CurrentMedia == null || AmenitiesState.CurrentAmenities == null && !string.IsNullOrEmpty(Token))
            {
                await LoadDataAsync();
            }
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
                    if (reservation == null) return;
                    var item = reservation.Items?.FirstOrDefault();
                    if (item != null)
                    {
                        await Task.WhenAll(
                            MediaState.GetMediaAsync(item.objectId),
                            ApartmentState.GetApartmentByIdAsync(item.objectId),
                            AmenitiesState.GetAmenitiesForObjectsAsync(item.objectId)
                        );
                    }


                }
            }
            finally
            {
                IsLoading = false;
            }
        }

    }
}
