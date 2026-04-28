using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Configuration;
using ResxTranslator.Services;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.Local.json", optional: true)
    .Build();

var rootCommand = new RootCommand(
    "Manage .resx resource file translations using Azure Cognitive Services Translator API. Use subcommands: translate (default) or rollback.");

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

// ── Translate command (default) ──
var targetOption = new Option<string[]>(
    "--target",
    description: "Target culture codes (e.g. en-US de-DE pl-PL). Omit to auto-detect and translate to all existing target cultures found in repository.")
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

// ── Translate command ──
var translateCommand = new Command("translate",
    "Translate .resx resource files to target languages using Azure Cognitive Services Translator API.");

translateCommand.AddOption(sourceOption);
translateCommand.AddOption(targetOption);
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
    var targets = ctx.ParseResult.GetValueForOption(targetOption) ?? [];
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
        targets,
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
var rollbackCultureOption = new Option<string>(
    "--culture",
    description: "Target culture code to remove (e.g. en-US). Must be different from --source culture.")
{ IsRequired = true };

var rollbackCommand = new Command("rollback",
    "Remove a target language from the repository. Deletes culture-specific .resx files and updates configuration (supported-languages.json and SupportedLanguagesConfig.cs).");

rollbackCommand.AddOption(sourceOption);
rollbackCommand.AddOption(rollbackCultureOption);
rollbackCommand.AddOption(dryRunOption);
rollbackCommand.AddOption(includeProjectsOption);
rollbackCommand.AddOption(excludeProjectsOption);
rollbackCommand.AddOption(repoRootOption);

rollbackCommand.SetHandler((InvocationContext ctx) =>
{
    var source          = ctx.ParseResult.GetValueForOption(sourceOption)!;
    var culture         = ctx.ParseResult.GetValueForOption(rollbackCultureOption)!;
    var dryRun          = ctx.ParseResult.GetValueForOption(dryRunOption);
    var includeProjects = ctx.ParseResult.GetValueForOption(includeProjectsOption) ?? [];
    var excludeProjects = ctx.ParseResult.GetValueForOption(excludeProjectsOption) ?? [];
    var repoRoot        = ctx.ParseResult.GetValueForOption(repoRootOption)
                          ?? FindRepoRoot(Directory.GetCurrentDirectory());

    var service = new RollbackService(
        repoRoot, culture, source, dryRun, includeProjects, excludeProjects);

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
