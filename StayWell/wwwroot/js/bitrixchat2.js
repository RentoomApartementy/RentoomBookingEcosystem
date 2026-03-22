window.bitrixInterop = {
    widget: null,
    initialized: false,

    init: function () {
        if (window.bitrixInterop.initialized) {
            return;
        }

        window.bitrixInterop.initialized = true;
        window.addEventListener("onBitrixLiveChat", function (event) {
            window.bitrixInterop.widget = event.detail.widget;
        });
    },

    enableLoader: function (loaderUrl) {
        if (typeof window.stayWellBitrixEnableLoader === "function") {
            window.stayWellBitrixEnableLoader(loaderUrl);
        }
    },

    openChat: function (readyJsonData) {
        var tryOpen = function () {
            var w = window.bitrixInterop.widget;

            if (w && typeof w.setCustomData === "function") {
                w.setCustomData(readyJsonData);
                w.open();
                console.log("udalo sie zaladowac dane");
            }
            else {
                if (w) {
                    w.open();
                }
                console.warn("nie dalo sie zaladowac danych");
            }
        };

        tryOpen();
    },

    destroy: function () {
        const selectors = [".b24-widget-button-wrapper", 'iframe[src*="bitrix24"]', 'script[src*="bitrix24"]'];
        selectors.forEach(function (selector) {
            document.querySelectorAll(selector).forEach(function (element) {
                element.remove();
            });
        });
        window.bitrixInterop.widget = null;
    }
};
