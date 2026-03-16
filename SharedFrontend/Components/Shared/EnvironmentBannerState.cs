namespace RentoomBooking.SharedFrontend.Components.Shared;

public static class EnvironmentBannerState
{
    public const string ProductionEnvironmentName = "Production";

    public static bool ShouldDisplay(string? environmentName)
    {
        return !string.IsNullOrWhiteSpace(environmentName)
            && !string.Equals(
                environmentName.Trim(),
                ProductionEnvironmentName,
                StringComparison.OrdinalIgnoreCase);
    }

    public static string GetDisplayName(string? environmentName)
    {
        return string.IsNullOrWhiteSpace(environmentName)
            ? string.Empty
            : environmentName.Trim().ToUpperInvariant();
    }
}
