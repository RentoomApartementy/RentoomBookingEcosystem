using ResxTranslator.Models;

namespace ResxTranslator.Services;

/// <summary>
/// Main orchestrator: scan → delta detect → translate → merge → update config.
/// </summary>
public sealed class TranslationOrchestrator
{
    private readonly string _repoRoot;
    private readonly string _sourceCulture;
    private readonly string[] _targetCultures;
    private readonly string _translatorKey;
    private readonly string _translatorRegion;
    private readonly bool _forceAll;
    private readonly bool _dryRun;
    private readonly string[] _includeProjects;
    private readonly string[] _excludeProjects;

    public TranslationOrchestrator(
        string repoRoot,
        string sourceCulture,
        string[] targetCultures,
        string translatorKey,
        string translatorRegion,
        bool forceAll,
        bool dryRun,
        string[] includeProjects,
        string[] excludeProjects)
    {
        _repoRoot = repoRoot;
        _sourceCulture = sourceCulture;
        _targetCultures = targetCultures;
        _translatorKey = translatorKey;
        _translatorRegion = translatorRegion;
        _forceAll = forceAll;
        _dryRun = dryRun;
        _includeProjects = includeProjects;
        _excludeProjects = excludeProjects;
    }

    public async Task RunAsync()
    {
        Console.WriteLine($"ResxTranslator — repo: {_repoRoot}");
        Console.WriteLine($"  Source: {_sourceCulture}");
        Console.WriteLine($"  Force all: {_forceAll}, Dry run: {_dryRun}");
        if (_includeProjects.Length > 0)
            Console.WriteLine($"  Include only: {string.Join(", ", _includeProjects)}");
        if (_excludeProjects.Length > 0)
            Console.WriteLine($"  Exclude: {string.Join(", ", _excludeProjects)}");
        Console.WriteLine();

        // 1. Find all source .resx files
        var sourceFiles = FindSourceFiles();
        if (sourceFiles.Count == 0)
        {
            Console.WriteLine("No source .resx files found.");
            return;
        }

        Console.WriteLine($"Found {sourceFiles.Count} source .resx files");

        // 2. Determine target cultures
        var targetCultures = _targetCultures.Length > 0
            ? _targetCultures
            : DiscoverExistingTargetCultures(sourceFiles);

        if (targetCultures.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No target cultures specified or discovered. Use --target to specify.");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"  Targets: {string.Join(", ", targetCultures)}");
        Console.WriteLine();

        // 3. Delta detection
        var delta = new DeltaDetector(_repoRoot);
        var tasks = new List<TranslationTask>();

        foreach (var sourceFile in sourceFiles)
        {
            var entries = ResxParser.Parse(sourceFile);
            if (entries.Count == 0) continue;

            var changedEntries = delta.GetChangedEntries(
                RelativePath(sourceFile), entries, _forceAll);
            var removedKeys = delta.GetRemovedKeys(RelativePath(sourceFile), entries);

            foreach (var targetCulture in targetCultures)
            {
                var targetPath = GetTargetPath(sourceFile, targetCulture);
                var existingTargetKeys = ResxParser.GetKeys(targetPath);
                var missingEntries = entries.Where(e => !existingTargetKeys.Contains(e.Name)).ToList();
                var entriesToTranslate = !File.Exists(targetPath)
                    ? entries
                    : changedEntries.Concat(missingEntries).DistinctBy(e => e.Name).ToList();
                tasks.Add(new TranslationTask(
                    sourceFile, targetPath, targetCulture, entriesToTranslate, removedKeys));
            }

            // Update hashes for source file (after processing all targets)
            if (!_dryRun)
                delta.UpdateHashes(RelativePath(sourceFile), entries);
        }

        // Count total keys to translate
        var totalKeys = tasks.Sum(t => t.KeysToTranslate.Count);
        var totalRemovals = tasks.Sum(t => t.KeysToRemove.Count);

        Console.WriteLine($"Delta: {totalKeys} keys to translate, {totalRemovals} keys to remove");

        if (totalKeys == 0 && totalRemovals == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Everything up to date. Nothing to translate.");
            Console.ResetColor();
            return;
        }

        if (_dryRun)
        {
            PrintDryRun(tasks);
            return;
        }

        // 4. Translate — group all unique texts, call API once per target language set
        using var translator = new TranslatorService(_translatorKey, _translatorRegion);

        // Collect unique texts to translate across all tasks
        var allTextsToTranslate = tasks
            .SelectMany(t => t.KeysToTranslate.Select(e => e.Value))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct()
            .ToList();

        Console.WriteLine($"Sending {allTextsToTranslate.Count} unique texts to Azure Translator...");

        // Extract source language code (pl-PL → pl), mapped to Azure Translator code
        var sourceLanguageCode = ToAzureLangCode(_sourceCulture);

        // Translate to all target languages in one batch call
        var targetLangCodes = targetCultures
            .Select(ToAzureLangCode)
            .Distinct()
            .ToArray();

        var translations = await translator.TranslateBatchAsync(
            sourceLanguageCode, targetLangCodes, allTextsToTranslate);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ Received translations for {targetLangCodes.Length} language(s)");
        Console.ResetColor();

        // 5. Merge translations into target files
        var filesWritten = 0;
        foreach (var task in tasks)
        {
            if (task.KeysToTranslate.Count == 0 && task.KeysToRemove.Count == 0)
                continue;

            var targetLangCode = ToAzureLangCode(task.TargetCulture);
            if (!translations.TryGetValue(targetLangCode, out var langTranslations))
                continue;

            var translatedEntries = new Dictionary<string, string>();
            foreach (var entry in task.KeysToTranslate)
            {
                if (langTranslations.TryGetValue(entry.Value, out var translated))
                    translatedEntries[entry.Name] = translated;
            }

            var keysToRemove = task.KeysToRemove.ToHashSet();

            ResxParser.MergeAndSave(
                task.TargetFilePath,
                task.SourceFilePath,
                translatedEntries,
                keysToRemove);

            filesWritten++;
            Console.WriteLine($"  → {RelativePath(task.TargetFilePath)} ({translatedEntries.Count} translated, {keysToRemove.Count} removed)");
        }

        // 6. Save hash cache
        delta.Save();

        // 7. Update supported-languages.json
        foreach (var culture in targetCultures)
        {
            LanguageConfigUpdater.EnsureCultureExists(_repoRoot, culture);
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Done! {filesWritten} file(s) updated. Changes are local — commit when ready.");
        Console.ResetColor();
    }

    private List<string> FindSourceFiles()
    {
        var allResx = Directory.GetFiles(_repoRoot, "*.resx", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(MatchesProjectFilter)
            .ToList();

        var primarySuffix = $".{_sourceCulture}.resx";
        var primaryFiles = allResx
            .Where(f => f.EndsWith(primarySuffix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var fallbackCulture = GetFallbackSourceCulture();
        if (fallbackCulture is null)
            return primaryFiles.OrderBy(f => f).ToList();

        var fallbackSuffix = $".{fallbackCulture}.resx";
        var primaryBases = new HashSet<string>(
            primaryFiles.Select(f => f[..^primarySuffix.Length]),
            StringComparer.OrdinalIgnoreCase);

        var fallbackFiles = allResx
            .Where(f => f.EndsWith(fallbackSuffix, StringComparison.OrdinalIgnoreCase))
            .Where(f => !primaryBases.Contains(f[..^fallbackSuffix.Length]))
            .ToList();

        if (fallbackFiles.Count > 0)
            Console.WriteLine($"  Fallback source culture '{fallbackCulture}' used for {fallbackFiles.Count} file(s) where '{_sourceCulture}' not found");

        return primaryFiles.Concat(fallbackFiles).OrderBy(f => f).ToList();
    }

    /// <summary>
    /// Returns the neutral-language fallback for a region-specific culture
    /// (e.g. "pl-PL" → "pl"), or null if the source culture has no region part.
    /// </summary>
    private string? GetFallbackSourceCulture()
    {
        var dashIdx = _sourceCulture.IndexOf('-');
        return dashIdx > 0 ? _sourceCulture[..dashIdx] : null;
    }

    /// <summary>
    /// Returns the actual source-file suffix for a given file, honoring the
    /// pl-PL → pl fallback so target paths are computed against the right base name.
    /// </summary>
    private string GetSourceSuffixForFile(string sourceFile)
    {
        var primary = $".{_sourceCulture}.resx";
        if (sourceFile.EndsWith(primary, StringComparison.OrdinalIgnoreCase))
            return primary;

        var fallback = GetFallbackSourceCulture();
        if (fallback is not null)
        {
            var fallbackSuffix = $".{fallback}.resx";
            if (sourceFile.EndsWith(fallbackSuffix, StringComparison.OrdinalIgnoreCase))
                return fallbackSuffix;
        }

        return primary;
    }

    /// <summary>
    /// Check if a file path matches the --include / --exclude project filters.
    /// Matches against the first directory segment relative to repo root
    /// (e.g. "RentoomBookingWeb", "StayWell").
    /// </summary>
    private bool MatchesProjectFilter(string filePath)
    {
        var relativePath = Path.GetRelativePath(_repoRoot, filePath);
        var firstSegment = relativePath.Split(Path.DirectorySeparatorChar, '/')[0];

        if (_includeProjects.Length > 0)
            return _includeProjects.Any(p =>
                firstSegment.Equals(p, StringComparison.OrdinalIgnoreCase));

        if (_excludeProjects.Length > 0)
            return !_excludeProjects.Any(p =>
                firstSegment.Equals(p, StringComparison.OrdinalIgnoreCase));

        return true;
    }

    private string[] DiscoverExistingTargetCultures(List<string> sourceFiles)
    {
        // Look at sibling .resx files to discover what cultures already exist
        var cultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceSuffix = $".{_sourceCulture}.resx";

        foreach (var sourceFile in sourceFiles)
        {
            var baseName = sourceFile[..^sourceSuffix.Length];
            var dir = Path.GetDirectoryName(sourceFile)!;

            foreach (var file in Directory.GetFiles(dir, "*.resx"))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var sourceBaseName = Path.GetFileNameWithoutExtension(
                    Path.GetFileNameWithoutExtension(sourceFile));

                // Skip the source file itself and base .resx
                if (file.Equals(sourceFile, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Extract culture from filename like "HomePage.en-us.resx"
                var name = Path.GetFileName(file);
                var parts = name.Split('.');
                if (parts.Length >= 3 && parts[^1].Equals("resx", StringComparison.OrdinalIgnoreCase))
                {
                    var culture = parts[^2]; // e.g. "en-us", "pl-PL"
                    var fallback = GetFallbackSourceCulture();
                    if (!culture.Equals(_sourceCulture, StringComparison.OrdinalIgnoreCase) &&
                        (fallback is null || !culture.Equals(fallback, StringComparison.OrdinalIgnoreCase)) &&
                        culture.Contains('-'))
                    {
                        cultures.Add(culture);
                    }
                }
            }
        }

        return cultures.ToArray();
    }

    private string GetTargetPath(string sourceFile, string targetCulture)
    {
        var sourceSuffix = GetSourceSuffixForFile(sourceFile);
        var basePath = sourceFile[..^sourceSuffix.Length];
        return $"{basePath}.{targetCulture}.resx";
    }

    private string RelativePath(string fullPath)
    {
        return Path.GetRelativePath(_repoRoot, fullPath).Replace('\\', '/');
    }

    private void PrintDryRun(List<TranslationTask> tasks)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n=== DRY RUN — no changes will be made ===\n");
        Console.ResetColor();

        foreach (var task in tasks.Where(t => t.KeysToTranslate.Count > 0 || t.KeysToRemove.Count > 0))
        {
            Console.WriteLine($"  {RelativePath(task.TargetFilePath)}:");

            var targetEntries = File.Exists(task.TargetFilePath)
                ? ResxParser.Parse(task.TargetFilePath, includeEmpty: true)
                    .ToDictionary(e => e.Name, e => e.Value)
                : new Dictionary<string, string>();

            foreach (var entry in task.KeysToTranslate)
            {
                if (targetEntries.TryGetValue(entry.Name, out var currentValue))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"    ~ [CHANGED] [{entry.Name}]");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine($"        src: \"{Truncate(entry.Value, 80)}\"");
                    Console.WriteLine($"        cur: \"{Truncate(currentValue, 80)}\"");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"    + [NEW]     [{entry.Name}]");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine($"        src: \"{Truncate(entry.Value, 80)}\"");
                }
            }

            foreach (var key in task.KeysToRemove)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"    - [REMOVE]  [{key}]");
                Console.ResetColor();
                if (targetEntries.TryGetValue(key, out var removedValue))
                    Console.WriteLine($"\n        cur: \"{Truncate(removedValue, 80)}\"");
                else
                    Console.WriteLine();
            }

            Console.WriteLine();
        }
    }

    private static string Truncate(string s, int maxLen)
        => s.Length <= maxLen ? s : s[..maxLen] + "…";

    /// <summary>
    /// Maps a .NET culture code to the Azure Translator language code.
    /// Handles three cases:
    ///   - Simple override: "no" → "nb"
    ///   - Script subtag preserved: "sr-Latn-RS" → "sr-Latn", "sr-Cyrl-RS" → "sr-Cyrl"
    ///   - Plain language code: "pl-PL" → "pl"
    /// </summary>
    private static string ToAzureLangCode(string cultureCode)
    {
        var parts = cultureCode.Split('-');
        var langCode = parts[0];

        // If the second part is a 4-character script subtag (e.g. "Latn", "Cyrl"),
        // preserve it — Azure Translator requires it for some languages (e.g. Serbian).
        if (parts.Length >= 2 && parts[1].Length == 4 && char.IsUpper(parts[1][0]))
        {
            var langWithScript = $"{langCode}-{parts[1]}";
            return AzureLangCodeOverrides.TryGetValue(langWithScript, out var scriptMapped)
                ? scriptMapped
                : langWithScript;
        }

        return AzureLangCodeOverrides.TryGetValue(langCode, out var mapped) ? mapped : langCode;
    }

    /// <summary>
    /// Maps .NET language/culture codes that differ from Azure Translator language codes.
    /// </summary>
    private static readonly Dictionary<string, string> AzureLangCodeOverrides =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["no"]      = "nb",      // Norwegian → Norwegian Bokmål (Azure uses "nb", not "no")
            ["sr"]      = "sr-Latn", // Serbian without script → default to Latin
        };
}
