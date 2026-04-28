using System.Text.Json;
using System.Text.RegularExpressions;

namespace ResxTranslator.Services;

/// <summary>
/// Updates the supported-languages.json in the repo root after successful translation.
/// Also updates the C# defaults in SupportedLanguagesConfig.cs so WASM apps pick up changes at compile time.
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

        // Also update the C# defaults so WASM picks up the change at compile time
        UpdateCSharpDefaults(repoRoot, config.Cultures);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ Added '{culture}' to supported-languages.json");
        Console.ResetColor();
    }

    /// <summary>
    /// Removes a culture from supported-languages.json and SupportedLanguagesConfig.cs.
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

        // Update C# defaults regardless (even if JSON was already clean, keep them in sync)
        RemoveFromCSharpDefaults(repoRoot, culture);
    }

    private static void RemoveFromCSharpDefaults(string repoRoot, string culture)
    {
        var csPath = Path.Combine(repoRoot, "SharedClasses", "Configuration", "SupportedLanguagesConfig.cs");
        if (!File.Exists(csPath)) return;

        var content = File.ReadAllText(csPath);

        // Remove the culture literal from the list initializer, handling both:
        //   "de-DE", "en-US", "pl-PL"  →  remove "de-DE",
        //   "en-US", "de-DE", "pl-PL"  →  remove , "de-DE"
        var escapedCulture = Regex.Escape($"\"{culture}\"");
        var removeWithTrailingComma = new Regex($@"{escapedCulture},\s*");
        var removeWithLeadingComma  = new Regex($@",\s*{escapedCulture}");

        string updated;
        if (removeWithTrailingComma.IsMatch(content))
            updated = removeWithTrailingComma.Replace(content, "", count: 1);
        else if (removeWithLeadingComma.IsMatch(content))
            updated = removeWithLeadingComma.Replace(content, "", count: 1);
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ⚠ '{culture}' not found in SupportedLanguagesConfig.cs — file unchanged.");
            Console.ResetColor();
            return;
        }

        File.WriteAllText(csPath, updated);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ Removed '{culture}' from SupportedLanguagesConfig.cs");
        Console.ResetColor();
    }

    private static void UpdateCSharpDefaults(string repoRoot, List<string> cultures)
    {
        var csPath = Path.Combine(repoRoot, "SharedClasses", "Configuration", "SupportedLanguagesConfig.cs");
        if (!File.Exists(csPath)) return;

        var content = File.ReadAllText(csPath);
        var culturesLiteral = string.Join(", ", cultures.Select(c => $"\"{c}\""));

        // Replace the default list initializer: ["en-US", "pl-PL"] → ["en-US", "pl-PL", "de-DE"]
        var pattern = @"Cultures\s*\{[^}]*\}\s*=\s*\[([^\]]*)\]";
        var replacement = $"Cultures {{ get; set; }} = [{culturesLiteral}]";
        content = Regex.Replace(content, pattern, replacement);

        File.WriteAllText(csPath, content);
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
