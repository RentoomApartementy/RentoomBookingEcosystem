using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
using RentoomBooking.StayWell.Services;

namespace RentoomBooking.StayWell.States
{
    public class LocksState(BackendApi backendApi)
    {
        private List<Lock>? _locks;
        private readonly BackendApi _backendApi = backendApi;

        public bool IsLoading { get; set; }
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
            try
            {
                var result = await _backendApi.PingLockAsync(token);
                if (result is not null && result.IsSuccess)
                {
                    BatteryLevel = result.BatteryLevel;
                    IsTTLockAvailable = (BatteryLevel > 20);
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
                NotifyStateChanged();
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

        public void ClearLocks()
        {
            CurrentLocks = null;
            IsLoading = false;
            TTLockId = null;
            BatteryLevel = null;
            IsTTLockAvailable = false;
            ApartmentItemCodes = null;
        }
        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
