using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using RentoomBooking.StayWell.Services;

namespace RentoomBooking.StayWell.Components;

public sealed class TelemetryErrorBoundary : ErrorBoundary
{
    [Inject] public FrontendTelemetryService FrontendTelemetry { get; set; } = null!;

    protected override async Task OnErrorAsync(Exception exception)
    {
        await FrontendTelemetry.TrackExceptionAsync(
            exception,
            "blazor-error-boundary");

        await base.OnErrorAsync(exception);
    }
}