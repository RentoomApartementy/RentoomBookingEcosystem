using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RentoomBooking.Api.LiveChat.Data;

public sealed class LiveChatDesignTimeDbContextFactory : IDesignTimeDbContextFactory<LiveChatDbContext>
{
    private static readonly string[] ConnectionStringKeys =
    {
        "POSTGRES_RENTOOM_BOOKING_DB_LOCAL",
        "LIVECHAT_DB_CONNECTION",
        "ConnectionStrings__POSTGRES_RENTOOM_BOOKING_DB_LOCAL",
        "ConnectionStrings:POSTGRES_RENTOOM_BOOKING_DB_LOCAL",
    };

    public LiveChatDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString(args);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Unable to resolve LiveChatDbContext connection string. " +
                "Set environment variable POSTGRES_RENTOOM_BOOKING_DB_LOCAL or LIVECHAT_DB_CONNECTION, " +
                "or define ConnectionStrings.POSTGRES_RENTOOM_BOOKING_DB_LOCAL in Api/local.settings.json.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<LiveChatDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new LiveChatDbContext(optionsBuilder.Options);
    }

    private static string? ResolveConnectionString(string[] args)
    {
        var fromArgs = TryGetFromArgs(args);
        if (!string.IsNullOrWhiteSpace(fromArgs))
            return fromArgs;

        foreach (var key in ConnectionStringKeys)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        foreach (var path in GetLocalSettingsCandidates())
        {
            var value = TryReadConnectionStringFromLocalSettings(path);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string? TryGetFromArgs(string[] args)
    {
        foreach (var arg in args)
        {
            const string prefix = "--connection=";
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = arg[prefix.Length..].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }
        return null;
    }

    private static IEnumerable<string> GetLocalSettingsCandidates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in GetSearchRoots())
        {
            var current = new DirectoryInfo(root);
            while (current is not null)
            {
                var localSettings = Path.Combine(current.FullName, "local.settings.json");
                if (seen.Add(localSettings))
                    yield return localSettings;

                var apiLocalSettings = Path.Combine(current.FullName, "Api", "local.settings.json");
                if (seen.Add(apiLocalSettings))
                    yield return apiLocalSettings;

                current = current.Parent;
            }
        }
    }

    private static IEnumerable<string> GetSearchRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var currentDirectory = Directory.GetCurrentDirectory();
        if (!string.IsNullOrWhiteSpace(currentDirectory))
            roots.Add(currentDirectory);

        var appContextDirectory = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(appContextDirectory))
            roots.Add(appContextDirectory);

        var assemblyDirectory = Path.GetDirectoryName(typeof(LiveChatDesignTimeDbContextFactory).Assembly.Location);
        if (!string.IsNullOrWhiteSpace(assemblyDirectory))
            roots.Add(assemblyDirectory);

        return roots;
    }

    private static string? TryReadConnectionStringFromLocalSettings(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(
                File.ReadAllText(path),
                new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });
            var root = doc.RootElement;

            if (TryReadFromSection(root, "ConnectionStrings", out var fromConnectionStrings))
                return fromConnectionStrings;

            if (TryReadFromSection(root, "Values", out var fromValues))
                return fromValues;
        }
        catch
        {
            // Ignore malformed files
        }

        return null;
    }

    private static bool TryReadFromSection(JsonElement root, string sectionName, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(sectionName, out var section) || section.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var key in ConnectionStringKeys)
        {
            if (section.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var candidate = prop.GetString();
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    value = candidate.Trim();
                    return true;
                }
            }
        }

        return false;
    }
}
