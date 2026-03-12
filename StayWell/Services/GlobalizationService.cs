using System.Globalization;

namespace RentoomBooking.StayWell.Services
{
    public class GlobalizationService
    {
        private const string LocalStorageKey = "staywell_language_preference";

        private readonly LocalStorageService _localStorage;
        public event Action? OnChange;

        public CultureInfo CurrentCulture { get; private set; } = CultureInfo.CurrentCulture;

        public GlobalizationService(LocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }

        public void SetCulture(string cultureName)
        {
            var culture = new CultureInfo(cultureName);

            if (CurrentCulture.Name == culture.Name)
                return;

            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CurrentCulture = culture;

            OnChange?.Invoke();
        }

        public async Task SetCultureWithPreferenceAsync(string cultureName)
        {
            SetCulture(cultureName);
            await SavePreferenceAsync(cultureName);
        }
        public async Task<string?> LoadPreferenceAsync()
        {
            try
            {
                return await _localStorage.GetItemAsync(LocalStorageKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GlobalizationService.LoadPreferenceAsync failed: {ex.Message}");
                return null;
            }
        }
        public async Task SavePreferenceAsync(string cultureName)
        {
            try
            {
                await _localStorage.SetItemAsync(LocalStorageKey, cultureName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GlobalizationService.SavePreferenceAsync failed: {ex.Message}");
            }
        }

        public static List<CultureInfo> GetSupportedCultures()
        {
            return
            [
                new("en-US"),
                new("pl-PL"),
            ];
        }
    }
}
