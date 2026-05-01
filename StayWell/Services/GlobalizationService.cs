using System.Globalization;
using Microsoft.JSInterop;
using RentoomBooking.SharedFrontend.Localization;

namespace RentoomBooking.StayWell.Services
{
    public class GlobalizationService
    {
        private readonly IJSRuntime _js;
        public event Action? OnChange;

        public CultureInfo CurrentCulture { get; private set; } = CultureInfo.CurrentCulture;

        public GlobalizationService(IJSRuntime js)
        {
            _js = js;
        }

        public void SetCulture(string cultureName)
        {
            var culture = new CultureInfo(cultureName);

            if (CurrentCulture.Name == culture.Name)
                return;

            ApplyCulture(culture);
        }
        public void ForceSetCulture(string cultureName)
        {
            var culture = new CultureInfo(cultureName);
            ApplyCulture(culture);
        }

        private void ApplyCulture(CultureInfo culture)
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CurrentCulture = culture;

            try
            {
                _ = _js.InvokeVoidAsync("eval",
                    $"document.documentElement.lang='{culture.TwoLetterISOLanguageName}'");
            }
            catch
            { }

            OnChange?.Invoke();
        }

        public async Task SetCultureWithPreferenceAsync(string cultureName)
        {
            ForceSetCulture(cultureName);
            await SavePreferenceAsync(cultureName);
        }
        public async Task<string?> LoadPreferenceAsync()
        {
            try
            {
                return await _js.InvokeAsync<string?>("blazorCulture.get");
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
                await _js.InvokeVoidAsync("blazorCulture.set", cultureName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GlobalizationService.SavePreferenceAsync failed: {ex.Message}");
            }
        }

        public static List<CultureInfo> GetSupportedCultures()
        {
            return [.. SupportedLanguagesProvider.SupportedCultures];
        }
    }
}
