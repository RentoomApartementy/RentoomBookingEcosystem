using RentoomBooking.SharedClasses.Models;

namespace RentoomBooking.StayWell.States
{
/* https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management/?view=aspnetcore-9.0#url */
    public class ReservationState
    {
        private RentoomReservation? _reservation;

        public RentoomReservation? CurrentReservation
        {
            get => _reservation;
            private set
            {
                _reservation = value;
                NotifyStateChanged();
            }
        }

        public event Action? OnChange;
        public bool IsLoading { get; private set; }

        public void SetReservation(RentoomReservation? reservation)
        {
            CurrentReservation = reservation;
            IsLoading = false;
        }

        public void SetLoading(bool isLoading)
        {
            IsLoading = isLoading;
            NotifyStateChanged();
        }

        public void ClearReservation()
        {
            CurrentReservation = null;
            IsLoading = false;
        }

        public bool HasReservation()
        {
            return CurrentReservation != null;
        }

        private void NotifyStateChanged() => OnChange?.Invoke();


    }
}
