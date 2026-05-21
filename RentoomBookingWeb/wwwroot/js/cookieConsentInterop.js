window.rentoomCookieInterop = {
    trackingEnabled: false,
    sessionRecordingEnabled: false,

    enableTracking: function (gtmId, gaId) {
        if (window.rentoomCookieInterop.trackingEnabled) {
            return;
        }

        window.rentoomCookieInterop.trackingEnabled = true;
        window.dataLayer = window.dataLayer || [];

        if (!document.getElementById("rentoom-gtm-script") && gtmId) {
            const script = document.createElement("script");
            script.id = "rentoom-gtm-script";
            script.async = true;
            script.src = "https://www.googletagmanager.com/gtm.js?id=" + encodeURIComponent(gtmId);
            document.head.appendChild(script);
            window.dataLayer.push({
                "gtm.start": new Date().getTime(),
                event: "gtm.js"
            });
        }

        if (!document.getElementById("rentoom-ga-script") && gaId) {
            const gaScript = document.createElement("script");
            gaScript.id = "rentoom-ga-script";
            gaScript.async = true;
            gaScript.src = "https://www.googletagmanager.com/gtag/js?id=" + encodeURIComponent(gaId);
            document.head.appendChild(gaScript);
        }

        window.gtag = window.gtag || function () {
            window.dataLayer.push(arguments);
        };

        if (gaId) {
            window.gtag("js", new Date());
            window.gtag("config", gaId);
        }
    },

    enableSessionRecording: function (projectId) {
        if (window.rentoomCookieInterop.sessionRecordingEnabled) {
            return;
        }

        const normalizedProjectId = projectId || "07445cfba2a7a";
        if (document.getElementById("rentoom-contentsquare-script")) {
            window.rentoomCookieInterop.sessionRecordingEnabled = true;
            return;
        }

        const csScript = document.createElement("script");
        csScript.id = "rentoom-contentsquare-script";
        csScript.async = true;
        csScript.src = "https://t.contentsquare.net/uxa/" + encodeURIComponent(normalizedProjectId) + ".js";
        document.head.appendChild(csScript);

        window.rentoomCookieInterop.sessionRecordingEnabled = true;
    }
};

window.rentoomAnalytics = {
    trackEvent: function (eventName, parameters) {
        if (!window.rentoomCookieInterop?.trackingEnabled || typeof window.gtag !== "function" || !eventName) {
            return;
        }

        const normalizedParameters = {};
        if (parameters && typeof parameters === "object") {
            Object.keys(parameters).forEach(key => {
                const value = parameters[key];
                if (value !== null && value !== undefined && value !== "") {
                    normalizedParameters[key] = value;
                }
            });
        }

        window.gtag("event", eventName, normalizedParameters);
    }
};
