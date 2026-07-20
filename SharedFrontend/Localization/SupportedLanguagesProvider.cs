using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace RentoomBooking.SharedFrontend.Localization;

public static class SupportedLanguagesProvider
{
    private const string FallbackCulture = "en-US";
    private static readonly Lazy<SupportedLanguagesConfigSnapshot> Snapshot = new(LoadSnapshot);

    public static IReadOnlyList<string> SupportedCultureNames => Snapshot.Value.SupportedCultureNames;
    public static IReadOnlyList<CultureInfo> SupportedCultures => Snapshot.Value.SupportedCultures;
    public static string DefaultCultureName => Snapshot.Value.DefaultCultureName;
    public static CultureInfo DefaultCulture => Snapshot.Value.DefaultCulture;

    public static string GetLanguageLabel(CultureInfo culture)
    {
        if (Snapshot.Value.LanguageLabelsByCultureName.TryGetValue(culture.Name, out var configuredName))
        {
            return configuredName;
        }

        var nativeName = culture.NativeName;
        if (string.IsNullOrWhiteSpace(nativeName))
        {
            return culture.Name;
        }

        var noRegion = nativeName.Split('(')[0].Trim();
        return string.IsNullOrWhiteSpace(noRegion) ? nativeName : noRegion;
    }

    private static SupportedLanguagesConfigSnapshot LoadSnapshot()
    {
        var assembly = typeof(SupportedLanguagesProvider).Assembly;
        const string resourceSuffix = "Localization.supported-languages.json";
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            return BuildSnapshot(Array.Empty<SupportedLanguageConfigItem>(), null);
        }

        try
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                return BuildSnapshot(Array.Empty<SupportedLanguageConfigItem>(), null);
            }

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var config = JsonSerializer.Deserialize<SupportedLanguagesConfig>(json);
            var activeCultures = (config?.Cultures ?? []).Where(c => c.Active);
            return BuildSnapshot(activeCultures, config?.DefaultCulture);
        }
        catch
        {
            return BuildSnapshot(Array.Empty<SupportedLanguageConfigItem>(), null);
        }
    }

    private static SupportedLanguagesConfigSnapshot BuildSnapshot(IEnumerable<SupportedLanguageConfigItem> configuredCultures, string? defaultCultureName)
    {
        var cultures = new List<CultureInfo>();
        var labelsByCulture = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var configuredCulture in configuredCultures)
        {
            var cultureName = configuredCulture.Culture;
            if (string.IsNullOrWhiteSpace(cultureName))
            {
                continue;
            }

            try
            {
                var culture = CultureInfo.GetCultureInfo(cultureName.Trim());
                if (seen.Add(culture.Name))
                {
                    cultures.Add(culture);
                    if (!string.IsNullOrWhiteSpace(configuredCulture.NativeName))
                    {
                        labelsByCulture[culture.Name] = configuredCulture.NativeName.Trim();
                    }
                }
            }
            catch (CultureNotFoundException)
            {
                // Skip unsupported culture names from configuration.
            }
        }

        if (cultures.Count == 0)
        {
            cultures.Add(CultureInfo.GetCultureInfo(FallbackCulture));
        }

        var defaultCulture = cultures.FirstOrDefault(c =>
            string.Equals(c.Name, defaultCultureName, StringComparison.OrdinalIgnoreCase))
            ?? cultures[0];

        return new SupportedLanguagesConfigSnapshot(cultures, defaultCulture, labelsByCulture);
    }

    private sealed class SupportedLanguagesConfigSnapshot
    {
        public SupportedLanguagesConfigSnapshot(
            IReadOnlyList<CultureInfo> supportedCultures,
            CultureInfo defaultCulture,
            IReadOnlyDictionary<string, string> languageLabelsByCultureName)
        {
            SupportedCultures = supportedCultures;
            SupportedCultureNames = supportedCultures.Select(c => c.Name).ToArray();
            DefaultCulture = defaultCulture;
            DefaultCultureName = defaultCulture.Name;
            LanguageLabelsByCultureName = languageLabelsByCultureName;
        }

        public IReadOnlyList<CultureInfo> SupportedCultures { get; }
        public IReadOnlyList<string> SupportedCultureNames { get; }
        public CultureInfo DefaultCulture { get; }
        public string DefaultCultureName { get; }
        public IReadOnlyDictionary<string, string> LanguageLabelsByCultureName { get; }
    }
}
