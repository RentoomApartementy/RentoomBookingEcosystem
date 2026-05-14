using System.Text.Json.Serialization;

namespace RentoomBooking.SharedFrontend.Localization;

public sealed class SupportedLanguagesConfig
{
    [JsonPropertyName("defaultCulture")]
    public string? DefaultCulture { get; init; }

    [JsonPropertyName("cultures")]
    public List<SupportedLanguageConfigItem>? Cultures { get; init; }
}

public sealed class SupportedLanguageConfigItem
{
    [JsonPropertyName("culture")]
    public string? Culture { get; init; }

    [JsonPropertyName("nativeName")]
    public string? NativeName { get; init; }
}
