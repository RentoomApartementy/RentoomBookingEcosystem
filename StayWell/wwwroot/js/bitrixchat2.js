window.bitrixInterop = {
    widget: null,

    init: function () {
        window.addEventListener('onBitrixLiveChat', function (event) {
            window.bitrixInterop.widget = event.detail.widget;
        });
    },

    openChat: function (readyJsonData) {
        var tryOpen = function () {
            var w = window.bitrixInterop.widget;

            if (w && typeof w.setCustomData === 'function') {
                w.setCustomData(readyJsonData);
                w.open();
                console.log("udało się załadować dane")
            }
            else {
                if (w) w.open();
                console.warn("nie dało się załadować danych");
            }
        };
        tryOpen();
    },

    destroy: function () {
        const selectors = ['.b24-widget-button-wrapper', 'iframe[src*="bitrix24"]', 'script[src*="bitrix24"]'];
        selectors.forEach(s => document.querySelectorAll(s).forEach(el => el.remove()));
        window.bitrixInterop.widget = null;
    }
};