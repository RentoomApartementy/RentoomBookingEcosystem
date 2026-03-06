using RentoomBooking.SharedClasses.Models;
using RentoomBooking.StayWell.Services;

namespace RentoomBooking.StayWell.States
{
    public class CustomerAgreedTermsState(BackendApi backendApi)
    {
        private readonly BackendApi _backendApi = backendApi;
        private string? _currentToken;

        public List<CustomerAgreedTermDto> Terms { get; private set; } = [];
        public bool IsLoading { get; private set; }

        public event Action? OnChange;

        private void NotifyStateChanged() => OnChange?.Invoke();

        public async Task LoadAsync(string reservationToken)
        {
            if (_currentToken == reservationToken && Terms.Count > 0) return;

            IsLoading = true;
            NotifyStateChanged();

            try
            {
                Terms = await _backendApi.GetAgreedTermsByReservationAsync(reservationToken);
                _currentToken = reservationToken;
            }
            finally
            {
                IsLoading = false;
                NotifyStateChanged();
            }
        }

        public async Task<bool> UpdateAcceptanceAsync(string reservationToken, int termsSourceId, bool isAccepted)
        {
            var success = await _backendApi.UpdateAgreedTermAsync(reservationToken, termsSourceId, isAccepted);
            if (success)
            {
                var term = Terms.FirstOrDefault(t => t.TermsSourceId == termsSourceId);
                if (term is not null)
                {
                    term.IsAccepted = isAccepted;
                    term.AgreedAt = DateTime.UtcNow;
                }
                NotifyStateChanged();
            }
            return success;
        }
    }
}