namespace RentoomBookingWeb.Services;

public sealed class FeatureFlagsService
{
    private readonly Dictionary<string, FeatureFlagEntry> _features;

    public FeatureFlagsService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        CurrentEnvironmentName = environment.EnvironmentName;
        var options = configuration.GetSection("FeatureFlags").Get<FeatureFlagsOptions>();
        _features = options?.Features is null
            ? new Dictionary<string, FeatureFlagEntry>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, FeatureFlagEntry>(options.Features, StringComparer.OrdinalIgnoreCase);
    }

    public string CurrentEnvironmentName { get; }

    public bool FeatureAllowed(string featureName)
    {
        if (string.IsNullOrWhiteSpace(featureName)
            || !_features.TryGetValue(featureName.Trim(), out var feature)
            || feature is null
            || !feature.Enabled)
        {
            return false;
        }

        return feature.AllowedEnvironments.Any(environment =>
            !string.IsNullOrWhiteSpace(environment)
            && string.Equals(environment.Trim(), CurrentEnvironmentName, StringComparison.OrdinalIgnoreCase));
    }
}
