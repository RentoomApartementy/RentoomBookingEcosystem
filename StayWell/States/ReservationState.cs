using RentoomBooking.SharedClasses.Models;
using RentoomBooking.StayWell.Services;

namespace RentoomBooking.StayWell.States
{
/* https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management/?view=aspnetcore-9.0#url */
    public class ReservationState(BackendApi backendApi)
    {
        private RentoomReservation? _reservation;
        private readonly BackendApi _backendApi = backendApi;
        private string? _currentToken;

        public RentoomReservation? CurrentReservation
        {
            get => _reservation;
            private set
            {
                _reservation = value;
                NotifyStateChanged();
            }
        }

        public string? CurrentToken => _currentToken;

        public async Task<RentoomReservation?> GetReservationAsync(string token)
        {
            if (_currentToken == token) return CurrentReservation;

            SetLoading(true);
            try
            {
                if (_backendApi == null) 
                { 
                    ClearReservation(); 
                    return null; 
                }

                var reservation = await _backendApi.GetReservationByTokenAsync(token);

                if (reservation == null) ClearReservation();

                _currentToken = token;
                CurrentReservation = reservation;

                return CurrentReservation;
            }
            finally
            {
                SetLoading(false);
            }


        }

        public event Action? OnChange;
        public bool IsLoading { get; private set; }

        public void SetReservation(RentoomReservation? reservation)
        {
            CurrentReservation = reservation;
            IsLoading = false;
            NotifyStateChanged();
        }

        public void SetLoading(bool isLoading)
        {
            IsLoading = isLoading;
            NotifyStateChanged();
        }

        public void ClearReservation()
        {
            CurrentReservation = null;
            _currentToken = null;
            IsLoading = false;
        }

        public bool HasReservation()
        {
            return CurrentReservation != null;
        }

        private void NotifyStateChanged() => OnChange?.Invoke();


    }
}
