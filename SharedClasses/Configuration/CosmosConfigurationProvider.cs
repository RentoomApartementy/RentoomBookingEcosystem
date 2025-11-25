using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Configuration
{
    internal class CosmosConfigurationProvider
    {
    }

    public static class CosmosEndpointProvider
    {
        // Key Vault and secret name are constants here for simplicity.
        // Consider moving them to configuration if you need to change them per environment.
        private const string KeyVaultUrl = "https://kv-rentoombooking.vault.azure.net/";
        private const string SecretName = "Cosmos-Booking-Prod-ConnectionString";


        private static string MaskSecret(string? secret)
        {
            if (string.IsNullOrEmpty(secret)) return "<empty>";
            if (secret.Length <= 24) return new string('*', 8);
            return $"{secret.Substring(0, 8)}...{secret.Substring(secret.Length - 8)}";
        }

        public static async Task<string?> GetCosmosEndpointAsync(IConfiguration configuration, bool isDevelopmentEnv, ILogger log)
        {
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));
            if (log is null) throw new ArgumentNullException(nameof(log));

            log.LogInformation("GetCosmosEndpointAsync called. isDevelopment={IsDev}", isDevelopmentEnv);

            // Development: read connection string from local config
            if (isDevelopmentEnv)
            {
                var conn = configuration.GetConnectionString("AZURE_COSMOS_ENDPOINT")
                           ?? configuration["AZURE_COSMOS_ENDPOINT"];

                if (string.IsNullOrWhiteSpace(conn))
                {
                    log.LogWarning("AZURE_COSMOS_ENDPOINT not found in configuration.");
                    return conn;
                }

                log.LogInformation("Using local Cosmos connection string from configuration (masked): {Masked}", MaskSecret(conn));
                return conn;
            }

            // Production: read from Key Vault using DefaultAzureCredential (managed identity, CLI, etc.)
            try
            {
                log.LogInformation("Retrieving secret '{SecretName}' from Key Vault '{KeyVaultUrl}'", SecretName, KeyVaultUrl);

                var credential = new DefaultAzureCredential();
                var client = new SecretClient(new Uri(KeyVaultUrl), credential);

                var secretResponse = await client.GetSecretAsync(SecretName);
                var secretValue = secretResponse.Value?.Value;

                if (string.IsNullOrWhiteSpace(secretValue))
                {
                    log.LogWarning("Secret '{SecretName}' was retrieved but the value is empty.", SecretName);
                    return secretValue;
                }

                log.LogInformation("Successfully retrieved secret '{SecretName}' from Key Vault (masked): {Masked}", SecretName, MaskSecret(secretValue));
                return secretValue;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to retrieve Cosmos connection string from Key Vault '{KeyVaultUrl}' secret '{SecretName}'", KeyVaultUrl, SecretName);
                throw new InvalidOperationException(
                    "Failed to retrieve Cosmos connection string from Azure Key Vault. Ensure the application identity has Get permission for the secret and DefaultAzureCredential is configured.",
                    ex);
            }
        }
    }
}
