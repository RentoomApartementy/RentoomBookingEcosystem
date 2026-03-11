using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.StayWell.Services;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.ArrivalInstructions;
using System.Globalization;
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
        private string? _currentArrivalInstructionLanguage;
        private RentoomWifiInfo? _wifiInfo;
        private IReadOnlyList<DefinedAddonEntity> _definedAddons = [];
        
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

        public async Task<IReadOnlyList<ApartmentArrivalInstructionStepDTO>> GetArrivalInstructionStepsAsync(int apartmentId, string? language = null)
        {
            if (apartmentId <= 0)
            {
                SetArrivalInstructionSteps([], null, null);
                return ArrivalInstructionSteps;
            }

            var normalizedLanguage = NormalizeArrivalInstructionLanguage(language);

            if (_currentArrivalInstructionApartmentId == apartmentId
                && string.Equals(_currentArrivalInstructionLanguage, normalizedLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return ArrivalInstructionSteps;
            }

            if (_backendApi == null)
            {
                SetArrivalInstructionSteps([], apartmentId, normalizedLanguage);
                return ArrivalInstructionSteps;
            }

            var steps = await _backendApi.GetArrivalInstructionStepsAsync(apartmentId, normalizedLanguage);
            var orderedSteps = steps
                .OrderBy(s => s.Sequence)
                .ToList();

            SetArrivalInstructionSteps(orderedSteps, apartmentId, normalizedLanguage);
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
            DefinedAddons = [];
            SetArrivalInstructionSteps([], null, null);
        }

        private void SetArrivalInstructionSteps(IReadOnlyList<ApartmentArrivalInstructionStepDTO> steps, int? apartmentId, string? language)
        {
            _currentArrivalInstructionApartmentId = apartmentId;
            _currentArrivalInstructionLanguage = language;
            ArrivalInstructionSteps = steps;
        }

        private void NotifyStateChanged() => OnChange?.Invoke();

        private static string NormalizeArrivalInstructionLanguage(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            }

            var trimmed = language.Trim();
            if (string.Equals(trimmed, "default", StringComparison.OrdinalIgnoreCase))
            {
                return "default";
            }

            var lowered = trimmed.ToLowerInvariant()
                .Replace('_', '-');

            var dashIndex = lowered.IndexOf('-', StringComparison.Ordinal);
            if (dashIndex > 0)
            {
                lowered = lowered[..dashIndex];
            }

            return lowered switch
            {
                "pl" => "pl",
                "pol" => "pl",
                "en" => "en",
                "eng" => "en",
                "de" => "de",
                "deu" => "de",
                "iv" => "default",
                _ => "default"
            };
        }

        public async Task<IReadOnlyList<DefinedAddonEntity>> GetDefinedAddonsAsync()
        {
            if (_definedAddons.Count > 0)
            {
                return DefinedAddons;
            }

            if (_backendApi == null)
            {
                DefinedAddons = [];
                return DefinedAddons;
            }

            var addons = await _backendApi.GetDefinedAddonsAsync();
            DefinedAddons = addons ?? [];
            return DefinedAddons;
        }

        public IReadOnlyList<DefinedAddonEntity> DefinedAddons
        {
            get => _definedAddons;
            private set
            {
                _definedAddons = value;
                NotifyStateChanged();
            }
        }
    }
}
