using Microsoft.AspNetCore.Components;

namespace RentoomBooking.StayWell.Services
{
    public class ModalService
    {
        public event Action<string, RenderFragment?, string?>? OnShow;

        public event Action? OnClose;

        public void ShowModal(string title, RenderFragment? renderFragment = null, string? imageUrl = null)
        {
            OnShow?.Invoke(title, renderFragment, imageUrl);
        }

        public void HideModal()
        {
            OnClose?.Invoke();
        }
    }
}