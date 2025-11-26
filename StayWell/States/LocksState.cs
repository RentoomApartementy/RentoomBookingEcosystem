using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.StayWell.Services;

namespace RentoomBooking.StayWell.States
{
    public class LocksState(BackendApi backendApi)
    {
        private List<Lock>? _locks;
        private readonly BackendApi _backendApi = backendApi;

        public bool IsLoading { get; set; }

        public List<Lock>? CurrentLocks
        {
            get => _locks;
            private set
            {
                _locks = value;
                NotifyStateChanged();
            }
        }

        public async Task<List<Lock?>?> GetLocksAsync(int reservationId, int itemId)
        {
            //if (_currentObjectId == objectId) return CurrentLocks;

            SetLoading(true);
            try
            {
                if (_backendApi == null)
                {
                    ClearLocks();
                    return null;
                }
                var locks = await _backendApi.GetLocksAsync(reservationId,itemId);
                if (locks == null) ClearLocks();
                //_currentObjectId = objectId;
                CurrentLocks = locks;
                return CurrentLocks;
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                ClearLocks();
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
        }
        private void NotifyStateChanged() => OnChange?.Invoke();


    }
}
