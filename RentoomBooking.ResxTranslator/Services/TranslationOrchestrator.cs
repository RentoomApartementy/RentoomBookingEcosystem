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
                // If the target file doesn't exist yet (new language), translate all entries
                var entriesToTranslate = !File.Exists(targetPath) ? entries : changedEntries;
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

        // Extract source language code (pl-PL → pl)
        var sourceLanguageCode = _sourceCulture.Split('-')[0];

        // Translate to all target languages in one batch call
        var targetLangCodes = targetCultures
            .Select(c => c.Split('-')[0])
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

            var targetLangCode = task.TargetCulture.Split('-')[0];
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
        var sourceSuffix = $".{_sourceCulture}.resx";
        return Directory.GetFiles(_repoRoot, "*.resx", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(sourceSuffix, StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => MatchesProjectFilter(f))
            .OrderBy(f => f)
            .ToList();
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
                    if (!culture.Equals(_sourceCulture, StringComparison.OrdinalIgnoreCase) &&
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
        var sourceSuffix = $".{_sourceCulture}.resx";
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
            foreach (var entry in task.KeysToTranslate)
                Console.WriteLine($"    + [{entry.Name}] = \"{Truncate(entry.Value, 60)}\"");
            foreach (var key in task.KeysToRemove)
                Console.WriteLine($"    - [{key}]");
        }
    }

    private static string Truncate(string s, int maxLen)
        => s.Length <= maxLen ? s : s[..maxLen] + "…";
}
