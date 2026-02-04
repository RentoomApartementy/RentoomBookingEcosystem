using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Configuration
{
    public static class PostgresConnectionStringProvider
    {
        private const string KeyVaultUrl = "https://kv-rentoombooking.vault.azure.net/";
        private const string ProductionSecretName = "PostgressConnectionStringProd";

        private static string MaskSecret(string? secret)
        {
            if (string.IsNullOrEmpty(secret)) return "<empty>";
            if (secret.Length <= 10) return new string('*', Math.Min(secret.Length, 4));
            return $"{secret[..6]}...{secret[^4..]}";
        }

        public static async Task<string?> GetPostgresConnectionStringAsync(IConfiguration configuration, string propertyName, bool isDevelopmentEnv, ILogger log)
        {
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));
            if (log is null) throw new ArgumentNullException(nameof(log));

            log.LogInformation("Resolving PostgreSQL connection string. isDevelopment={IsDev}", isDevelopmentEnv);

            if (isDevelopmentEnv)
            {
                var local = configuration.GetConnectionString(propertyName) ?? configuration[$"ConnectionStrings:{propertyName}"] ?? configuration[$"Values:{propertyName}"] ?? configuration[propertyName];



                if (!string.IsNullOrWhiteSpace(local))
                {
                    log.LogInformation("Using development PostgreSQL connection string from configuration (masked): {Masked}", MaskSecret(local));
                    return local;
                }

                log.LogWarning("Development PostgreSQL connection string not found in configuration.");
            }

            try
            {
                log.LogInformation("Retrieving PostgreSQL connection string from Key Vault '{KeyVaultUrl}' using secret '{SecretName}'", KeyVaultUrl, ProductionSecretName);
                var credential = new DefaultAzureCredential();
                var client = new SecretClient(new Uri(KeyVaultUrl), credential);
                var secretResponse = await client.GetSecretAsync(ProductionSecretName);
                var secretValue = secretResponse.Value?.Value;

                if (string.IsNullOrWhiteSpace(secretValue))
                {
                    log.LogWarning("Secret '{SecretName}' retrieved from Key Vault but is empty.", ProductionSecretName);
                    return secretValue;
                }

                log.LogInformation("Successfully retrieved PostgreSQL connection string from Key Vault (masked): {Masked}", MaskSecret(secretValue));
                return secretValue;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to retrieve PostgreSQL connection string from Key Vault '{KeyVaultUrl}' with secret '{SecretName}'", KeyVaultUrl, ProductionSecretName);
                throw new InvalidOperationException(
                    "Failed to retrieve PostgreSQL connection string from Azure Key Vault. Ensure the application identity has Get permission for the secret and DefaultAzureCredential is configured.",
                    ex);
            }
        }
    }
}
