using Microsoft.Extensions.Hosting;
using Npgsql;

namespace RentoomBookingWeb.Services;

public sealed record FooterDatabaseInfo(string Name, string Host, string Database);

public sealed class FooterEnvironmentInfo
{
    public FooterEnvironmentInfo(string environmentName, string buildConfiguration, IReadOnlyList<FooterDatabaseInfo> databases)
    {
        EnvironmentName = environmentName;
        BuildConfiguration = buildConfiguration;
        Databases = databases;
    }

    public string EnvironmentName { get; }
    public string BuildConfiguration { get; }
    public IReadOnlyList<FooterDatabaseInfo> Databases { get; }

    public string Summary
    {
        get
        {
            var segments = Databases.Select(db => $"{db.Name}: {db.Host}/{db.Database}")
                .Concat(new[] { BuildConfiguration, EnvironmentName });
            return string.Join(" • ", segments);
        }
    }

    public static FooterEnvironmentInfo Create(IHostEnvironment environment, params (string Name, string ConnectionString)[] connections)
    {
        var databases = new List<FooterDatabaseInfo>();
        foreach (var connection in connections)
        {
            if (string.IsNullOrWhiteSpace(connection.ConnectionString))
            {
                continue;
            }

            var builder = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
            var host = string.IsNullOrWhiteSpace(builder.Host) ? "unknown-host" : builder.Host;
            var database = string.IsNullOrWhiteSpace(builder.Database) ? "unknown-db" : builder.Database;
            databases.Add(new FooterDatabaseInfo(connection.Name, host, database));
        }

        return new FooterEnvironmentInfo(environment.EnvironmentName, GetBuildConfiguration(), databases);
    }

    private static string GetBuildConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }
}