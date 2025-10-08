using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.StayWell.Services;

namespace RentoomBooking.StayWell.States
{
    public class MediaState(BackendApi backendApi)
    {
        private List<ObjectMedium>? _media;
        private readonly BackendApi _backendApi = backendApi;
        private int? _currentObjectId;

        public bool IsLoading { get; set; }

        public List<ObjectMedium>? CurrentMedia
        {
            get => _media;
            private set
            {
                _media = value;
                NotifyStateChanged();
            }
        }

        public async Task<List<ObjectMedium>?> GetMediaAsync(int objectId)
        {
            if (_currentObjectId == objectId) return CurrentMedia;

            SetLoading(true);
            try
            {
                if (_backendApi == null)
                {
                    ClearMedia();
                    return null;
                }
                var media = await _backendApi.GetApartmentMediaAsync(objectId);
                if (media == null) ClearMedia();
                _currentObjectId = objectId;
                CurrentMedia = media;
                return CurrentMedia;
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                ClearMedia();
                return null;
            }
            finally
            {
                SetLoading(false);
            }
        }


        public event Action? OnChange;

        public void SetMedia(List<ObjectMedium>? media)
        {
            CurrentMedia = media;
            IsLoading = false;
        }

        public void SetLoading(bool isLoading)
        {
            IsLoading = isLoading;
            NotifyStateChanged();
        }


        public void ClearMedia()
        {
            CurrentMedia = null;
            _currentObjectId = null;
            IsLoading = false;
        }
        private void NotifyStateChanged() => OnChange?.Invoke();


    }
}
