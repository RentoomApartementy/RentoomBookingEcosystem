using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace RentoomBooking.StayWell.Services;

public sealed class FrontendTelemetryService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly NavigationManager _navigationManager;
    private readonly ReservationTokenService _reservationTokenService;
    private bool _initialized;

    public FrontendTelemetryService(
        IJSRuntime jsRuntime,
        NavigationManager navigationManager,
        ReservationTokenService reservationTokenService)
    {
        _jsRuntime = jsRuntime;
        _navigationManager = navigationManager;
        _reservationTokenService = reservationTokenService;
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _jsRuntime.InvokeVoidAsync("staywellTelemetry.init");
        _initialized = true;
    }

    public async Task TrackExceptionAsync(Exception exception, string source, IDictionary<string, string?>? extraProperties = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var properties = await BuildPropertiesAsync(source, extraProperties);

        await _jsRuntime.InvokeVoidAsync("staywellTelemetry.trackException", new
        {
            message = exception.Message,
            type = exception.GetType().FullName,
            stack = exception.ToString(),
            properties
        });
    }

    public async Task TrackMessageAsync(string message, string source, IDictionary<string, string?>? extraProperties = null)
    {
        var properties = await BuildPropertiesAsync(source, extraProperties);

        await _jsRuntime.InvokeVoidAsync("staywellTelemetry.trackMessage", new
        {
            message,
            properties
        });
    }

    private async Task<Dictionary<string, string?>> BuildPropertiesAsync(string source, IDictionary<string, string?>? extraProperties)
    {
        var properties = new Dictionary<string, string?>
        {
            ["source"] = source,
            ["route"] = _navigationManager.ToBaseRelativePath(_navigationManager.Uri),
            ["absoluteUrl"] = _navigationManager.Uri,
            ["reservationToken"] = await _reservationTokenService.GetTokenAsync()
        };

        if (extraProperties is null)
        {
            return properties;
        }

        foreach (var pair in extraProperties)
        {
            properties[pair.Key] = pair.Value;
        }

        return properties;
    }
}