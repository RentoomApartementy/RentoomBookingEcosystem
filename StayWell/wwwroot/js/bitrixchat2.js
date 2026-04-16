window.bitrixInterop = {
    widget: null,
    initialized: false,
    pendingCustomData: null,
    pendingOpen: false,
    openRetryTimer: null,
    configLoaded: false,

    init: function () {
        if (window.bitrixInterop.initialized) {
            return;
        }

        window.bitrixInterop.initialized = true;

        if (window.BX && window.BX.LiveChatWidget && window.BX.LiveChatWidget.open) {
            console.log("Bitrix widget already available at init time");
            window.bitrixInterop.widget = window.BX.LiveChatWidget;
            window.bitrixInterop.configLoaded = true;
            window.bitrixInterop.flushPending();
            return;
        }

        window.addEventListener("onBitrixLiveChat", function (event) {
            var w = event.detail.widget;
            window.bitrixInterop.widget = w;

            if (w && typeof w.subscribe === "function") {
                w.subscribe({
                    type: BX.LiveChatWidget.SubscriptionType.configLoaded,
                    callback: function () {
                        console.log("Bitrix configLoaded");
                        window.bitrixInterop.configLoaded = true;
                        window.bitrixInterop.flushPending();
                    }
                });

                if (typeof w.setCustomData === "function") {
                    console.log("Bitrix widget already config-loaded at subscribe time");
                    window.bitrixInterop.configLoaded = true;
                    window.bitrixInterop.flushPending();
                }
            }
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
        if (!w || !window.bitrixInterop.configLoaded) {
            return false;
        }

        if (window.bitrixInterop.pendingCustomData && typeof w.setCustomData === "function") {
            try {
                w.setCustomData(window.bitrixInterop.pendingCustomData);
                console.log("Bitrix customData ustawione:", window.bitrixInterop.pendingCustomData);
                window.bitrixInterop.pendingCustomData = null;
            } catch (error) {
                console.error("Bitrix setCustomData error", error);
            }
        }

        if (window.bitrixInterop.pendingOpen && typeof w.open === "function") {
            w.open();
            window.bitrixInterop.pendingOpen = false;
            console.log("Bitrix chat otwarty");
            return true;
        }

        return false;
    },

    scheduleRetry: function () {
        if (window.bitrixInterop.openRetryTimer) {
            return;
        }

        var attemptsLeft = 60;
        window.bitrixInterop.openRetryTimer = window.setInterval(function () {
            attemptsLeft -= 1;

            if (window.bitrixInterop.flushPending() || attemptsLeft <= 0) {
                if (attemptsLeft <= 0) {
                    console.error("Bitrix: przekroczono limit prob otwarcia chatu");
                }
                window.clearInterval(window.bitrixInterop.openRetryTimer);
                window.bitrixInterop.openRetryTimer = null;
            }
        }, 250);
    },

    openChat: function (readyJsonData) {
        window.bitrixInterop.pendingCustomData = window.bitrixInterop.normalizeCustomData(readyJsonData);
        window.bitrixInterop.pendingOpen = true;

        console.log("Bitrix openChat called, customData:", window.bitrixInterop.pendingCustomData);

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

        var selectors = [".b24-widget-button-wrapper", 'iframe[src*="bitrix24"]', 'script[src*="bitrix24"]'];
        selectors.forEach(function (selector) {
            document.querySelectorAll(selector).forEach(function (element) {
                element.remove();
            });
        });
        Object.keys(localStorage).forEach(function (key) {
            if (key.startsWith("bx-im-") || key.startsWith("b24-")) {
                localStorage.removeItem(key);
            }
        });

        window.bitrixInterop.widget = null;
        window.bitrixInterop.pendingCustomData = null;
        window.bitrixInterop.pendingOpen = false;
        window.bitrixInterop.configLoaded = false;
    }
};
