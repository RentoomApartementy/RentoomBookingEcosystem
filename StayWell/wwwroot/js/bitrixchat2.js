window.bitrixInterop = {
    widget: null,
    initialized: false,
    pendingCustomData: null,
    pendingOpen: false,
    openRetryTimer: null,

    init: function () {
        if (window.bitrixInterop.initialized) {
            return;
        }

        window.bitrixInterop.initialized = true;
        window.addEventListener("onBitrixLiveChat", function (event) {
            window.bitrixInterop.widget = event.detail.widget;
            window.bitrixInterop.flushPending();
        });
    },

    enableLoader: function (loaderUrl) {
        if (typeof window.stayWellBitrixEnableLoader === "function") {
            window.stayWellBitrixEnableLoader(loaderUrl);
        }
    },

    normalizeCustomData: function (rawData) {
        if (!rawData) {
            return null;
        }

        if (typeof rawData === "string") {
            try {
                return JSON.parse(rawData);
            } catch (error) {
                console.error("Bitrix customData JSON parse error", error);
                return null;
            }
        }

        return rawData;
    },

    getWidget: function () {
        if (window.bitrixInterop.widget) {
            return window.bitrixInterop.widget;
        }

        if (window.BX && window.BX.LiveChatWidget) {
            window.bitrixInterop.widget = window.BX.LiveChatWidget;
            return window.bitrixInterop.widget;
        }

        return null;
    },

    flushPending: function () {
        var w = window.bitrixInterop.getWidget();
        if (!w) {
            return false;
        }

        if (window.bitrixInterop.pendingCustomData && typeof w.setCustomData === "function") {
            try {
                w.setCustomData(window.bitrixInterop.pendingCustomData);
                console.log("Bitrix customData ustawione");
            } catch (error) {
                console.error("Bitrix setCustomData error", error);
            }
        }

        if (window.bitrixInterop.pendingOpen && typeof w.open === "function") {
            w.open();
            window.bitrixInterop.pendingOpen = false;
            return true;
        }

        return false;
    },

    scheduleRetry: function () {
        if (window.bitrixInterop.openRetryTimer) {
            return;
        }

        var attemptsLeft = 40;
        window.bitrixInterop.openRetryTimer = window.setInterval(function () {
            attemptsLeft -= 1;

            if (window.bitrixInterop.flushPending() || attemptsLeft <= 0) {
                window.clearInterval(window.bitrixInterop.openRetryTimer);
                window.bitrixInterop.openRetryTimer = null;
            }
        }, 250);
    },

    openChat: function (readyJsonData) {
        window.bitrixInterop.pendingCustomData = window.bitrixInterop.normalizeCustomData(readyJsonData);
        window.bitrixInterop.pendingOpen = true;

        if (!window.bitrixInterop.flushPending()) {
            window.bitrixInterop.scheduleRetry();
            console.warn("Bitrix widget jeszcze niegotowy, ponawiam probe otwarcia");
        }
    },

    destroy: function () {
        if (window.bitrixInterop.openRetryTimer) {
            window.clearInterval(window.bitrixInterop.openRetryTimer);
            window.bitrixInterop.openRetryTimer = null;
        }

        const selectors = [".b24-widget-button-wrapper", 'iframe[src*="bitrix24"]', 'script[src*="bitrix24"]'];
        selectors.forEach(function (selector) {
            document.querySelectorAll(selector).forEach(function (element) {
                element.remove();
            });
        });
        window.bitrixInterop.widget = null;
        window.bitrixInterop.pendingCustomData = null;
        window.bitrixInterop.pendingOpen = false;
    }
};
