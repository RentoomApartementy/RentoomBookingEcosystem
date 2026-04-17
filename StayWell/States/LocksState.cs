using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
using RentoomBooking.SharedClasses.Integrations.TTLock.Models;

using RentoomBooking.StayWell.Services;

namespace RentoomBooking.StayWell.States
{
    public class LocksState(BackendApi backendApi, LocalStorageService localStorage, ReservationState reservationState)
    {
        private List<Lock>? _locks;
        private readonly BackendApi _backendApi = backendApi;
        private readonly LocalStorageService _localStorage = localStorage;
        private readonly ReservationState _reservationState = reservationState;
        private const string TtLockCacheKeyPrefix = "staywell_ttlock_status_";
        private static readonly TimeSpan TtLockCacheTtl = TimeSpan.FromHours(1);
        private static readonly TimeSpan RetryCooldown = TimeSpan.FromSeconds(30);

        // Passcode
        private const string PasscodeCooldownKeyPrefix = "staywell_passcode_cooldown_";
        private static readonly TimeSpan PasscodeCooldown = TimeSpan.FromHours(1);

        private sealed record TtLockCacheEntry(
            bool IsSuccess,
            int? BatteryLevel,
            long ExpiresAtUtcTicks
        );

        private sealed record PasscodeCooldownEntry(long UnlocksAtUtcTicks);

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

        // Passcode state
        public BackendApi.PasscodeDto? LatestPasscode { get; private set; }
        public List<BackendApi.PasscodeDto> PasscodeHistory { get; private set; } = [];
        public bool IsPasscodeLoading { get; private set; }
        public int PasscodeLoadingElapsedSeconds { get; private set; }
        public int PasscodeCooldownSecondsRemaining { get; private set; }
        public bool IsPasscodeCooldownActive { get; private set; }
        public bool IsPasscodeGenerationAllowed => !IsPasscodeCooldownActive && !IsPasscodeLoading;

        private CancellationTokenSource? _passcodeCts;
        private CancellationTokenSource? _passcodeLoadingTimerCts;

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

        public async Task LoadPasscodeHistoryAsync(string token)
        {
            try
            {
                var history = await _backendApi.GetPasscodeHistoryAsync(token);
                PasscodeHistory = history;
                LatestPasscode = history.Count > 0 ? history[0] : null;
                NotifyStateChanged();
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
                var reservation = _reservationState.CurrentReservation;
                var details = reservation?.Reservation?.ReservationDetails;
                if (details is null)
                {
                    return;
                }

                var nowUtc = DateTimeOffset.UtcNow;
                var startDate = new DateTimeOffset(
                    nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, 0, 0, TimeSpan.Zero);

                var polandTz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
                var localEndDateTime = details.getDateTo().Date + _reservationState.CheckOutTime.ToTimeSpan();
                var polandOffset = polandTz.GetUtcOffset(localEndDateTime);
                var endDate = new DateTimeOffset(localEndDateTime, polandOffset);

                var lastName = reservation?.Reservation?.Client?.LastName ?? "Guest";
                var idoReservationId = reservation?.Reservation?.id ?? 0;
                var passcodeName = $"SW-{lastName}-{idoReservationId}";

                var request = new BackendApi.GeneratePasscodeRequest(startDate, endDate, passcodeName);
                var result = await _backendApi.GeneratePasscodeAsync(token, request);
                if (result is null)
                {
                    return;
                }

                LatestPasscode = result;

                if (!PasscodeHistory.Any(p => p.KeyboardPwdId == result.KeyboardPwdId))
                {
                    PasscodeHistory.Insert(0, result);
                }

                RebuildAllCodes();

                await SavePasscodeCooldownAsync(token);
                StartPasscodeCooldown();
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

        private void RebuildAllCodes()
        {
            var all = PasscodeHistory
                .Select(p => new BackendApi.ReservationCodeDto(
                    p.KeyboardPwd,
                    p.KeyboardPwdId,
                    p.GeneratedAt,
                    p.StartDate,
                    p.EndDate,
                    BackendApi.PasscodeSource.TTLock))
                .ToList();

            var idoCode = ApartmentItemCodes?.TTLockId;
            if (!string.IsNullOrWhiteSpace(idoCode))
            {
                all.Add(new BackendApi.ReservationCodeDto(
                    idoCode,
                    null,
                    null,
                    null,
                    null,
                    BackendApi.PasscodeSource.Ido));
            }

            AllCodes = all;
        }

        public async Task RestorePasscodeCooldownIfNeededAsync(string token)
        {
            try
            {
                var key = BuildPasscodeCooldownKey(token);
                var (exists, entry) = await _localStorage.TryGetItemAsync<PasscodeCooldownEntry>(key);
                if (!exists || entry is null)
                {
                    return;
                }

                var remaining = entry.UnlocksAtUtcTicks - DateTimeOffset.UtcNow.Ticks;
                if (remaining <= 0)
                {
                    await _localStorage.RemoveItemAsync(key);
                    return;
                }

                if (IsPasscodeCooldownActive && _passcodeCts is { IsCancellationRequested: false })
                {
                    return;
                }

                PasscodeCooldownSecondsRemaining = (int)Math.Ceiling(TimeSpan.FromTicks(remaining).TotalSeconds);
                IsPasscodeCooldownActive = true;
                _passcodeCts = new CancellationTokenSource();

                _ = RunPasscodeCooldownAsync(_passcodeCts.Token);
                NotifyStateChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LocksState.RestorePasscodeCooldownIfNeededAsync failed: {ex.Message}");
            }
        }

        private void StartPasscodeCooldown()
        {
            CancelPasscodeCooldown();

            PasscodeCooldownSecondsRemaining = (int)PasscodeCooldown.TotalSeconds;
            IsPasscodeCooldownActive = true;
            _passcodeCts = new CancellationTokenSource();

            _ = RunPasscodeCooldownAsync(_passcodeCts.Token);
            NotifyStateChanged();
        }

        public void CancelPasscodeCooldown()
        {
            if (_passcodeCts is not null)
            {
                _passcodeCts.Cancel();
                _passcodeCts.Dispose();
                _passcodeCts = null;
            }

            PasscodeCooldownSecondsRemaining = 0;
            IsPasscodeCooldownActive = false;
        }

        private async Task RunPasscodeCooldownAsync(CancellationToken token)
        {
            try
            {
                while (PasscodeCooldownSecondsRemaining > 0 && !token.IsCancellationRequested)
                {
                    await Task.Delay(1000, token);
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    PasscodeCooldownSecondsRemaining--;
                    NotifyStateChanged();
                }
            }
            catch (TaskCanceledException) { }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    IsPasscodeCooldownActive = false;
                    NotifyStateChanged();
                }
            }
        }

        private async Task SavePasscodeCooldownAsync(string token)
        {
            try
            {
                var entry = new PasscodeCooldownEntry(
                    DateTimeOffset.UtcNow.Add(PasscodeCooldown).Ticks
                );
                await _localStorage.SetItemAsync(BuildPasscodeCooldownKey(token), entry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LocksState.SavePasscodeCooldownAsync failed: {ex.Message}");
            }
        }

        private static string BuildPasscodeCooldownKey(string token) =>
            $"{PasscodeCooldownKeyPrefix}{token}";

        // ── Reszta istniejących metod ─────────────────────────────────────────────

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
            LatestPasscode = null;
            PasscodeHistory = [];
            CancelPasscodeCooldown();
            CancelRetryCountdown();
            StopPasscodeLoadingTimer();
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

        public List<BackendApi.ReservationCodeDto> AllCodes { get; private set; } = [];

        public async Task LoadAllCodesAsync(string token)
        {
            try
            {
                var history = await _backendApi.GetPasscodeHistoryAsync(token);
                PasscodeHistory = history
                    .GroupBy(p => p.KeyboardPwdId)
                    .Select(g => g.First())
                    .ToList();
                LatestPasscode = PasscodeHistory.Count > 0 ? PasscodeHistory[0] : null;

                // Budujemy AllCodes lokalnie — bez dodatkowego wywołania API
                var all = PasscodeHistory
                    .Select(p => new BackendApi.ReservationCodeDto(
                        p.KeyboardPwd,
                        p.KeyboardPwdId,
                        p.GeneratedAt,
                        p.StartDate,
                        p.EndDate,
                        BackendApi.PasscodeSource.TTLock))
                    .ToList();

                var idoCode = ApartmentItemCodes?.TTLockId;
                if (!string.IsNullOrWhiteSpace(idoCode))
                {
                    all.Add(new BackendApi.ReservationCodeDto(
                        idoCode,
                        null,
                        null,
                        null,
                        null,
                        BackendApi.PasscodeSource.Ido));
                }

                AllCodes = all;
                NotifyStateChanged();
            }
            catch
            {
                // nie blokujemy ładowania strony
            }
        }
    }
}