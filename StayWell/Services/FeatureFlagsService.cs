using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace RentoomBooking.StayWell.Services;

public sealed class FeatureFlagsService
{
    private readonly Dictionary<string, FeatureFlagEntry> _features;

    public FeatureFlagsService(IConfiguration configuration, IWebAssemblyHostEnvironment hostEnvironment)
    {
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
}
