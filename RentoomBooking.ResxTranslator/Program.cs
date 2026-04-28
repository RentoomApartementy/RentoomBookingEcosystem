using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using ResxTranslator.Services;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.Local.json", optional: true)
    .Build();

var rootCommand = new RootCommand("""
    Manage .resx resource file translations using Azure Cognitive Services Translator API.

    Subcommands:
      translate   Translate .resx files to one or more target languages
      rollback    Remove one or more target languages from the repository

    Examples:
      translate --culture en-US --translator-key <key>
      translate --culture en-US de-DE fr-FR --translator-key <key>
      rollback  --culture en-US
      rollback  --culture de-DE fr-FR
    """);

// ── Shared options ──
var sourceOption = new Option<string>(
    "--source",
    getDefaultValue: () => "pl-PL",
    description: "Source culture code (e.g. pl-PL). Specifies the original language of the .resx files.");

var includeProjectsOption = new Option<string[]>(
    "--include",
    description: "Process only specified projects (e.g. RentoomBookingWeb StayWell). Matches project folder names in repository.")
{ AllowMultipleArgumentsPerToken = true };

var excludeProjectsOption = new Option<string[]>(
    "--exclude",
    description: "Skip specified projects (e.g. StayWell). Matches project folder names in repository.")
{ AllowMultipleArgumentsPerToken = true };

var repoRootOption = new Option<string?>(
    "--repo-root",
    description: "Path to repository root directory. Defaults to current working directory if not specified.");

// ── Culture option — shared between translate and rollback ──
var cultureOption = new Option<string[]>(
    "--culture",
    description: "Culture codes to process (e.g. en-US de-DE fr-FR). Required — specify at least one culture code.")
{ AllowMultipleArgumentsPerToken = true };

var allOption = new Option<bool>(
    "--all",
    getDefaultValue: () => false,
    description: "Re-translate all keys regardless of cache. Ignores cached translations and forces Azure API calls.");

var dryRunOption = new Option<bool>(
    "--dry-run",
    getDefaultValue: () => false,
    description: "Preview changes without modifying any files. Useful for testing translation before committing.");

var translatorKeyOption = new Option<string?>(
    "--translator-key",
    description: "Azure Translator API subscription key. If not provided, falls back to AZURE_TRANSLATOR_KEY environment variable.");

var translatorRegionOption = new Option<string?>(
    "--translator-region",
    description: "Azure Translator API region (e.g. polandcentral). Fallback order: AZURE_TRANSLATOR_REGION env var, appsettings.Local.json, default (polandcentral).");

// ── Add shared options to root ──
rootCommand.AddOption(sourceOption);
rootCommand.AddOption(dryRunOption);
rootCommand.AddOption(includeProjectsOption);
rootCommand.AddOption(excludeProjectsOption);
rootCommand.AddOption(repoRootOption);

// ── Translate command ──
var translateCommand = new Command("translate", """
    Translate .resx resource files to target languages using Azure Cognitive Services Translator API.

    Examples:
      translate --culture en-US --translator-key <key>
      translate --culture en-US de-DE fr-FR --source pl-PL --translator-key <key>
      translate --culture en-US --dry-run
      translate --culture en-US --all --translator-key <key>
      translate --culture en-US --include RentoomBookingWeb --translator-key <key>
    """);

translateCommand.AddOption(sourceOption);
translateCommand.AddOption(cultureOption);
translateCommand.AddOption(allOption);
translateCommand.AddOption(dryRunOption);
translateCommand.AddOption(includeProjectsOption);
translateCommand.AddOption(excludeProjectsOption);
translateCommand.AddOption(repoRootOption);
translateCommand.AddOption(translatorKeyOption);
translateCommand.AddOption(translatorRegionOption);

translateCommand.SetHandler(async (InvocationContext ctx) =>
{
    var source = ctx.ParseResult.GetValueForOption(sourceOption)!;
    var cultures = ctx.ParseResult.GetValueForOption(cultureOption) ?? [];
    var all = ctx.ParseResult.GetValueForOption(allOption);
    var dryRun = ctx.ParseResult.GetValueForOption(dryRunOption);
    var includeProjects = ctx.ParseResult.GetValueForOption(includeProjectsOption) ?? [];
    var excludeProjects = ctx.ParseResult.GetValueForOption(excludeProjectsOption) ?? [];
    var repoRoot = ctx.ParseResult.GetValueForOption(repoRootOption) ?? FindRepoRoot(Directory.GetCurrentDirectory());
    var translatorKey = ctx.ParseResult.GetValueForOption(translatorKeyOption)
                        ?? Environment.GetEnvironmentVariable("AZURE_TRANSLATOR_KEY")
                        ?? config["AzureTranslator:Key"];
    var translatorRegion = Environment.GetEnvironmentVariable("AZURE_TRANSLATOR_REGION")
                           ?? ctx.ParseResult.GetValueForOption(translatorRegionOption)
                           ?? config["AzureTranslator:Region"]
                           ?? "polandcentral";

    if (cultures.Length == 0)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine("ERROR: --culture is required. Specify one or more culture codes (e.g. --culture en-US de-DE).");
        Console.ResetColor();
        ctx.ExitCode = 1;
        return;
    }

    if (!ValidateCultures(cultures, out var cultureError))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"ERROR: {cultureError}");
        Console.ResetColor();
        ctx.ExitCode = 1;
        return;
    }

    if (string.IsNullOrWhiteSpace(translatorKey) && !dryRun)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine("ERROR: Azure Translator key is required. Use --translator-key or set AZURE_TRANSLATOR_KEY env var.");
        Console.ResetColor();
        ctx.ExitCode = 1;
        return;
    }

    var orchestrator = new TranslationOrchestrator(
        repoRoot,
        source,
        cultures,
        translatorKey ?? "",
        translatorRegion,
        all,
        dryRun,
        includeProjects,
        excludeProjects);

    await orchestrator.RunAsync();
});

rootCommand.AddCommand(translateCommand);

// ── Rollback command ──
var rollbackCommand = new Command("rollback", """
    Remove one or more target languages from the repository.
    Deletes culture-specific .resx files and updates SharedClasses/supported-languages.json.

    Examples:
      rollback --culture en-US
      rollback --culture de-DE fr-FR
      rollback --culture en-US --dry-run
      rollback --culture en-US --include RentoomBookingWeb
    """);

rollbackCommand.AddOption(sourceOption);
rollbackCommand.AddOption(cultureOption);
rollbackCommand.AddOption(dryRunOption);
rollbackCommand.AddOption(includeProjectsOption);
rollbackCommand.AddOption(excludeProjectsOption);
rollbackCommand.AddOption(repoRootOption);

rollbackCommand.SetHandler((InvocationContext ctx) =>
{
    var source          = ctx.ParseResult.GetValueForOption(sourceOption)!;
    var cultures        = ctx.ParseResult.GetValueForOption(cultureOption) ?? [];
    var dryRun          = ctx.ParseResult.GetValueForOption(dryRunOption);
    var includeProjects = ctx.ParseResult.GetValueForOption(includeProjectsOption) ?? [];
    var excludeProjects = ctx.ParseResult.GetValueForOption(excludeProjectsOption) ?? [];
    var repoRoot        = ctx.ParseResult.GetValueForOption(repoRootOption)
                          ?? FindRepoRoot(Directory.GetCurrentDirectory());

    if (cultures.Length == 0)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine("ERROR: --culture is required. Specify one or more culture codes to roll back (e.g. --culture en-US de-DE).");
        Console.ResetColor();
        ctx.ExitCode = 1;
        return;
    }

    if (!ValidateCultures(cultures, out var cultureError))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"ERROR: {cultureError}");
        Console.ResetColor();
        ctx.ExitCode = 1;
        return;
    }

    var service = new RollbackService(
        repoRoot, cultures, source, dryRun, includeProjects, excludeProjects);

    ctx.ExitCode = service.Execute();
});

rootCommand.AddCommand(rollbackCommand);

return await rootCommand.InvokeAsync(args);

static string FindRepoRoot(string startDir)
{
    var dir = new DirectoryInfo(startDir);
    while (dir != null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
            return dir.FullName;
        dir = dir.Parent;
    }
    return startDir;
}

static bool ValidateCultures(string[] cultures, out string? error)
{
    var known = new HashSet<string>(
        CultureInfo.GetCultures(CultureTypes.AllCultures).Select(c => c.Name),
        StringComparer.OrdinalIgnoreCase);

    foreach (var code in cultures)
    {
        if (!known.Contains(code))
        {
            error = $"'{code}' is not a recognised culture code (e.g. en-US, de-DE, fr-FR).";
            return false;
        }
    }
    error = null;
    return true;
}
