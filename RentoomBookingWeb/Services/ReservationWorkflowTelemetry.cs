using Microsoft.ApplicationInsights;

namespace RentoomBookingWeb.Services;

public class ReservationWorkflowTelemetry
{
    private readonly TelemetryClient _telemetryClient;

    public ReservationWorkflowTelemetry(TelemetryClient telemetryClient)
    {
        _telemetryClient = telemetryClient;
    }

    public void TrackEvent(string eventName, IDictionary<string, string?>? properties = null, IDictionary<string, double>? metrics = null)
    {
        _telemetryClient.TrackEvent(
            eventName,
            SanitizeProperties(properties),
            metrics);
    }

    private static IDictionary<string, string>? SanitizeProperties(IDictionary<string, string?>? properties)
    {
        if (properties is null || properties.Count == 0)
        {
            return null;
        }

        var sanitized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in properties)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            sanitized[pair.Key] = pair.Value;
        }

        return sanitized.Count == 0 ? null : sanitized;
    }
}
