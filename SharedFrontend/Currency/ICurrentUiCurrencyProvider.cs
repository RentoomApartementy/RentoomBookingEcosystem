using RentoomBooking.SharedClasses.Services.Currency;

namespace RentoomBooking.SharedFrontend.Currency;

public interface ICurrentUiCurrencyProvider
{
    /// <summary>Kod waluty (np. "EUR") powiązany z bieżącym CultureInfo.CurrentUICulture, lub null (np. dla pl-PL).</summary>
    string? CurrencyCode { get; }

    /// <summary>
    /// Zwraca kurs dla CurrencyCode. Memoizowane per instancja (per scope Blazor) —
    /// niezależnie ile razy jest wywoływane w obrębie jednego requestu/obwodu, IExchangeRateService jest odpytywany raz.
    /// </summary>
    Task<ExchangeRateResult?> GetRateAsync();
}
