namespace RentoomBooking.StayWell.States
{
    public class LayoutState
    {
        private bool _showFooter = true;
        private bool _showHeader = true;
        private bool _isContentReady = true;

        /// <summary>
        /// Gdy false, MainLayout pokazuje loader zamiast @Body. Ustawiane przez PageBase na czas
        /// bootstrapu/redirectu, aby gość nie zobaczył błysku treści strony, z której zaraz jest przekierowywany.
        /// Default true, by strony spoza PageBase (NotFound/Error) renderowały się normalnie.
        /// </summary>
        public bool IsContentReady
        {
            get => _isContentReady;
            set
            {
                if (_isContentReady != value)
                {
                    _isContentReady = value;
                    NotifyStateChanged();
                }
            }
        }

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
