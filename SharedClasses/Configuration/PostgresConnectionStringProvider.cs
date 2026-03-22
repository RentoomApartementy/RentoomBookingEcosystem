using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;

namespace RentoomBooking.SharedClasses.Configuration
{
    public static class PostgresConnectionStringProvider
    {
        private const string KeyVaultUrl = "https://kv-rentoombooking.vault.azure.net/";
        private const string ProductionSecretName = "PostgressConnectionStringProd";
        private const string PoolingSection = "PostgresPooling";

        private static string MaskSecret(string? secret)
        {
            if (string.IsNullOrEmpty(secret)) return "<empty>";
            if (secret.Length <= 10) return new string('*', Math.Min(secret.Length, 4));
            return $"{secret[..6]}...{secret[^4..]}";
        }

        private static string? GetConfigurationValue(IConfiguration configuration, string key)
        {
            return configuration[key] ?? configuration[$"Values:{key}"];
        }

        private static bool TryGetIntValue(IConfiguration configuration, string key, out int value)
        {
            return int.TryParse(GetConfigurationValue(configuration, key), out value);
        }

        private static bool TryGetBoolValue(IConfiguration configuration, string key, out bool value)
        {
            return bool.TryParse(GetConfigurationValue(configuration, key), out value);
        }

        private static string ApplyPoolingConfiguration(IConfiguration configuration, string connectionString, ILogger log)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var hasExplicitOverrides = false;

            if (TryGetBoolValue(configuration, $"{PoolingSection}:Enabled", out var poolingEnabled))
            {
                builder.Pooling = poolingEnabled;
                hasExplicitOverrides = true;
            }

            if (TryGetIntValue(configuration, $"{PoolingSection}:MinimumPoolSize", out var minimumPoolSize))
            {
                builder.MinPoolSize = minimumPoolSize;
                hasExplicitOverrides = true;
            }

            if (TryGetIntValue(configuration, $"{PoolingSection}:MaximumPoolSize", out var maximumPoolSize))
            {
                builder.MaxPoolSize = maximumPoolSize;
                hasExplicitOverrides = true;
            }

            if (TryGetIntValue(configuration, $"{PoolingSection}:ConnectionIdleLifetime", out var connectionIdleLifetime))
            {
                builder.ConnectionIdleLifetime = connectionIdleLifetime;
                hasExplicitOverrides = true;
            }

            if (TryGetIntValue(configuration, $"{PoolingSection}:ConnectionPruningInterval", out var connectionPruningInterval))
            {
                builder.ConnectionPruningInterval = connectionPruningInterval;
                hasExplicitOverrides = true;
            }

            if (TryGetIntValue(configuration, $"{PoolingSection}:Timeout", out var timeout))
            {
                builder.Timeout = timeout;
                hasExplicitOverrides = true;
            }

            if (TryGetIntValue(configuration, $"{PoolingSection}:CommandTimeout", out var commandTimeout))
            {
                builder.CommandTimeout = commandTimeout;
                hasExplicitOverrides = true;
            }

            if (!hasExplicitOverrides)
            {
                log.LogInformation(
                    "No PostgreSQL pooling overrides configured for {Host}/{Database}; using connection string as-is.",
                    builder.Host,
                    builder.Database);
                return connectionString;
            }

            log.LogInformation(
                "Applied PostgreSQL pooling overrides for {Host}/{Database}: Pooling={Pooling}, MinPoolSize={MinPoolSize}, MaxPoolSize={MaxPoolSize}, ConnectionIdleLifetime={ConnectionIdleLifetime}, ConnectionPruningInterval={ConnectionPruningInterval}, Timeout={Timeout}, CommandTimeout={CommandTimeout}",
                builder.Host,
                builder.Database,
                builder.Pooling,
                builder.MinPoolSize,
                builder.MaxPoolSize,
                builder.ConnectionIdleLifetime,
                builder.ConnectionPruningInterval,
                builder.Timeout,
                builder.CommandTimeout);

            return builder.ConnectionString;
        }

        public static string? GetPostgresConnectionString(IConfiguration configuration, string propertyName, bool isDevelopmentEnv, ILogger log)
        {
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));
            if (log is null) throw new ArgumentNullException(nameof(log));

            log.LogInformation("Resolving PostgreSQL connection string. isDevelopment={IsDev}", isDevelopmentEnv);

            if (isDevelopmentEnv)
            {
                var local = configuration.GetConnectionString(propertyName)
                    ?? configuration[$"ConnectionStrings:{propertyName}"]
                    ?? configuration[$"Values:{propertyName}"]
                    ?? configuration[propertyName];

                if (!string.IsNullOrWhiteSpace(local))
                {
                    log.LogInformation("Using development PostgreSQL connection string from configuration (masked): {Masked}", MaskSecret(local));
                    return ApplyPoolingConfiguration(configuration, local, log);
                }

                log.LogWarning("Development PostgreSQL connection string not found in configuration.");
            }

            try
            {
                /*  log.LogInformation("Retrieving PostgreSQL connection string from Key Vault '{KeyVaultUrl}' using secret '{SecretName}'", KeyVaultUrl, ProductionSecretName);
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
                */
                log.LogInformation("PROD: Retrieving PostgreSQL connection string from env variables using env_name '{SecretName}'", ProductionSecretName);
                var local = configuration.GetConnectionString(propertyName)
                    ?? configuration[$"ConnectionStrings:{propertyName}"]
                    ?? configuration[$"Values:{propertyName}"]
                    ?? configuration[propertyName];

                if (!string.IsNullOrWhiteSpace(local))
                {
                    log.LogInformation("Using PROD PostgreSQL connection string from configuration (masked): {Masked}", MaskSecret(local));
                    return ApplyPoolingConfiguration(configuration, local, log);
                }

                log.LogWarning("PROD PostgreSQL connection string not found in configuration.");

                return local;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "PROD Failed to retrieve PostgreSQL connection string from env_variables  '{SecretName}'", propertyName);
                throw new InvalidOperationException(
                    "PROD - postgress env variables: Failed to retrieve PostgreSQL connection string from Azure Key Vault. Ensure the application identity has Get permission for the secret and DefaultAzureCredential is configured.",
                    ex);
            }
        }
    }
}
