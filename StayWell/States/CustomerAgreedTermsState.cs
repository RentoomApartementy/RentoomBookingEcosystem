using RentoomBooking.SharedClasses.Models;
using RentoomBooking.StayWell.Services;
using System.Globalization;
using System.Threading;

namespace RentoomBooking.StayWell.States
{
    public class CustomerAgreedTermsState(BackendApi backendApi, GlobalizationService globalizationService) : IDisposable
    {
        private readonly BackendApi _backendApi = backendApi;
        private readonly GlobalizationService _globalizationService = globalizationService;
        private readonly SemaphoreSlim _reloadLock = new(1, 1);

        private string? _currentToken;
        private string? _currentLanguage;

        public List<CustomerAgreedTermDto> Terms { get; private set; } = [];
        public bool IsLoading { get; private set; }

        public event Action? OnChange;

        private void NotifyStateChanged() => OnChange?.Invoke();

        public async Task LoadAsync(string reservationToken, string? language = null)
        {
            var cultureFromGlobalization = _globalizationService.CurrentCulture.Name;
            var normalizedLanguage = NormalizeLanguage(language ?? cultureFromGlobalization);

            if (_currentToken == reservationToken
                && string.Equals(_currentLanguage, normalizedLanguage, StringComparison.OrdinalIgnoreCase)
                && Terms.Count > 0)
            {
                return;
            }

            IsLoading = true;
            NotifyStateChanged();

            try
            {
                Terms = await _backendApi.GetAgreedTermsByReservationAsync(reservationToken, normalizedLanguage);
                _currentToken = reservationToken;
                _currentLanguage = normalizedLanguage;
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

        public void InitializeLanguageSync()
        {
            _globalizationService.OnChange -= OnGlobalizationChanged;
            _globalizationService.OnChange += OnGlobalizationChanged;
        }

        private void OnGlobalizationChanged()
        {
            NotifyStateChanged(); 

            _ = ReloadForCurrentTokenAsync(); 
        }

        private async Task ReloadForCurrentTokenAsync()
        {
            if (string.IsNullOrWhiteSpace(_currentToken))
            {
                return;
            }

            await _reloadLock.WaitAsync();
            try
            {
                await LoadAsync(_currentToken, _globalizationService.CurrentCulture.Name);
            }
            finally
            {
                _reloadLock.Release();
            }
        }

        private static string NormalizeLanguage(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return "en-GB";
            }

            try
            {
                var culture = CultureInfo.GetCultureInfo(language);
                return culture.TwoLetterISOLanguageName.ToLowerInvariant() switch
                {
                    "en" => "en-GB",
                    "pl" => "pl-PL",
                    _ => culture.Name
                };
            }
            catch (CultureNotFoundException)
            {
                return "en-GB";
            }
        }

        public void Dispose()
        {
            _globalizationService.OnChange -= OnGlobalizationChanged;
            _reloadLock.Dispose();
        }
    }
}
