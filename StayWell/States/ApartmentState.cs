using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.StayWell.Services;

namespace RentoomBooking.StayWell.States
{
    public class ApartmentState(BackendApi backendApi)
    {
        private ApartmentObject? _apartment;
        private readonly BackendApi _backendApi = backendApi;
        private int? _currentObjectId;

        public bool IsLoading { get; set; }

        public ApartmentObject? CurrentApartment
        {
            get => _apartment;
            private set
            {
                _apartment = value;
                NotifyStateChanged();
            }
        }

        public async Task<ApartmentObject?> GetApartmentByIdAsync(int objectId)
        {
            if (_currentObjectId == objectId) return CurrentApartment;

            SetLoading(true);
            try
            {
                if (_backendApi == null)
                {
                    ClearApartment();
                    return null;
                }
                var apartment = await _backendApi.GetApartmentByIdAsync(objectId);
                if (apartment == null) ClearApartment();
                _currentObjectId = objectId;
                CurrentApartment = apartment;
                return CurrentApartment;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                ClearApartment();
                return null;
            }
            finally
            {
                SetLoading(false);
            }
        }

        public event Action? OnChange;

        public void SetApartment(ApartmentObject? apartment)
        {
            CurrentApartment = apartment;
            IsLoading = false;
        }

        public void SetLoading(bool isLoading)
        {
            IsLoading = isLoading;
            NotifyStateChanged();
        }


        public void ClearApartment()
        {
            CurrentApartment = null;
            _currentObjectId = null;
            IsLoading = false;
        }
        private void NotifyStateChanged() => OnChange?.Invoke();

    }
}
