using System.Globalization;
using RentoomBooking.SharedClasses.Services.Currency;
using RentoomBooking.SharedFrontend.Localization;

namespace RentoomBooking.SharedFrontend.Currency;

public class CurrentUiCurrencyProvider : ICurrentUiCurrencyProvider
{
    private readonly IExchangeRateService _exchangeRateService;
    private readonly Lazy<Task<ExchangeRateResult?>> _rateTask;

    public CurrentUiCurrencyProvider(IExchangeRateService exchangeRateService)
    {
        _exchangeRateService = exchangeRateService;
        CurrencyCode = SupportedLanguagesProvider.GetCurrencyForCulture(CultureInfo.CurrentUICulture);
        _rateTask = new Lazy<Task<ExchangeRateResult?>>(FetchRateAsync);
    }

    public string? CurrencyCode { get; }

    public Task<ExchangeRateResult?> GetRateAsync() => _rateTask.Value;

    private Task<ExchangeRateResult?> FetchRateAsync() =>
        CurrencyCode is null
            ? Task.FromResult<ExchangeRateResult?>(null)
            : _exchangeRateService.GetRateAsync(CurrencyCode, SupportedLanguagesProvider.DefaultFallbackCurrency);
}
