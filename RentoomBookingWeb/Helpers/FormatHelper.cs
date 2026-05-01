using System.Globalization;

namespace RentoomBookingWeb.Helpers
{
    public static class FormatHelper
    {
        public static string FormatCurrency(decimal? amount, string currencyCode = "PLN", string culture = "pl-PL")
        {
            if (!amount.HasValue)
                return string.Empty;

            CultureInfo cultureInfo;
            try
            {
                cultureInfo = CultureInfo.GetCultureInfo(culture);
                if (cultureInfo.IsNeutralCulture)
                {
                    cultureInfo = CultureInfo.CreateSpecificCulture(cultureInfo.Name);
                }
            }
            catch (CultureNotFoundException)
            {
                cultureInfo = CultureInfo.GetCultureInfo("en-US");
            }

            var formatInfo = (NumberFormatInfo)cultureInfo.NumberFormat.Clone();

            RegionInfo? region = null;
            try
            {
                region = new RegionInfo(cultureInfo.Name);
            }
            catch (ArgumentException)
            {
                // Keep null and fallback to currencyCode below.
            }

            formatInfo.CurrencySymbol = currencyCode switch
            {
                "PLN" => "zł",
                "EUR" => "€",
                "USD" => "$",
                _ => region?.CurrencySymbol ?? currencyCode
            };

            return amount.Value.ToString("C", formatInfo);
        }
    }
}
