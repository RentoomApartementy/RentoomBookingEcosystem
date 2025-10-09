using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.StayWell.Services;

namespace RentoomBooking.StayWell.States
{
    public class AmenitiesState(BackendApi backendApi)
    {
        private List<ObjectAmenity>? _amenities;
        private readonly BackendApi _backendApi = backendApi;
        private int? _currentObjectId;

        public bool IsLoading { get; set; }

        public List<ObjectAmenity>? CurrentAmenities
        {
            get => _amenities;
            private set
            {
                _amenities = value;
                NotifyStateChanged();
            }
        }

        public async Task<List<ObjectAmenity>?> GetAmenitiesForObjectsAsync(int objectId)
        {
            if (_currentObjectId == objectId) return CurrentAmenities;

            SetLoading(true);
            try
            {
                if (_backendApi == null)
                {
                    ClearAmenities();
                    return null;
                }
                var amenities = await _backendApi.GetAmenitiesForObjectsAsync(objectId);
                if (amenities == null) ClearAmenities();
                _currentObjectId = objectId;
                CurrentAmenities = amenities;
                return CurrentAmenities;
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                ClearAmenities();
                return null;
            }
            finally
            {
                SetLoading(false);
            }
        }


        public event Action? OnChange;

        public void SetAmenities(List<ObjectAmenity>? media)
        {
            CurrentAmenities = media;
            IsLoading = false;
        }

        public void SetLoading(bool isLoading)
        {
            IsLoading = isLoading;
            NotifyStateChanged();
        }


        public void ClearAmenities()
        {
            CurrentAmenities = null;
            _currentObjectId = null;
            IsLoading = false;
        }
        private void NotifyStateChanged() => OnChange?.Invoke();


    }
}
