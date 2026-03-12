using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
using RentoomBooking.StayWell.Services;

namespace RentoomBooking.StayWell.States
{
    public class LocksState(BackendApi backendApi, LocalStorageService localStorage)
    {
        private List<Lock>? _locks;
        private readonly BackendApi _backendApi = backendApi;
        private readonly LocalStorageService _localStorage = localStorage;
        private const string TtLockCacheKeyPrefix = "staywell_ttlock_status_";
        private static readonly TimeSpan TtLockCacheTtl = TimeSpan.FromHours(1);

        private sealed record TtLockCacheEntry(bool IsSuccess, int? BatteryLevel, long ExpiresAtUtcTicks);

        public bool IsLoading { get; set; }
        public bool IsTTLockLoading { get; private set; }
        public int? BatteryLevel { get; private set; }
        public bool IsTTLockAvailable { get; private set; }

        public List<Lock>? CurrentLocks
        {
            get => _locks;
            private set
            {
                _locks = value;
                NotifyStateChanged();
            }
        }

        public string? TTLockId { get; private set; }
        public ApartmentItemLocalSettings? ApartmentItemCodes { get; private set; }

        public async Task<List<Lock?>?> GetLocksAsync(int reservationId, int itemId)
        {
            SetLoading(true);
            try
            {
                var locks = await _backendApi.GetLocksAsync(reservationId, itemId);
                CurrentLocks = locks;
                return CurrentLocks;
            }
            catch (Exception e)
            {
                //Console.WriteLine(e.Message);
                ClearLocks();
                return null;
            }
            finally
            {
                SetLoading(false);
            }
        }

        public async Task CheckTTLockStatusAsync(string token)
        {
            SetTTLockLoading(true);
            try
            {
                //najpierw cache (wazny 1h - moze za dlugo?), żeby nie pytaci api za często przy każdym wejściu na stronę z kodem
                //todo: do przemyslenia czy nie uzaleznic zapisu do cache od battery level np wiekszy niz 30%?
                //      bo Mati - masz w linicje nizej availability> 20 (wiec zakladam ze przzez 1h nie spadnie o 10%)
                //      a wtedy trzeba odpytac api
                var cached = await TryLoadTtLockCacheAsync(token);
                if (cached is not null)
                {
                    ApplyTtLockCache(cached);
                    return;
                }

                var result = await _backendApi.PingLockAsync(token);
                if (result is not null && result.IsSuccess)
                {
                    BatteryLevel = result.BatteryLevel;
                    IsTTLockAvailable = (BatteryLevel > 20);
                    //zapisuje do cache zamek, zeby przy kolejnych odpytkach nie pingować od razu api, tylko dać wynik z cache (nawet jesli jest dostpny, bo nie ma sensu pingować co chwilę)
                    await SaveTtLockCacheAsync(token, result);
                }
                else
                {
                    IsTTLockAvailable = false;
                    BatteryLevel = null;
                }
            }
            catch
            {
                IsTTLockAvailable = false;
            }
            finally
            {
                SetTTLockLoading(false);
            }
        }

        public async Task GetTTLockIdAsync(int apartmentItemId)
        {
            TTLockId = await _backendApi.GetLockCodeAsync(apartmentItemId);
            //Console.WriteLine(TTLockId);
            NotifyStateChanged();
        }

        public async Task<ApartmentItemLocalSettings?> GetApartmentItemCodesAsync(string reservationToken)
        {
            SetLoading(true);
            try
            {
                ApartmentItemCodes = await _backendApi.GetApartmentItemCodesAsync(reservationToken);
                return ApartmentItemCodes;
            }
            catch
            {
                ApartmentItemCodes = null;
                return null;
            }
            finally
            {
                SetLoading(false);
            }
        }

        public event Action? OnChange;

        public void SetLocks(List<Lock?> media)
        {
            CurrentLocks = media;
            IsLoading = false;
            NotifyStateChanged();
        }

        public void SetLoading(bool isLoading)
        {
            IsLoading = isLoading;
            NotifyStateChanged();
        }

        public void SetTTLockLoading(bool isLoading)
        {
            IsTTLockLoading = isLoading;
            NotifyStateChanged();
        }

        public void ClearLocks()
        {
            CurrentLocks = null;
            IsLoading = false;
            IsTTLockLoading = false;
            TTLockId = null;
            BatteryLevel = null;
            IsTTLockAvailable = false;
            ApartmentItemCodes = null;
        }

        private async Task<TtLockCacheEntry?> TryLoadTtLockCacheAsync(string token)
        {
            try
            {
                var key = BuildTtLockCacheKey(token);
                var (exists, entry) = await _localStorage.TryGetItemAsync<TtLockCacheEntry>(key);
                if (!exists)
                {
                    return null;
                }

                if (entry is null)
                {
                    await _localStorage.RemoveItemAsync(key);
                    return null;
                }

                var nowTicks = DateTimeOffset.UtcNow.Ticks;
                if (entry.ExpiresAtUtcTicks <= nowTicks)
                {
                    await _localStorage.RemoveItemAsync(key);
                    return null;
                }

                return entry;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LocksState.TryLoadTtLockCacheAsync failed: {ex.Message}");
                return null;
            }
        }

        private async Task SaveTtLockCacheAsync(string token, BackendApi.TTLockActionResult result)
        {
            try
            {
                var entry = new TtLockCacheEntry(
                    IsSuccess: result.IsSuccess,
                    BatteryLevel: result.BatteryLevel,
                    ExpiresAtUtcTicks: DateTimeOffset.UtcNow.Add(TtLockCacheTtl).Ticks);

                await _localStorage.SetItemAsync(BuildTtLockCacheKey(token), entry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LocksState.SaveTtLockCacheAsync failed: {ex.Message}");
            }
        }

        private void ApplyTtLockCache(TtLockCacheEntry entry)
        {
            if (entry.IsSuccess)
            {
                BatteryLevel = entry.BatteryLevel;
                IsTTLockAvailable = (BatteryLevel > 20);
            }
            else
            {
                BatteryLevel = null;
                IsTTLockAvailable = false;
            }

            NotifyStateChanged();
        }

        private static string BuildTtLockCacheKey(string token) =>
            $"{TtLockCacheKeyPrefix}{token}";

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
