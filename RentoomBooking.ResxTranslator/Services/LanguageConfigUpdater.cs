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
