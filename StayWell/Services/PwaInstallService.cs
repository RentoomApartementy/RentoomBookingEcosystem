using Microsoft.JSInterop;

namespace RentoomBooking.StayWell.Services
{
    public class PwaInstallService(IJSRuntime jsRuntime)
    {
        private readonly IJSRuntime _jsRuntime = jsRuntime;
        private DotNetObjectReference<PwaInstallService>? _objRef;
        private bool _initialized;

        public bool IsInstallPromptAvailable { get; private set; }
        public event Action? OnChange;

        public async Task InitializeAsync()
        {
            if (_initialized)
            {
                return;
            }

            _objRef = DotNetObjectReference.Create(this);
            await _jsRuntime.InvokeVoidAsync("pwaInstall.init", _objRef);
            IsInstallPromptAvailable = await _jsRuntime.InvokeAsync<bool>("pwaInstall.canInstall");
            _initialized = true;
        }

        public async Task<bool> PromptInstallAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<bool>("pwaInstall.promptInstall");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PwaInstallService.PromptInstallAsync failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> IsStandaloneAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<bool>("pwaInstall.isStandalone");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PwaInstallService.IsStandaloneAsync failed: {ex.Message}");
                return false;
            }
        }

        [JSInvokable]
        public void SetInstallPromptAvailable(bool available)
        {
            IsInstallPromptAvailable = available;
            OnChange?.Invoke();
        }
    }
}
