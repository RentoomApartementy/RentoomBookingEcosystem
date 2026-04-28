using Microsoft.AspNetCore.Components;

namespace RentoomBooking.StayWell.Services
{
    public enum ModalVariant
    {
        Default = 0,
        Image = 1,
        AiChatFullscreen = 2,
        LiveChatFullscreen = 3
    }

    public class ModalService
    {
        public event Action<string, RenderFragment?, ModalVariant>? OnShow;

        public event Action? OnClose;

        public void ShowModal(string title, RenderFragment? renderFragment = null, bool hasImage = false)
        {
            OnShow?.Invoke(title, renderFragment, hasImage ? ModalVariant.Image : ModalVariant.Default);
        }

        public void ShowAiChatModal(string title, RenderFragment? renderFragment = null)
        {
            OnShow?.Invoke(title, renderFragment, ModalVariant.AiChatFullscreen);
        }

        public void ShowLiveChatModal(string title, RenderFragment? renderFragment = null)
        {
            OnShow?.Invoke(title, renderFragment, ModalVariant.LiveChatFullscreen);
        }

        public void HideModal()
        {
            OnClose?.Invoke();
        }
    }
}
