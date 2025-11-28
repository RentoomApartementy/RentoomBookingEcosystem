
//stary kod z app.rentoom.pl - nie jest wywoływany obecnie
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



//na podstawie https://helpdesk.bitrix24.pl/open/10887560/
// działa poprawnie w czacie na birix24.pl widac ustawione dane
// TODO: przekazywac dane goscia dynamicznie z aplikacji poprzez zmienne JS (poprzez JS interop)
    window.addEventListener('onBitrixLiveChat', function(event)
    {
        var widget = event.detail.widget;

    // Setting custom data (get published at the beginning of a new conversation, extended format)
    widget.setCustomData([
    {"USER": {
        "NAME" : "John Smith",
    "AVATAR" : "http://files.smith.com/images/avatar-john.jpg",
            }},
    {"GRID": [
    {
        "NAME" : "E-mail",
    "VALUE" : "john@smith.com",
    "DISPLAY" : "LINE",
                },
    {
        "NAME" : "Customer ID",
    "VALUE" : "12234",
    "COLOR" : "#ff0000",
    "DISPLAY" : "LINE"
                },
    {
        "NAME": "Website",
    "VALUE": location.hostname,
    "DISPLAY": "LINE"
                },
    {
        "NAME": "Page",
    "VALUE": "[url="+location.href+"]"+(document.title || location.href)+"[/url]",
    "DISPLAY": "LINE"
                },
            ]}
    ]);
        
    });

function destroyBitrixChat() {
    document.querySelectorAll('script[src*="bitrix24.pl"]').forEach(s => s.remove());
    document.querySelectorAll('.b24-widget-button-wrapper').forEach(el => el.remove());
    document.querySelectorAll('iframe[src*="bitrix24"]').forEach(iframe => iframe.remove());
}