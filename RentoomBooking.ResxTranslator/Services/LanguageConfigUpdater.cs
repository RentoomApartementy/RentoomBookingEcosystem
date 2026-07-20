using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResxTranslator.Services;

/// <summary>
/// Updates SharedFrontend/Localization/supported-languages.json after successful translation.
/// The file uses the object schema { culture, nativeName } consumed by
/// RentoomBooking.SharedFrontend.Localization.SupportedLanguagesConfig.
/// Native names are derived from CultureInfo.NativeName — verify after first run,
/// some locales need a manual touch-up (capitalization, region suffix).
/// </summary>
public static class LanguageConfigUpdater
{
    private static string ConfigPath(string repoRoot) =>
        Path.Combine(repoRoot, "SharedFrontend", "Localization", "supported-languages.json");

    public static void EnsureCultureExists(string repoRoot, string culture)
    {
        var configPath = ConfigPath(repoRoot);
        var config = Load(configPath) ?? new LanguageConfig();

        if (config.Cultures.Any(c => string.Equals(c.Culture, culture, StringComparison.OrdinalIgnoreCase)))
            return;

        config.Cultures.Add(new LanguageItem
        {
            Culture = culture,
            NativeName = BuildNativeName(culture),
            Active = true
        });
        config.Cultures.Sort((a, b) =>
            string.Compare(a.Culture, b.Culture, StringComparison.OrdinalIgnoreCase));

        Save(configPath, config);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ Added '{culture}' to supported-languages.json");
        Console.ResetColor();
    }

    /// <summary>
    /// Removes a culture from supported-languages.json.
    /// No-op if the culture is not present. Caller is responsible for ensuring the culture
    /// is not the default and that no .resx files for it remain.
    /// </summary>
    public static void RemoveCulture(string repoRoot, string culture)
    {
        var configPath = ConfigPath(repoRoot);
        var config = Load(configPath);
        if (config is null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  ⚠ supported-languages.json not found — skipping config update.");
            Console.ResetColor();
            return;
        }

        var before = config.Cultures.Count;
        config.Cultures.RemoveAll(c =>
            string.Equals(c.Culture, culture, StringComparison.OrdinalIgnoreCase));

        if (config.Cultures.Count == before)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ⚠ '{culture}' was not found in supported-languages.json.");
            Console.ResetColor();
            return;
        }

        Save(configPath, config);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ Removed '{culture}' from supported-languages.json");
        Console.ResetColor();
    }

    private static LanguageConfig? Load(string configPath)
    {
        if (!File.Exists(configPath)) return null;

        try
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<LanguageConfig>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"  ERROR reading supported-languages.json: {ex.Message}");
            Console.ResetColor();
            return null;
        }
    }

    private static void Save(string configPath, LanguageConfig config)
    {
        var dir = Path.GetDirectoryName(configPath);
        if (dir != null) Directory.CreateDirectory(dir);

        var updatedJson = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(configPath, updatedJson + Environment.NewLine);
    }

    // CultureInfo.NativeName returns e.g. "polski (Polska)" or "беларуская".
    // Strip region suffix and uppercase first letter so the result matches the
    // existing convention ("Polski", "Беларуская").
    private static string BuildNativeName(string culture)
    {
        try
        {
            var raw = CultureInfo.GetCultureInfo(culture).NativeName;
            var parenIdx = raw.IndexOf(" (", StringComparison.Ordinal);
            var name = parenIdx > 0 ? raw[..parenIdx] : raw;
            return name.Length == 0
                ? culture
                : char.ToUpper(name[0], CultureInfo.InvariantCulture) + name[1..];
        }
        catch (CultureNotFoundException)
        {
            return culture;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private sealed class LanguageConfig
    {
        [JsonPropertyName("defaultCulture")]
        public string DefaultCulture { get; set; } = "en-US";

        [JsonPropertyName("cultures")]
        public List<LanguageItem> Cultures { get; set; } = [];
    }

    private sealed class LanguageItem
    {
        [JsonPropertyName("culture")]
        public string Culture { get; set; } = "";

        [JsonPropertyName("nativeName")]
        public string NativeName { get; set; } = "";

        [JsonPropertyName("active")]
        public bool Active { get; set; } = false;
    }

    /// <summary>
    /// Returns the set of culture codes marked "active": true in supported-languages.json.
    /// Used by the translator to skip cultures that exist in config but are turned off.
    /// </summary>
    public static HashSet<string> GetActiveCultures(string repoRoot)
    {
        var config = Load(ConfigPath(repoRoot));
        return new HashSet<string>(
            (config?.Cultures ?? []).Where(c => c.Active).Select(c => c.Culture),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns every culture code currently present in supported-languages.json,
    /// regardless of its "active" flag.
    /// </summary>
    public static HashSet<string> GetConfiguredCultures(string repoRoot)
    {
        var config = Load(ConfigPath(repoRoot));
        return new HashSet<string>(
            (config?.Cultures ?? []).Select(c => c.Culture),
            StringComparer.OrdinalIgnoreCase);
    }
}
