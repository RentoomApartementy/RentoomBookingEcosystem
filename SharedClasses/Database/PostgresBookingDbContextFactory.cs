using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;

namespace RentoomBooking.SharedClasses.Database
{
    public class PostgresBookingDbContextFactory : IDesignTimeDbContextFactory<PostgresBookingDbContext>
    {
        public PostgresBookingDbContext CreateDbContext(string[] args)
        {
            var connectionString =
                Environment.GetEnvironmentVariable("POSTGRES_RENTOOM_BOOKING_DB_LOCAL")
                ?? Environment.GetEnvironmentVariable("ConnectionStrings__POSTGRES_RENTOOM_BOOKING_DB_LOCAL")
                ?? TryReadLocalSettingsConnectionString();

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Unable to resolve a PostgreSQL connection string for design-time operations.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<PostgresBookingDbContext>();
            optionsBuilder.UseNpgsql(connectionString);
            return new PostgresBookingDbContext(optionsBuilder.Options);
        }

        private static string? TryReadLocalSettingsConnectionString()
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var currentDirectoryCandidate = Path.Combine(currentDirectory, "Api", "local.settings.json");
            var siblingDirectoryCandidate = Path.GetFullPath(Path.Combine(currentDirectory, "..", "Api", "local.settings.json"));
            var appBaseDirectoryCandidate = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Api", "local.settings.json"));

            foreach (var candidate in new[] { currentDirectoryCandidate, siblingDirectoryCandidate, appBaseDirectoryCandidate })
            {
                if (!File.Exists(candidate))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(
                    File.ReadAllText(candidate),
                    new JsonDocumentOptions
                    {
                        CommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    });
                if (!document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings))
                {
                    continue;
                }

                if (connectionStrings.TryGetProperty("POSTGRES_RENTOOM_BOOKING_DB_LOCAL", out var connectionStringElement))
                {
                    return connectionStringElement.GetString();
                }
            }

            return null;
        }
    }
}
