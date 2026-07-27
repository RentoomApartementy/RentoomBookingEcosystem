using System.Text.Json.Serialization;

namespace RentoomBooking.SharedClasses.Models.Currency
{
    public sealed class NbpExchangeRateResponse
    {
        [JsonPropertyName("table")]
        public string? Table { get; init; }

        [JsonPropertyName("currency")]
        public string? Currency { get; init; }

        [JsonPropertyName("code")]
        public string? Code { get; init; }

        [JsonPropertyName("rates")]
        public List<NbpRate>? Rates { get; init; }
    }

    public sealed class NbpRate
    {
        [JsonPropertyName("no")]
        public string? No { get; init; }

        [JsonPropertyName("effectiveDate")]
        public string? EffectiveDate { get; init; }

        [JsonPropertyName("mid")]
        public decimal Mid { get; init; }
    }
}
