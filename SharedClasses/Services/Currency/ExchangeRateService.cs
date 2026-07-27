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

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<ExchangeRateService> _logger;

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fetchLocks = new();

        public ExchangeRateService(
            IHttpClientFactory httpClientFactory,
            IMemoryCache memoryCache,
            ILogger<ExchangeRateService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<ExchangeRateResult?> GetRateAsync(string currencyCode, CancellationToken cancellationToken = default)
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

            var effectiveDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

            var primary = await GetCachedRateOrFetchAsync(code, effectiveDate, cancellationToken);
            if (primary is not null)
            {
                return primary;
            }

            if (code == "USD")
            {
                return null;
            }

            return await GetCachedRateOrFetchAsync("USD", effectiveDate, cancellationToken);
        }

        private async Task<ExchangeRateResult?> GetCachedRateOrFetchAsync(string code, DateOnly effectiveDate, CancellationToken cancellationToken)
        {
            var cacheKey = BuildCacheKey(code, effectiveDate);

            if (_memoryCache.TryGetValue(cacheKey, out ExchangeRateResult? cached))
            {
                return cached;
            }

            var fetchLock = _fetchLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await fetchLock.WaitAsync(cancellationToken);
            try
            {
                if (_memoryCache.TryGetValue(cacheKey, out cached))
                {
                    return cached;
                }

                var result = await FetchRateAsync(code, effectiveDate, cancellationToken);

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromHours(1));
                _memoryCache.Set(cacheKey, result, cacheOptions);

                return result;
            }
            finally
            {
                fetchLock.Release();
            }
        }

        private async Task<ExchangeRateResult?> FetchRateAsync(string code, DateOnly effectiveDate, CancellationToken cancellationToken)
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
                    // Oba przypadki traktujemy jako "brak danych" i pozwalamy wywołującemu spróbować fallbacku.
                    return null;
                }

                response.EnsureSuccessStatusCode();

                var dto = await response.Content.ReadFromJsonAsync<NbpExchangeRateResponse>(cancellationToken: cancellationToken);
                var rate = dto?.Rates?.FirstOrDefault();
                if (rate is null || string.IsNullOrWhiteSpace(rate.EffectiveDate))
                {
                    return null;
                }

                return new ExchangeRateResult(code, rate.Mid, DateOnly.Parse(rate.EffectiveDate, CultureInfo.InvariantCulture));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Nie udało się pobrać kursu NBP dla waluty {CurrencyCode}", code);
                return null;
            }
        }

        private static string BuildCacheKey(string code, DateOnly effectiveDate) =>
            $"exrate:{code}:{effectiveDate:yyyy-MM-dd}";
    }
}
