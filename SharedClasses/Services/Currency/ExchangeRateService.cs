using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models.Currency;

namespace RentoomBooking.SharedClasses.Services.Currency
{
    public class ExchangeRateService : IExchangeRateService
    {
        public const string HttpClientName = "Nbp";

        // NBP nie publikuje kursów w weekendy/święta - cofamy się do MaxLookbackDays dni wstecz
        // szukając ostatniego dnia z faktycznym notowaniem (typowo 1-3 dni, np. po długim weekendzie).
        private const int MaxLookbackDays = 7;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<ExchangeRateService> _logger;

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fetchLocks = new();

        private enum FetchOutcome
        {
            Found,
            NotFoundForDate,
            HardFailure
        }

        private sealed record CachedRateEntry(bool Found, ExchangeRateResult? Rate);

        public ExchangeRateService(
            IHttpClientFactory httpClientFactory,
            IMemoryCache memoryCache,
            ILogger<ExchangeRateService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<ExchangeRateResult?> GetRateAsync(string currencyCode, string fallbackCurrencyCode = "USD", CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(currencyCode))
            {
                return null;
            }

            var code = currencyCode.Trim().ToUpperInvariant();
            if (code == "PLN")
            {
                return null;
            }

            var fallbackCode = string.IsNullOrWhiteSpace(fallbackCurrencyCode)
                ? "USD"
                : fallbackCurrencyCode.Trim().ToUpperInvariant();

            var primary = await FindRateWithLookbackAsync(code, cancellationToken);
            if (primary is not null)
            {
                return primary;
            }

            if (code == fallbackCode || fallbackCode == "PLN")
            {
                return null;
            }

            return await FindRateWithLookbackAsync(fallbackCode, cancellationToken);
        }

        private async Task<ExchangeRateResult?> FindRateWithLookbackAsync(string code, CancellationToken cancellationToken)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            for (var daysBack = 1; daysBack <= MaxLookbackDays; daysBack++)
            {
                var date = today.AddDays(-daysBack);
                var (outcome, rate) = await GetCachedRateOrFetchAsync(code, date, cancellationToken);

                if (outcome == FetchOutcome.Found)
                {
                    return rate;
                }

                if (outcome == FetchOutcome.HardFailure)
                {
                    // Błąd sieci/serwera NBP - nie ma sensu dalej cofać się w czasie, spróbujemy przy następnym żądaniu.
                    return null;
                }

                // NotFoundForDate (404/400 - brak notowania na ten dzień, np. weekend/święto) - próbujemy dzień wcześniej.
            }

            return null;
        }

        private async Task<(FetchOutcome Outcome, ExchangeRateResult? Rate)> GetCachedRateOrFetchAsync(string code, DateOnly effectiveDate, CancellationToken cancellationToken)
        {
            var cacheKey = BuildCacheKey(code, effectiveDate);

            if (_memoryCache.TryGetValue(cacheKey, out CachedRateEntry? cached) && cached is not null)
            {
                return (cached.Found ? FetchOutcome.Found : FetchOutcome.NotFoundForDate, cached.Rate);
            }

            var fetchLock = _fetchLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await fetchLock.WaitAsync(cancellationToken);
            try
            {
                if (_memoryCache.TryGetValue(cacheKey, out cached) && cached is not null)
                {
                    return (cached.Found ? FetchOutcome.Found : FetchOutcome.NotFoundForDate, cached.Rate);
                }

                var (outcome, rate) = await FetchSingleDateAsync(code, effectiveDate, cancellationToken);

                if (outcome != FetchOutcome.HardFailure)
                {
                    // Cachujemy zarówno trafienia jak i potwierdzony brak notowania na dany dzień (fakt stabilny przez 1h).
                    // Błędów sieciowych/serwera nie cachujemy, żeby kolejne żądanie mogło spróbować ponownie.
                    var cacheOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1));
                    _memoryCache.Set(cacheKey, new CachedRateEntry(outcome == FetchOutcome.Found, rate), cacheOptions);
                }

                return (outcome, rate);
            }
            finally
            {
                fetchLock.Release();
            }
        }

        private async Task<(FetchOutcome Outcome, ExchangeRateResult? Rate)> FetchSingleDateAsync(string code, DateOnly effectiveDate, CancellationToken cancellationToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                var dateSegment = effectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var requestUri = $"api/exchangerates/rates/a/{code.ToLowerInvariant()}/{dateSegment}/?format=json";

                using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                request.Headers.Add("Accept", "application/json");

                using var response = await client.SendAsync(request, cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.BadRequest)
                {
                    // 404: brak notowania dla tej daty (np. weekend/święto). 400: błędne zapytanie / przekroczony limit.
                    // Oba przypadki traktujemy jako "brak danych na ten dzień" - wywołujący spróbuje wcześniejszej daty lub fallbacku.
                    return (FetchOutcome.NotFoundForDate, null);
                }

                response.EnsureSuccessStatusCode();

                var dto = await response.Content.ReadFromJsonAsync<NbpExchangeRateResponse>(cancellationToken: cancellationToken);
                var rate = dto?.Rates?.FirstOrDefault();
                if (rate is null || string.IsNullOrWhiteSpace(rate.EffectiveDate))
                {
                    return (FetchOutcome.NotFoundForDate, null);
                }

                var result = new ExchangeRateResult(code, rate.Mid, DateOnly.Parse(rate.EffectiveDate, CultureInfo.InvariantCulture));
                return (FetchOutcome.Found, result);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Nie udało się pobrać kursu NBP dla waluty {CurrencyCode} na dzień {EffectiveDate}", code, effectiveDate);
                return (FetchOutcome.HardFailure, null);
            }
        }

        private static string BuildCacheKey(string code, DateOnly effectiveDate) =>
            $"exrate:{code}:{effectiveDate:yyyy-MM-dd}";
    }
}
