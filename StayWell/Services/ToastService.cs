using Microsoft.AspNetCore.Components;

namespace RentoomBooking.StayWell.Services
{
    public class ToastService
    {
        public event Action<string, RenderFragment, int>? OnShow;
        public event Action? OnHide;

        public void ShowToast(string message, RenderFragment? content = null, int durationMs = 3000)
        {
            OnShow?.Invoke(message, content ?? (builder => builder.AddContent(0, message)), durationMs);
        }

        public void HideToast()
        {
            OnHide?.Invoke();
        }
    }
}