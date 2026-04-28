using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Configuration;
using ResxTranslator.Services;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.Local.json", optional: true)
    .Build();

var rootCommand = new RootCommand("Translate .resx files using Azure AI Translator");

// ── Shared options ──
var sourceOption = new Option<string>(
    "--source",
    getDefaultValue: () => "pl-PL",
    description: "Source culture (e.g. pl-PL)");

var includeProjectsOption = new Option<string[]>(
    "--include",
    description: "Only affect these projects (e.g. RentoomBookingWeb StayWell). Matches folder names.")
{ AllowMultipleArgumentsPerToken = true };

var excludeProjectsOption = new Option<string[]>(
    "--exclude",
    description: "Skip these projects (e.g. StayWell). Matches folder names.")
{ AllowMultipleArgumentsPerToken = true };

var repoRootOption = new Option<string?>(
    "--repo-root",
    description: "Repository root path. Defaults to current directory.");

// ── Translate command (default) ──
var targetOption = new Option<string[]>(
    "--target",
    description: "Target cultures (e.g. en-US de-DE). If omitted, translates to all existing target cultures found in repo.")
{ AllowMultipleArgumentsPerToken = true };

var allOption = new Option<bool>(
    "--all",
    getDefaultValue: () => false,
    description: "Force re-translate all keys (ignore hash cache)");

var dryRunOption = new Option<bool>(
    "--dry-run",
    getDefaultValue: () => false,
    description: "Show what would be translated without making changes");

var translatorKeyOption = new Option<string?>(
    "--translator-key",
    description: "Azure Translator subscription key. Falls back to AZURE_TRANSLATOR_KEY env var.");

var translatorRegionOption = new Option<string?>(
    "--translator-region",
    description: "Azure Translator region. Falls back to AZURE_TRANSLATOR_REGION env var or appsettings.Local.json.");

rootCommand.AddOption(sourceOption);
rootCommand.AddOption(targetOption);
rootCommand.AddOption(allOption);
rootCommand.AddOption(dryRunOption);
rootCommand.AddOption(includeProjectsOption);
rootCommand.AddOption(excludeProjectsOption);
rootCommand.AddOption(repoRootOption);
rootCommand.AddOption(translatorKeyOption);
rootCommand.AddOption(translatorRegionOption);

// ── Rollback command ──
var rollbackCultureOption = new Option<string>(
    "--culture",
    description: "Target culture to roll back (e.g. en-US). Must not be the source culture.")
{ IsRequired = true };

var rollbackCommand = new Command("rollback",
    "Remove a target culture from the repo: deletes .resx files and cleans up supported-languages.json / SupportedLanguagesConfig.cs.");

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

rootCommand.SetHandler(async (InvocationContext ctx) =>
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
