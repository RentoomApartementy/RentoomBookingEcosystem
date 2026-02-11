using RentoomBooking.SharedClasses.Models.Upsell;
using RentoomBooking.StayWell.Services;

namespace RentoomBooking.StayWell.States
{
    public class AvailableUpsellsState(BackendApi backendApi)
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(1); //<<-- zeby nie sciagal za kazdym razem wejsca na page.

        private readonly BackendApi _backendApi = backendApi;
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        private string? _currentLocale;

        public bool IsLoading { get; private set; }
        public string? Error { get; private set; }
        public string? CurrentToken { get; private set; }
        public DateTime? LastLoadedAtUtc { get; private set; }
        public IReadOnlyList<UpsellTileDto> AvailableUpsells { get; set; } = Array.Empty<UpsellTileDto>();

        public event Action? OnChange;

        public async Task EnsureLoadedAsync(string token, string? locale = null, bool force = false, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                ResetState();
                Error = "Reservation token is required.";
                NotifyStateChanged();
                return;
            }

            var normalizedLocale = NormalizeLocale(locale);

            if (!force && !ShouldFetch(token, normalizedLocale))
            {
                return;
            }

            await _loadLock.WaitAsync(ct);
            try
            {
                if (!force && !ShouldFetch(token, normalizedLocale))
                {
                    return;
                }

                var tokenChanged = !string.Equals(CurrentToken, token, StringComparison.Ordinal);
                if (tokenChanged)
                {
                    ResetState();
                    CurrentToken = token;
                    _currentLocale = normalizedLocale;
                    NotifyStateChanged();
                }

                SetLoading(true);
                Error = null;
                NotifyStateChanged();

                // BackendApi method currently does not expose CancellationToken.
                var response = await _backendApi.GetAvailableUpsellsByReservationTokenAsync(token, normalizedLocale);

                AvailableUpsells = response?.Available.ToArray() ?? Array.Empty<UpsellTileDto>();
                CurrentToken = token;
                _currentLocale = normalizedLocale;
                LastLoadedAtUtc = DateTime.UtcNow;
                Error = null;
                NotifyStateChanged();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                AvailableUpsells = Array.Empty<UpsellTileDto>();
                LastLoadedAtUtc = null;
                NotifyStateChanged();
            }
            finally
            {
                SetLoading(false);
            }
        }

        public void Invalidate()
        {
            AvailableUpsells = Array.Empty<UpsellTileDto>();
            LastLoadedAtUtc = null;
            Error = null;
            NotifyStateChanged();
        }

        private bool ShouldFetch(string token, string? locale)
        {
            if (!string.Equals(CurrentToken, token, StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.Equals(_currentLocale, locale, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!LastLoadedAtUtc.HasValue)
            {
                return true;
            }

            return DateTime.UtcNow - LastLoadedAtUtc.Value >= CacheTtl;
        }

        private static string? NormalizeLocale(string? locale)
        {
            return string.IsNullOrWhiteSpace(locale) ? null : locale.Trim();
        }

        private void SetLoading(bool isLoading)
        {
            IsLoading = isLoading;
            NotifyStateChanged();
        }

        private void ResetState()
        {
            AvailableUpsells = Array.Empty<UpsellTileDto>();
            LastLoadedAtUtc = null;
            Error = null;
            CurrentToken = null;
            _currentLocale = null;
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
