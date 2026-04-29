using System.Globalization;
using System.Text.Json;

namespace RentoomBooking.SharedClasses.Models;

public class SupportedLanguagesConfig
{
    public string DefaultCulture { get; set; } = "en-US";
    public List<string> Cultures { get; set; } = ["en-US", "pl-PL"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static SupportedLanguagesConfig LoadFromEmbeddedResource()
    {
        var assembly = typeof(SupportedLanguagesConfig).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "RentoomBooking.SharedClasses.supported-languages.json");

        if (stream is null)
            return new SupportedLanguagesConfig();

        return JsonSerializer.Deserialize<SupportedLanguagesConfig>(stream, JsonOptions)
               ?? new SupportedLanguagesConfig();
    }

    public List<CultureInfo> GetCultureInfos() =>
        Cultures.Select(c => new CultureInfo(c)).ToList();
}
