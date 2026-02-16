using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.StayWell.Services;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.ArrivalInstructions;
using System.Linq;

namespace RentoomBooking.StayWell.States
{
    public class ApartmentState(BackendApi backendApi)
    {
        private ApartmentObject? _apartment;
        private readonly BackendApi _backendApi = backendApi;
        private int? _currentObjectId;
        private string? _qrMaintFormUrl;
        private IReadOnlyList<ApartmentArrivalInstructionStepDTO> _arrivalInstructionSteps = [];
        private int? _currentArrivalInstructionApartmentId;
        private RentoomWifiInfo? _wifiInfo;

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

        public RentoomWifiInfo? WifiInfo
        {
            get => _wifiInfo;
            private set
            {
                _wifiInfo = value;
                NotifyStateChanged();
            }
        }

        public IReadOnlyList<ApartmentArrivalInstructionStepDTO> ArrivalInstructionSteps
        {
            get => _arrivalInstructionSteps;
            private set
            {
                _arrivalInstructionSteps = value;
                NotifyStateChanged();
            }
        }

        public bool IsArrivalInstructionAvailable => ArrivalInstructionSteps.Count > 0;

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

        public async Task<IReadOnlyList<ApartmentArrivalInstructionStepDTO>> GetArrivalInstructionStepsAsync(int apartmentId)
        {
            if (apartmentId <= 0)
            {
                SetArrivalInstructionSteps([], null);
                return ArrivalInstructionSteps;
            }

            if (_currentArrivalInstructionApartmentId == apartmentId)
            {
                return ArrivalInstructionSteps;
            }

            if (_backendApi == null)
            {
                SetArrivalInstructionSteps([], apartmentId);
                return ArrivalInstructionSteps;
            }

            var steps = await _backendApi.GetArrivalInstructionStepsAsync(apartmentId);
            var orderedSteps = steps
                .OrderBy(s => s.Sequence)
                .ToList();

            SetArrivalInstructionSteps(orderedSteps, apartmentId);
            return ArrivalInstructionSteps;
        }

        public string? QrMaintFormUrl
        {
            get => _qrMaintFormUrl;
            set
            {
                _qrMaintFormUrl = value;
                NotifyStateChanged();
            }
        }

        public async Task<string?> GetQrMaintFormUrlAsync(int apartmentId)
        {
            if (!string.IsNullOrEmpty(QrMaintFormUrl)) return QrMaintFormUrl;
            if (_backendApi == null)
            {
                QrMaintFormUrl = null;
                return QrMaintFormUrl;
            }
            var url = await _backendApi.GetQrMaintFormUrlAsync(apartmentId);
            QrMaintFormUrl = url;
            return QrMaintFormUrl;
        }

        public async Task<RentoomWifiInfo?> GetWifiInfoAsync(int apartmentId)
        {
            if (_currentObjectId == apartmentId && WifiInfo != null)
            {
                return WifiInfo;
            }

            if (_backendApi == null)
            {
                WifiInfo = null;
                return WifiInfo;
            }

            WifiInfo = await _backendApi.GetApartmentWifiInfoAsync(apartmentId);
            return WifiInfo;
        }

        public event Action? OnChange;

        public void SetApartment(ApartmentObject? apartment)
        {
            CurrentApartment = apartment;
            IsLoading = false;
            NotifyStateChanged();
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
            QrMaintFormUrl = null;
            WifiInfo = null;
            SetArrivalInstructionSteps([], null);
        }

        private void SetArrivalInstructionSteps(IReadOnlyList<ApartmentArrivalInstructionStepDTO> steps, int? apartmentId)
        {
            _currentArrivalInstructionApartmentId = apartmentId;
            ArrivalInstructionSteps = steps;
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
