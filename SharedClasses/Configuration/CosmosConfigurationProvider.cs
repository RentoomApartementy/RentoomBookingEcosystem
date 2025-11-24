using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
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

        public static async Task<string?> GetCosmosEndpointAsync(IConfiguration configuration, bool isDevelopmentEnv)
        {
            // Development: read connection string from local config
            if (isDevelopmentEnv)
            {
                // Keep compatibility with existing code that uses GetConnectionString
                return configuration.GetConnectionString("AZURE_COSMOS_ENDPOINT")
                       ?? configuration["AZURE_COSMOS_ENDPOINT"];
            }

            // Production: read from Key Vault using DefaultAzureCredential (managed identity, CLI, etc.)
            try
            {
                var credential = new DefaultAzureCredential();
                var client = new SecretClient(new Uri(KeyVaultUrl), credential);

                var secret = await client.GetSecretAsync(SecretName);
                return secret.Value.Value;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to retrieve Cosmos connection string from Azure Key Vault. " +
                    "Ensure the application identity has Get permission for the secret and DefaultAzureCredential is configured.",
                    ex);
            }
        }
    }
}
