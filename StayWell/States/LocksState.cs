using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
using RentoomBooking.SharedClasses.Integrations.TTLock.Models;

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
        private static readonly TimeSpan RetryCooldown = TimeSpan.FromSeconds(30);

        private sealed record TtLockCacheEntry(
            bool IsSuccess,
            int? BatteryLevel,
            long ExpiresAtUtcTicks
        );

        public bool IsLoading { get; set; }
        public bool IsTTLockLoading { get; private set; }
        public int? BatteryLevel { get; private set; }
        public bool IsTTLockAvailable { get; private set; }

        private CancellationTokenSource? _retryCts;
        private DateTimeOffset? _retryUnlocksAt;

        public bool IsStatusResolved { get; private set; }

        public int RetrySecondsRemaining { get; private set; }
        public bool IsRetryCountdownActive { get; private set; }

        public bool IsRetryDisabled =>
            RetrySecondsRemaining > 0 || IsTTLockLoading;

        public BackendApi.AccessCodesResponse? AccessCodes { get; private set; }
        public bool IsPasscodeLoading { get; private set; }
        public int PasscodeLoadingElapsedSeconds { get; private set; }

        public BackendApi.AccessCodeDto? CurrentCode => AccessCodes?.CurrentCode;
        public List<BackendApi.AccessCodeDto> AllCodes => AccessCodes?.History ?? [];
        public bool CanGenerate => AccessCodes?.CanGenerate ?? false;
        public string? GenerationBlockReason => AccessCodes?.GenerationBlockReason;
        public int? CooldownSecondsRemaining => AccessCodes?.CooldownSecondsRemaining;
        public DateTimeOffset? NextGenerationAvailableAt => AccessCodes?.NextGenerationAvailableAt;
        public bool IsPasscodeGenerationAllowed => CanGenerate && !IsPasscodeLoading;

        private CancellationTokenSource? _passcodeLoadingTimerCts;

        // Live cooldown countdown (ticks toward the next full hour when generation is available)
        private CancellationTokenSource? _cooldownCts;
        public int LiveCooldownSecondsRemaining { get; private set; }

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

        public async Task<List<Lock?>?> GetLocksAsync(
            int reservationId,
            int itemId
        )
        {
            SetLoading(true);
            try
            {
                var locks = await _backendApi.GetLocksAsync(
                    reservationId,
                    itemId
                );
                CurrentLocks = locks;
                return CurrentLocks;
            }
            catch
            {
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
                IsStatusResolved = true;
                SetTTLockLoading(false);

                if (!IsTTLockAvailable)
                {
                    StartRetryCountdown();
                }
            }
        }

        public async Task RetryCheckTTLockStatusAsync(string token)
        {
            if (IsRetryDisabled || string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            CancelRetryCountdown();
            await CheckTTLockStatusAsync(token);
        }

        public void StartRetryCountdown()
        {
            CancelRetryCountdown();

            _retryUnlocksAt = DateTimeOffset.UtcNow.Add(RetryCooldown);
            RetrySecondsRemaining = (int)RetryCooldown.TotalSeconds;
            IsRetryCountdownActive = true;
            _retryCts = new CancellationTokenSource();

            _ = RunRetryCountdownAsync(_retryCts.Token);
            NotifyStateChanged();
        }

        public void RestoreRetryCountdownIfNeeded()
        {
            if (_retryUnlocksAt is null)
            {
                return;
            }

            var remaining = _retryUnlocksAt.Value - DateTimeOffset.UtcNow;
            if (remaining.TotalSeconds <= 0)
            {
                CancelRetryCountdown();
                return;
            }

            if (IsRetryCountdownActive && _retryCts is { IsCancellationRequested: false })
            {
                return;
            }

            RetrySecondsRemaining = (int)Math.Ceiling(remaining.TotalSeconds);
            IsRetryCountdownActive = true;
            _retryCts = new CancellationTokenSource();

            _ = RunRetryCountdownAsync(_retryCts.Token);
            NotifyStateChanged();
        }

        public void CancelRetryCountdown()
        {
            if (_retryCts is not null)
            {
                _retryCts.Cancel();
                _retryCts.Dispose();
                _retryCts = null;
            }

            RetrySecondsRemaining = 0;
            IsRetryCountdownActive = false;
            _retryUnlocksAt = null;
        }

        private async Task RunRetryCountdownAsync(CancellationToken token)
        {
            try
            {
                while (
                    RetrySecondsRemaining > 0
                    && !token.IsCancellationRequested
                )
                {
                    await Task.Delay(1000, token);
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    RetrySecondsRemaining--;
                    NotifyStateChanged();
                }
            }
            catch (TaskCanceledException) { }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    IsRetryCountdownActive = false;
                    _retryUnlocksAt = null;
                    NotifyStateChanged();
                }
            }
        }

        public async Task LoadAccessCodesAsync(string token)
        {
            try
            {
                var model = await _backendApi.GetAccessCodesAsync(token);
                if (model is not null)
                {
                    AccessCodes = model;
                    StartCooldownCountdownIfNeeded();
                    NotifyStateChanged();
                }
            }
            catch
            {
            }
        }

        public async Task GeneratePasscodeAsync(string token)
        {
            if (!IsPasscodeGenerationAllowed || string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            IsPasscodeLoading = true;
            PasscodeLoadingElapsedSeconds = 0;
            NotifyStateChanged();

            StartPasscodeLoadingTimer();

            try
            {
                var result = await _backendApi.GenerateAccessCodeAsync(token);
                if (result is not null)
                {
                    AccessCodes = result;
                    StartCooldownCountdownIfNeeded();
                }
            }
            finally
            {
                StopPasscodeLoadingTimer();
                IsPasscodeLoading = false;
                NotifyStateChanged();
            }
        }

        private void StartPasscodeLoadingTimer()
        {
            StopPasscodeLoadingTimer();
            _passcodeLoadingTimerCts = new CancellationTokenSource();
            _ = RunPasscodeLoadingTimerAsync(_passcodeLoadingTimerCts.Token);
        }

        private void StopPasscodeLoadingTimer()
        {
            if (_passcodeLoadingTimerCts is not null)
            {
                _passcodeLoadingTimerCts.Cancel();
                _passcodeLoadingTimerCts.Dispose();
                _passcodeLoadingTimerCts = null;
            }
        }

        private async Task RunPasscodeLoadingTimerAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(1000, token);
                    if (token.IsCancellationRequested) return;

                    PasscodeLoadingElapsedSeconds++;
                    NotifyStateChanged();
                }
            }
            catch (TaskCanceledException) { }
        }

        // ── Reszta istniejących metod ─────────────────────────────────────────────

        private void StartCooldownCountdownIfNeeded()
        {
            StopCooldownCountdown();

            var target = NextGenerationAvailableAt;
            if (target is null) return;

            var remaining = target.Value - DateTimeOffset.UtcNow;
            if (remaining.TotalSeconds <= 0) return;

            LiveCooldownSecondsRemaining = (int)Math.Ceiling(remaining.TotalSeconds);
            _cooldownCts = new CancellationTokenSource();
            _ = RunCooldownAsync(_cooldownCts.Token, target.Value);
        }

        private void StopCooldownCountdown()
        {
            if (_cooldownCts is not null)
            {
                _cooldownCts.Cancel();
                _cooldownCts.Dispose();
                _cooldownCts = null;
            }
            LiveCooldownSecondsRemaining = 0;
        }

        private async Task RunCooldownAsync(CancellationToken token, DateTimeOffset target)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(1000, token);
                    if (token.IsCancellationRequested) return;

                    var remaining = target - DateTimeOffset.UtcNow;
                    LiveCooldownSecondsRemaining = remaining.TotalSeconds > 0
                        ? (int)Math.Ceiling(remaining.TotalSeconds)
                        : 0;
                    NotifyStateChanged();

                    if (LiveCooldownSecondsRemaining <= 0) break;
                }
            }
            catch (TaskCanceledException) { }
        }

        public async Task GetTTLockIdAsync(int apartmentItemId)
        {
            TTLockId = await _backendApi.GetLockCodeAsync(apartmentItemId);
            NotifyStateChanged();
        }

        public async Task<ApartmentItemLocalSettings?> GetApartmentItemCodesAsync(
            string reservationToken
        )
        {
            SetLoading(true);
            try
            {
                ApartmentItemCodes =
                    await _backendApi.GetApartmentItemCodesAsync(
                        reservationToken
                    );
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
            IsStatusResolved = false;
            ApartmentItemCodes = null;
            AccessCodes = null;
            CancelRetryCountdown();
            StopPasscodeLoadingTimer();
            StopCooldownCountdown();
        }

        private async Task<TtLockCacheEntry?> TryLoadTtLockCacheAsync(
            string token
        )
        {
            try
            {
                var key = BuildTtLockCacheKey(token);
                var (exists, entry)
                    = await _localStorage.TryGetItemAsync<TtLockCacheEntry>(
                        key
                    );
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
                Console.WriteLine(
                    $"LocksState.TryLoadTtLockCacheAsync failed: {ex.Message}"
                );
                return null;
            }
        }

        private async Task SaveTtLockCacheAsync(
            string token,
            BackendApi.TTLockActionResult result
        )
        {
            try
            {
                var entry = new TtLockCacheEntry(
                    IsSuccess: result.IsSuccess,
                    BatteryLevel: result.BatteryLevel,
                    ExpiresAtUtcTicks: DateTimeOffset.UtcNow
                        .Add(TtLockCacheTtl)
                        .Ticks
                );

                await _localStorage.SetItemAsync(
                    BuildTtLockCacheKey(token),
                    entry
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"LocksState.SaveTtLockCacheAsync failed: {ex.Message}"
                );
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

            IsStatusResolved = true;
            NotifyStateChanged();
        }

        private static string BuildTtLockCacheKey(string token) =>
            $"{TtLockCacheKeyPrefix}{token}";

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}