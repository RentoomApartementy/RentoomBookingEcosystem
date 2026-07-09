window.rentoomCookieInterop = window.rentoomCookieInterop || {};
window.rentoomCookieInterop.trackingEnabled = window.rentoomCookieInterop.trackingEnabled || false;
window.rentoomCookieInterop.sessionRecordingEnabled = window.rentoomCookieInterop.sessionRecordingEnabled || false;
window.rentoomCookieInterop.trackingConfigured = window.rentoomCookieInterop.trackingConfigured || false;
window.rentoomCookieInterop.pendingEvents = window.rentoomCookieInterop.pendingEvents || [];
Object.assign(window.rentoomCookieInterop, {
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
            window.rentoomCookieInterop.trackingConfigured = true;
            window.rentoomAnalytics.flushPendingEvents();
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
});

window.rentoomAnalytics = window.rentoomAnalytics || {};
Object.assign(window.rentoomAnalytics, {
    trackEvent: function (eventName, parameters, dedupeKey) {
        if (!eventName) {
            return "ignored";
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

        if (dedupeKey && this.isEventDeduped(dedupeKey)) {
            return "duplicate";
        }

        if (!window.rentoomCookieInterop.trackingConfigured || typeof window.gtag !== "function") {
            this.queuePendingEvent(eventName, normalizedParameters, dedupeKey);
            return "queued";
        }

        window.gtag("event", eventName, normalizedParameters);
        this.markEventDeduped(dedupeKey);
        return "sent";
    },

    flushPendingEvents: function () {
        if (!window.rentoomCookieInterop?.trackingConfigured || typeof window.gtag !== "function") {
            return;
        }

        const pendingEvents = window.rentoomCookieInterop.pendingEvents.splice(0);
        pendingEvents.forEach(entry => {
            if (!entry?.eventName) {
                return;
            }

            if (entry.dedupeKey && window.rentoomAnalytics.isEventDeduped(entry.dedupeKey)) {
                return;
            }

            window.gtag("event", entry.eventName, entry.parameters || {});
            window.rentoomAnalytics.markEventDeduped(entry.dedupeKey);
        });
    },

    queuePendingEvent: function (eventName, parameters, dedupeKey) {
        if (dedupeKey) {
            const alreadyQueued = window.rentoomCookieInterop.pendingEvents.some(entry => entry?.dedupeKey === dedupeKey);
            if (alreadyQueued) {
                return;
            }
        }

        window.rentoomCookieInterop.pendingEvents.push({
            eventName: eventName,
            parameters: parameters,
            dedupeKey: dedupeKey || null
        });
    },

    isEventDeduped: function (dedupeKey) {
        if (!dedupeKey) {
            return false;
        }

        try {
            return window.sessionStorage?.getItem(dedupeKey) === "1";
        } catch {
            return false;
        }
    },

    markEventDeduped: function (dedupeKey) {
        if (!dedupeKey) {
            return;
        }

        try {
            window.sessionStorage?.setItem(dedupeKey, "1");
        } catch {
            // Analytics storage failures must not break the reservation flow.
        }
    }
});
