using Microsoft.JSInterop;

namespace RentoomBookingWeb.Services;

public class GoogleAnalyticsService
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _analyticsModule;

    public GoogleAnalyticsService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<bool> TrackEventAsync(
        string eventName,
        IDictionary<string, object?>? parameters = null,
        string? dedupeKey = null)
    {
        try
        {
            await EnsureAnalyticsInteropLoadedAsync();

            var result = await _jsRuntime.InvokeAsync<string?>("rentoomAnalytics.trackEvent", eventName, parameters, dedupeKey);
            return string.Equals(result, "sent", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result, "queued", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result, "duplicate", StringComparison.OrdinalIgnoreCase);
        }
        catch (InvalidOperationException)
        {
            // JS interop is not available during prerendering.
            return false;
        }
        catch (JSException)
        {
            // Frontend analytics should never break user flow.
            return false;
        }
    }

    private async Task EnsureAnalyticsInteropLoadedAsync()
    {
        _analyticsModule ??= await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/cookieConsentInterop.js");
    }
}
