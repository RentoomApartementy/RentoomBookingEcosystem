using System.Globalization;
using System.Text.Json;

namespace RentoomBooking.SharedClasses.Configuration;

public sealed class SupportedLanguagesConfig
{
    public string DefaultCulture { get; set; } = "en-US";
    public List<string> Cultures { get; set; } = ["en-US", "fr-FR", "pl-PL"];

    public string[] GetCultureNames() => Cultures.ToArray();

    public CultureInfo[] GetCultureInfos() =>
        Cultures.Select(c => new CultureInfo(c)).ToArray();

    /// <summary>
    /// Load from a JSON file (server-side apps).
    /// </summary>
    public static SupportedLanguagesConfig LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SupportedLanguagesConfig>(json, JsonOptions) ?? new();
    }

    /// <summary>
    /// Load from a JSON string (WASM apps that fetch the file via HTTP).
    /// </summary>
    public static SupportedLanguagesConfig LoadFromJson(string json) =>
        JsonSerializer.Deserialize<SupportedLanguagesConfig>(json, JsonOptions) ?? new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}

