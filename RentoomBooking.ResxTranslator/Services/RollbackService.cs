using System.Text.Json;

namespace ResxTranslator.Services;

public sealed class RollbackService
{
    private readonly string _repoRoot;
    private readonly string[] _cultures;
    private readonly string _sourceCulture;
    private readonly bool _dryRun;
    private readonly string[] _includeProjects;
    private readonly string[] _excludeProjects;

    public RollbackService(
        string repoRoot,
        string[] cultures,
        string sourceCulture,
        bool dryRun,
        string[] includeProjects,
        string[] excludeProjects)
    {
        _repoRoot = repoRoot;
        _cultures = cultures;
        _sourceCulture = sourceCulture;
        _dryRun = dryRun;
        _includeProjects = includeProjects;
        _excludeProjects = excludeProjects;
    }

    public int Execute()
    {
        Console.WriteLine($"ResxTranslator Rollback — repo: {_repoRoot}");
        Console.WriteLine($"  Rolling back: {string.Join(", ", _cultures)}");
        Console.WriteLine($"  Dry run: {_dryRun}");
        if (_includeProjects.Length > 0)
            Console.WriteLine($"  Include only: {string.Join(", ", _includeProjects)}");
        if (_excludeProjects.Length > 0)
            Console.WriteLine($"  Exclude: {string.Join(", ", _excludeProjects)}");
        Console.WriteLine();

        foreach (var culture in _cultures)
        {
            if (culture.Equals(_sourceCulture, StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"ERROR: Cannot roll back the source culture '{_sourceCulture}'.");
                Console.ResetColor();
                return 1;
            }
        }

        var overallExitCode = 0;
        foreach (var culture in _cultures)
        {
            if (_cultures.Length > 1)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"── {culture} ──");
                Console.ResetColor();
            }

            var result = ExecuteForCulture(culture);
            if (result != 0) overallExitCode = result;

            if (_cultures.Length > 1)
                Console.WriteLine();
        }

        return overallExitCode;
    }

    private int ExecuteForCulture(string culture)
    {
        var filesToDelete = FindTargetFiles(culture);

        if (filesToDelete.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            var filterNote = _includeProjects.Length > 0 || _excludeProjects.Length > 0
                ? " (with current project filter)"
                : "";
            Console.WriteLine($"No .{culture}.resx files found{filterNote}. Nothing to do.");
            Console.ResetColor();
            return 0;
        }

        var allRepoWide = FindAllCultureFilesRepoWide(culture);
        var remainingAfterDeletion = allRepoWide
            .Except(filesToDelete, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var shouldUpdateConfig = remainingAfterDeletion.Count == 0;

        if (shouldUpdateConfig && IsDefaultCulture(culture))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(
                $"ERROR: '{culture}' is the default culture in supported-languages.json. " +
                "Assign a different default culture before rolling back.");
            Console.ResetColor();
            return 1;
        }

        if (_dryRun)
        {
            PrintDryRun(culture, filesToDelete, shouldUpdateConfig, remainingAfterDeletion.Count);
            return 0;
        }

        var deleted = 0;
        var errors = 0;
        foreach (var file in filesToDelete)
        {
            try
            {
                File.Delete(file);
                deleted++;
                Console.WriteLine($"  - {RelativePath(file)}");
            }
            catch (Exception ex)
            {
                errors++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"  ERROR deleting {RelativePath(file)}: {ex.Message}");
                Console.ResetColor();
            }
        }

        if (shouldUpdateConfig)
        {
            LanguageConfigUpdater.RemoveCulture(_repoRoot, culture);
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
            Console.WriteLine(
                $"  ⚠ {remainingAfterDeletion.Count} file(s) for '{culture}' remain outside the filtered scope — " +
                "supported-languages.json was NOT modified.");
            Console.WriteLine(
                $"  Run again without --include/--exclude to fully remove '{culture}' and clean up config.");
            Console.ResetColor();
        }

        Console.WriteLine();
        if (errors == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Rollback complete! {deleted} file(s) deleted.");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Rollback finished with {errors} error(s). {deleted} file(s) deleted.");
        }
        Console.ResetColor();

        return errors > 0 ? 1 : 0;
    }

    private List<string> FindTargetFiles(string culture)
    {
        var sourceSuffix = $".{_sourceCulture}.resx";
        return Directory.GetFiles(_repoRoot, "*.resx", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(sourceSuffix, StringComparison.OrdinalIgnoreCase))
            .Where(IsNotBinOrObj)
            .Where(MatchesProjectFilter)
            .Select(sf => $"{sf[..^sourceSuffix.Length]}.{culture}.resx")
            .Where(File.Exists)
            .OrderBy(f => f)
            .ToList();
    }

    private List<string> FindAllCultureFilesRepoWide(string culture)
    {
        var suffix = $".{culture}.resx";
        return Directory.GetFiles(_repoRoot, "*.resx", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .Where(IsNotBinOrObj)
            .ToList();
    }

    private bool MatchesProjectFilter(string filePath)
    {
        var relativePath = Path.GetRelativePath(_repoRoot, filePath);
        var firstSegment = relativePath.Split(Path.DirectorySeparatorChar, '/')[0];

        if (_includeProjects.Length > 0)
            return _includeProjects.Any(p => firstSegment.Equals(p, StringComparison.OrdinalIgnoreCase));

        if (_excludeProjects.Length > 0)
            return !_excludeProjects.Any(p => firstSegment.Equals(p, StringComparison.OrdinalIgnoreCase));

        return true;
    }

    private bool IsDefaultCulture(string culture)
    {
        var configPath = Path.Combine(_repoRoot, "SharedClasses", "supported-languages.json");
        if (!File.Exists(configPath)) return false;

        try
        {
            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);
            var defaultCulture = doc.RootElement
                .TryGetProperty("defaultCulture", out var prop) ? prop.GetString() : null;
            return defaultCulture?.Equals(culture, StringComparison.OrdinalIgnoreCase) ?? false;
        }
        catch
        {
            return false;
        }
    }

    private void PrintDryRun(string culture, List<string> filesToDelete, bool wouldUpdateConfig, int remainingCount)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=== DRY RUN — no changes will be made ===\n");
        Console.ResetColor();

        Console.WriteLine($"Files to delete ({filesToDelete.Count}):");
        foreach (var file in filesToDelete)
            Console.WriteLine($"  - {RelativePath(file)}");

        Console.WriteLine();

        if (wouldUpdateConfig)
        {
            Console.WriteLine("Config changes:");
            Console.WriteLine($"  - Remove '{culture}' from supported-languages.json");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(
                $"Config: NOT modified — {remainingCount} file(s) for '{culture}' remain outside the current filter.");
            Console.ResetColor();
        }
    }

    private string RelativePath(string fullPath)
        => Path.GetRelativePath(_repoRoot, fullPath).Replace('\\', '/');

    private static bool IsNotBinOrObj(string filePath)
        => !filePath.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
           !filePath.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar);
}
