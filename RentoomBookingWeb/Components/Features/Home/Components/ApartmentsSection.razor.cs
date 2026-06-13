using Microsoft.AspNetCore.Components;
using RentoomBookingWeb.Components.Features.Apartments.ViewModels;

namespace RentoomBookingWeb.Components.Features.Home.Components;

public partial class ApartmentsSection : ComponentBase, IAsyncDisposable
{
    [Inject] public IApartmentsViewModel ViewModel { get; set; } = default!;

    private bool _isDisposed;
    private readonly CancellationTokenSource _initCts = new();

    private void HandleViewModelChange()
    {
        if (_isDisposed) return;
        InvokeAsync(StateHasChanged);
    }

    protected override async Task OnInitializedAsync()
    {
        ViewModel.OnChange += HandleViewModelChange;
        await ViewModel.InitializeForSliderAsync(_initCts.Token);
    }

    public ValueTask DisposeAsync()
    {
        _isDisposed = true;
        _initCts.Cancel();
        _initCts.Dispose();
        ViewModel.OnChange -= HandleViewModelChange;
        return ValueTask.CompletedTask;
    }
}
