namespace RentoomBooking.StayWell.States
{
    public class LayoutState
    {
        private bool _showFooter = true;
        private bool _showHeader = true;

        public bool ShowFooter
        {
            get => _showFooter;
            set
            {
                if(_showFooter != value)
                {
                    _showFooter = value;
                    NotifyStateChanged();
                }
            }
        }

        public bool ShowHeader
        {
            get => _showHeader;
            set
            {
                if (_showHeader != value)
                {
                    _showHeader = value;
                    NotifyStateChanged();
                }
            }
        }

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
