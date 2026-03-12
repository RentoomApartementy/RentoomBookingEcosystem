window.pwaInstall = (() => {
    let deferredPrompt = null;
    let dotNetRef = null;

    window.addEventListener("beforeinstallprompt", (e) => {
        e.preventDefault();
        deferredPrompt = e;
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync("SetInstallPromptAvailable", true);
        }
    });

    window.addEventListener("appinstalled", () => {
        deferredPrompt = null;
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync("SetInstallPromptAvailable", false);
        }
    });

    async function init(ref) {
        dotNetRef = ref;
        if (deferredPrompt && dotNetRef) {
            await dotNetRef.invokeMethodAsync("SetInstallPromptAvailable", true);
        }
    }

    function canInstall() {
        return !!deferredPrompt;
    }

    async function promptInstall() {
        if (!deferredPrompt) {
            return false;
        }

        deferredPrompt.prompt();
        const choice = await deferredPrompt.userChoice;
        deferredPrompt = null;

        if (dotNetRef) {
            await dotNetRef.invokeMethodAsync("SetInstallPromptAvailable", false);
        }

        return choice && choice.outcome === "accepted";
    }

    function isStandalone() {
        return window.matchMedia("(display-mode: standalone)").matches || window.navigator.standalone === true;
    }

    return {
        init,
        canInstall,
        promptInstall,
        isStandalone
    };
})();
