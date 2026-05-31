using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Logging;

namespace RentoomBooking.StayWell.Services;

public sealed class FeatureFlagsService
{
    private readonly Dictionary<string, FeatureFlagEntry> _features;
    private readonly ILogger<FeatureFlagsService> _logger;

    public FeatureFlagsService(
        IConfiguration configuration,
        IWebAssemblyHostEnvironment hostEnvironment,
        ILogger<FeatureFlagsService> logger)
    {
        _logger = logger;

        var configuredEnvironment = configuration["ASPNETCORE_ENVIRONMENT_STAYWELL"];
        CurrentEnvironmentName = string.IsNullOrWhiteSpace(configuredEnvironment)
            ? hostEnvironment.Environment
            : configuredEnvironment.Trim();

        var options = configuration
            .GetSection("FeatureFlags")
            .Get<FeatureFlagsOptions>();

        _features = options?.Features is null
            ? new Dictionary<string, FeatureFlagEntry>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, FeatureFlagEntry>(options.Features, StringComparer.OrdinalIgnoreCase);

        LogFeatureFlagsStatus();
    }

    public string CurrentEnvironmentName { get; }

    public bool FeatureAllowed(string featureName)
    {
        if (string.IsNullOrWhiteSpace(featureName))
            return false;

        if (!_features.TryGetValue(featureName.Trim(), out var feature) || feature is null)
            return false;

        if (!feature.Enabled)
            return false;

        if (feature.AllowedEnvironments is null || feature.AllowedEnvironments.Length == 0)
            return false;

        return feature.AllowedEnvironments.Any(env =>
            !string.IsNullOrWhiteSpace(env)
            && string.Equals(env.Trim(), CurrentEnvironmentName, StringComparison.OrdinalIgnoreCase));
    }

    private void LogFeatureFlagsStatus()
    {
        if (_features.Count == 0)
        {
            _logger.LogWarning(
                "Feature flags configuration is empty for environment {EnvironmentName}",
                CurrentEnvironmentName);
            return;
        }

        foreach (var (featureName, feature) in _features)
        {
            if (feature is null)
            {
                _logger.LogWarning(
                    "Feature flag {FeatureName} has null configuration in environment {EnvironmentName}",
                    featureName,
                    CurrentEnvironmentName);
                continue;
            }

            var allowedEnvironments = feature.AllowedEnvironments ?? Array.Empty<string>();
            var allowedEnvironmentsForLog = string.Join(",", allowedEnvironments.Where(env => !string.IsNullOrWhiteSpace(env)).Select(env => env.Trim()));

            var isEnabledForCurrentEnvironment = feature.Enabled
                && allowedEnvironments.Any(env =>
                    !string.IsNullOrWhiteSpace(env)
                    && string.Equals(env.Trim(), CurrentEnvironmentName, StringComparison.OrdinalIgnoreCase));

            _logger.LogInformation(
                "Feature flag status: {FeatureName} => {FeatureState} for environment {EnvironmentName}. Enabled={Enabled}, AllowedEnvironments={AllowedEnvironments}",
                featureName,
                isEnabledForCurrentEnvironment ? "ON" : "OFF",
                CurrentEnvironmentName,
                feature.Enabled,
                allowedEnvironmentsForLog);
        }
    }
}
