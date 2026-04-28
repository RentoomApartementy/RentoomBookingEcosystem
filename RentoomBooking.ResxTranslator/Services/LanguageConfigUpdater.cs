using System.Text.Json;

namespace ResxTranslator.Services;

/// <summary>
/// Updates the supported-languages.json in the repo root after successful translation.
/// </summary>
public static class LanguageConfigUpdater
{
    public static void EnsureCultureExists(string repoRoot, string culture)
    {
        var configPath = Path.Combine(repoRoot, "supported-languages.json");

        LanguageConfig config;
        if (File.Exists(configPath))
        {
            var json = File.ReadAllText(configPath);
            config = JsonSerializer.Deserialize<LanguageConfig>(json, JsonOptions) ?? new();
        }
        else
        {
            config = new();
        }

        if (config.Cultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
            return;

        config.Cultures.Add(culture);
        config.Cultures.Sort(StringComparer.OrdinalIgnoreCase);

        var updatedJson = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(configPath, updatedJson + Environment.NewLine);

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
        var configPath = Path.Combine(repoRoot, "supported-languages.json");
        if (!File.Exists(configPath))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  ⚠ supported-languages.json not found — skipping config update.");
            Console.ResetColor();
            return;
        }

        var json = File.ReadAllText(configPath);
        LanguageConfig config;
        try
        {
            config = JsonSerializer.Deserialize<LanguageConfig>(json, JsonOptions) ?? new();
        }
        catch (JsonException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"  ERROR reading supported-languages.json: {ex.Message}");
            Console.ResetColor();
            return;
        }

        var before = config.Cultures.Count;
        config.Cultures.RemoveAll(c => c.Equals(culture, StringComparison.OrdinalIgnoreCase));

        if (config.Cultures.Count == before)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ⚠ '{culture}' was not found in supported-languages.json.");
            Console.ResetColor();
        }
        else
        {
            var updatedJson = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            File.WriteAllText(configPath, updatedJson + Environment.NewLine);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ Removed '{culture}' from supported-languages.json");
            Console.ResetColor();
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private sealed class LanguageConfig
    {
        public string DefaultCulture { get; set; } = "en-US";
        public List<string> Cultures { get; set; } = ["en-US", "pl-PL"];
    }
}
