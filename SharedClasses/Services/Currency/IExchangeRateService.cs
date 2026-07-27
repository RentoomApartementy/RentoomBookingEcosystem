namespace RentoomBooking.SharedClasses.Services.Currency
{
    public sealed record ExchangeRateResult(string CurrencyCode, decimal Rate, DateOnly EffectiveDate);

    public interface IExchangeRateService
    {
        /// <summary>
        /// Zwraca kurs NBP (tabela A) dla podanej waluty - najnowsze dostępne notowanie sprzed dzisiaj,
        /// cofając się wstecz o kilka dni gdy NBP nie publikował kursu (weekend/święto).
        /// Gdy żadnego notowania nie znaleziono dla żądanej waluty, próbuje fallbacku do fallbackCurrencyCode.
        /// Zwraca null gdy żaden kurs nie jest dostępny lub currencyCode to PLN.
        /// </summary>
        Task<ExchangeRateResult?> GetRateAsync(string currencyCode, string fallbackCurrencyCode = "USD", CancellationToken cancellationToken = default);
    }
}
