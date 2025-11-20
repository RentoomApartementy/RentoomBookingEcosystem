using System.Globalization;

namespace RentoomBookingWeb.Helpers
{
    public static class FormatHelper
    {
        public static string FormatCurrency(decimal? amount, string currencyCode = "PLN", string culture = "pl-PL")
        {
            if (!amount.HasValue)
                return string.Empty;

            var cultureInfo = new CultureInfo(culture);
            var formatInfo = (NumberFormatInfo)cultureInfo.NumberFormat.Clone();

            var region = new RegionInfo(cultureInfo.Name);
            formatInfo.CurrencySymbol = currencyCode switch
            {
                "PLN" => "zł",
                "EUR" => "€",
                "USD" => "$",
                _ => region.CurrencySymbol
            };

            return amount.Value.ToString("C", formatInfo);
        }
    }
}