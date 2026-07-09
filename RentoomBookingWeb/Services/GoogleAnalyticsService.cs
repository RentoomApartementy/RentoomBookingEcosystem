using Microsoft.JSInterop;

namespace RentoomBookingWeb.Services;

public class GoogleAnalyticsService
{
    private readonly IJSRuntime _jsRuntime;

    public GoogleAnalyticsService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<bool> TrackEventAsync(string eventName, IDictionary<string, object?>? parameters = null)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("rentoomAnalytics.trackEvent", eventName, parameters);
           // Console.WriteLine($"Tracked GOOGLE event: {eventName} with parameters: {parameters}");
            return true;
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
}
