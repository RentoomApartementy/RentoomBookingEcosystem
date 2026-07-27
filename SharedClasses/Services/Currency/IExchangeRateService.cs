namespace RentoomBooking.SharedClasses.Services.Currency
{
    public sealed record ExchangeRateResult(string CurrencyCode, decimal Rate, DateOnly EffectiveDate);

    public interface IExchangeRateService
    {
        /// <summary>
        /// Zwraca kurs NBP (tabela A, notowanie z wczoraj) dla podanej waluty.
        /// Gdy NBP nie ma notowania (404/400) dla żądanej waluty, próbuje fallbacku do USD.
        /// Zwraca null gdy żaden kurs nie jest dostępny lub currencyCode to PLN.
        /// </summary>
        Task<ExchangeRateResult?> GetRateAsync(string currencyCode, CancellationToken cancellationToken = default);
    }
}
