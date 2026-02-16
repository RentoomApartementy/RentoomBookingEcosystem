using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.StayWell.Services;
using System.Net;

namespace RentoomBooking.StayWell.States
{
    public class TermsState(BackendApi backendApi)
    {
        private TermsEntity? _currentTerms;
        private readonly BackendApi _backendApi = backendApi;
        private string? _currentToken;
        private HttpStatusCode? _currentStatus;

        public TermsEntity? CurrentTerms
        {
            get => _currentTerms;
            private set
            {
                _currentTerms = value;
                NotifyStateChanged();
            }
        }

        public string? CurrentToken => _currentToken;
        public HttpStatusCode? CurrentStatus => _currentStatus;
        public bool IsLoading { get; private set; }

        public event Action? OnChange;

        public bool IsAccepted =>
            CurrentTerms != null;

        public void SetLoading(bool isLoading)
        {
            IsLoading = isLoading;
            NotifyStateChanged();
        }

        public void ClearTerms()
        {
            CurrentTerms = null;
            _currentToken = null;
            IsLoading = false;
        }

        private void NotifyStateChanged() => OnChange?.Invoke();

        public async Task<TermsEntity?> GetTermsAsync(string resToken)
        {
            if (_currentToken == resToken) return CurrentTerms;

            SetLoading(true);
            try
            {
                if (_backendApi == null)
                {
                    ClearTerms();
                    return null;
                }

                var terms = await _backendApi.GetTermsByResTokenAsync(resToken);
                _currentToken = resToken;
                CurrentTerms = terms;
                return CurrentTerms;
            }
            finally
            {
                SetLoading(false);
            }
        }

        public async Task<bool> AddTermsAsync(TermsEntity entity)
        {
            SetLoading(true);
            try
            {
                var result = await _backendApi.AddTermsAsync(entity);
                if (result)
                {
                    _currentToken = entity.ResToken;
                    CurrentTerms = entity;
                }
                return result;
            }
            finally
            {
                SetLoading(false);
            }
        }

        public async Task<bool> UpdateTermsAsync(TermsEntity entity)
        {
            SetLoading(true);
            try
            {
                var result = await _backendApi.UpdateTermsAsync(entity.ResToken, entity);
                if (result)
                {
                    _currentToken = entity.ResToken;
                    CurrentTerms = entity;
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