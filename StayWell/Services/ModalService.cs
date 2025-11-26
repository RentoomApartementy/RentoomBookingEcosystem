using Microsoft.AspNetCore.Components;

namespace RentoomBooking.StayWell.Services
{
    public class ModalService
    {

        public event Action<string, RenderFragment> OnShow;
        public event Action OnClose;

        public void ShowModal(string title, RenderFragment renderFragment)
        {
            OnShow?.Invoke(title, renderFragment);
        }

        public void HideModal()
        {
            OnClose?.Invoke();
        }

    }
}
