using System.ComponentModel;
using System.Globalization;

namespace RentoomBooking.StayWell.Services
{
    public class GlobalizationService
    {
        public event Action? OnChange;

        public CultureInfo CurrentCulture { get; private set; } = CultureInfo.CurrentCulture;
        public void SetCulture(string cultureName)
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);

            if (CurrentCulture.Name == culture.Name)
                return;

            CurrentCulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            OnChange?.Invoke();
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
