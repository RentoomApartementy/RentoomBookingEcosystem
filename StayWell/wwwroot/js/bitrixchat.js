window.bitrixChat = {
    open: function () {
        console.log("bitrixChat.open called");

        // Check if BX.LiveChatWidget exists
        if (typeof BX !== 'undefined' && BX.LiveChatWidget) {
            console.log("BX.LiveChatWidget detected");

            console.log(BX.LiveChatWidget.SubscriptionType.configLoaded);

            // Subscribe to events and wait for full initialization
            BX.LiveChatWidget.subscribe({
                type: BX.LiveChatWidget.SubscriptionType.configLoaded,
                callback: function () {
                    console.log("Widget configLoaded event received");

                    if (typeof BX.LiveChatWidget.setCustomData === 'function') {
                        console.log("setCustomData is available, setting custom data");

                        BX.LiveChatWidget.setCustomData([
                            { USER: { NAME: "John Smith", AVATAR: "" } },
                            {
                                GRID: [
                                    { NAME: "E-mail", VALUE: "john@smith.com", DISPLAY: "LINE" },
                                    { NAME: "Account Type", VALUE: "Premium", DISPLAY: "LINE" }
                                ]
                            }
                        ]);

                        console.log("Custom data set successfully");
                    } else {
                        console.error("setCustomData is still undefined");
                    }

                    // Finally open the chat
                    BX.LiveChatWidget.open();
                }
            });
        } else {
            console.warn("BX.LiveChatWidget not available yet, waiting for onBitrixLiveChat");

            // Fallback: listen for onBitrixLiveChat event
            window.addEventListener('onBitrixLiveChat', function (event) {
                console.log("onBitrixLiveChat fired");

                const widget = event.detail.widget;

                // Now check if setCustomData is available
                if (typeof BX.LiveChatWidget.setCustomData === 'function') {
                    console.log("setCustomData is now available (after onBitrixLiveChat)");

                    BX.LiveChatWidget.setCustomData([
                        { USER: { NAME: "John Smith", AVATAR: "" } },
                        {
                            GRID: [
                                { NAME: "E-mail", VALUE: "john@smith.com", DISPLAY: "LINE" },
                                { NAME: "Account Type", VALUE: "Premium", DISPLAY: "LINE" }
                            ]
                        }
                    ]);

                    console.log("Custom data set successfully");
                } else {
                    console.error("setCustomData still undefined even after onBitrixLiveChat");
                }

                // Open the widget
                widget.open();
            });
        }
    }
};


function destroyBitrixChat() {
    document.querySelectorAll('script[src*="bitrix24.pl"]').forEach(s => s.remove());
    document.querySelectorAll('.b24-widget-button-wrapper').forEach(el => el.remove());
    document.querySelectorAll('iframe[src*="bitrix24"]').forEach(iframe => iframe.remove());
}