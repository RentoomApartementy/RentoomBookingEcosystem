window.registerScrollObserver = (elementId, dotnetHelper) => {
    // 1. Sprawdzamy, czy element już istnieje
    const target = document.getElementById(elementId);

    if (target) {
        // Jeśli jest od razu - super, podpinamy się
        startIntersectionObserver(target, elementId, dotnetHelper);
    } else {
        // 2. Jeśli go nie ma (bo się ładuje), uruchamiamy "czujkę" (MutationObserver)
        // która patrzy na zmiany w HTML i czeka aż element się pojawi
        const observer = new MutationObserver((mutations, obs) => {
            const targetNow = document.getElementById(elementId);
            if (targetNow) {
                // Element się pojawił! Podpinamy się i wyłączamy czujkę
                startIntersectionObserver(targetNow, elementId, dotnetHelper);
                obs.disconnect(); // Przestań szukać, już mamy
            }
        });

        // Obserwuj całe body w poszukiwaniu nowych elementów
        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }
};

// Funkcja pomocnicza - właściwa logika scrolla
function startIntersectionObserver(target, elementId, dotnetHelper) {
    const observer = new IntersectionObserver((entries) => {
        const entry = entries[0];
        const isPastOrVisible = entry.isIntersecting || entry.boundingClientRect.top < 0;
        dotnetHelper.invokeMethodAsync('UpdateScrollState', elementId, isPastOrVisible);
    }, {
        threshold: 0,
        rootMargin: "-120px 0px 0px 0px"
    });

    observer.observe(target);
}

// Funkcja do scrollowania (też zabezpieczona, żeby nie sypała błędami)
window.scrollToElement = (elementId) => {
    const element = document.getElementById(elementId);
    if (element) {
        element.scrollIntoView({ behavior: 'smooth', block: 'start' });
    } else {
        console.warn(`[Scroll] Nie można przewinąć - brak elementu: ${elementId}`);
    }
};