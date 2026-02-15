using Microsoft.AspNetCore.Components;

namespace RentoomBooking.StayWell.Services
{
    public class ModalService
    {
        public event Action<string, RenderFragment?, bool>? OnShow;

        public event Action? OnClose;

        public void ShowModal(string title, RenderFragment? renderFragment = null, bool hasImage = false)
        {
            OnShow?.Invoke(title, renderFragment, hasImage);
        }

        public void HideModal()
        {
            OnClose?.Invoke();
        }
    }
}