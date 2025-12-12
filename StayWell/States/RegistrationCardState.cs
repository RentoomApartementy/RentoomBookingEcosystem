using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.StayWell.Services;
using System.Net;

namespace RentoomBooking.StayWell.States
{
    public class RegistrationCardState(BackendApi backendApi)
    {
        private RegistrationCardEntity? _currentCard;
        private readonly BackendApi _backendApi = backendApi;
        private string? _currentToken;
        private HttpStatusCode? _currentStatus;

        public RegistrationCardEntity? CurrentCard
        {
            get => _currentCard;
            private set
            {
                _currentCard = value;
                NotifyStateChanged();
            }
        }

        public string? CurrentToken => _currentToken;
        public HttpStatusCode? CurrentStatus => _currentStatus;
        public bool IsLoading { get; private set; }

        public event Action? OnChange;

        public bool IsFilled =>
            CurrentCard != null;

        public void SetLoading(bool isLoading)
        {
            IsLoading = isLoading;
            NotifyStateChanged();
        }

        public void ClearCard()
        {
            CurrentCard = null;
            _currentToken = null;
            IsLoading = false;
        }

        private void NotifyStateChanged() => OnChange?.Invoke();

        public async Task<RegistrationCardEntity?> GetCardAsync(string resToken)
        {
            if (_currentToken == resToken) return CurrentCard;

            SetLoading(true);
            try
            {
                if (_backendApi == null)
                {
                    ClearCard();
                    return null;
                }

                var card = await _backendApi.GetRegistrationCardByResTokenAsync(resToken);
                _currentToken = resToken;
                CurrentCard = card;
                return CurrentCard;
            }
            finally
            {
                SetLoading(false);
            }
        }

        public async Task<bool> SaveCardAsync(RegistrationCardEntity entity)
        {
            SetLoading(true);
            try
            {
                var result = await _backendApi.SaveRegistrationCardAsync(entity);
                if (result)
                {
                    _currentToken = entity.ResToken;
                    CurrentCard = entity;
                }
                return result;
            }
            finally
            {
                SetLoading(false);
            }
        }
        public async Task<bool> SaveCardAsync()
        {
            SetLoading(true);
            try
            {
                var result = await _backendApi.SaveRegistrationCardAsync(CurrentCard);
                if (result)
                {
                    _currentToken = CurrentCard.ResToken;
                    CurrentCard = CurrentCard;
                }
                return result;
            }
            finally
            {
                SetLoading(false);
            }
        }
    }
}
