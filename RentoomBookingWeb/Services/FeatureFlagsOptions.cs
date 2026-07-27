namespace RentoomBookingWeb.Services;

public sealed class FeatureFlagsOptions
{
    public Dictionary<string, FeatureFlagEntry> Features { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class FeatureFlagEntry
{
    public bool Enabled { get; set; }
    public string[] AllowedEnvironments { get; set; } = [];
}
