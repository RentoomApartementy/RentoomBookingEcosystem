export function registerScrollObserver(elementId, dotnetHelper, options) {
    const target = document.getElementById(elementId);
    
    const observerOptions = options || {
        threshold: 0,
        rootMargin: "-120px 0px 0px 0px"
    };

    if (target) {
        startIntersectionObserver(target, elementId, dotnetHelper, observerOptions);
    } else {
        const observer = new MutationObserver((mutations, obs) => {
            const targetNow = document.getElementById(elementId);
            if (targetNow) {
                startIntersectionObserver(targetNow, elementId, dotnetHelper, observerOptions);
                obs.disconnect();
            }
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }
}

function startIntersectionObserver(target, elementId, dotnetHelper, options) {
    const observer = new IntersectionObserver((entries) => {
        const entry = entries[0];
        
        // Element jest uznawany za "widoczny lub minięty" jeśli:
        // 1. Przecina się z obszarem roboczym (jest widoczny)
        // 2. Jego górna krawędź jest powyżej górnej krawędzi obszaru roboczego (minęliśmy go)
        const rootTop = entry.rootBounds ? entry.rootBounds.top : 0;
        const isPastOrVisible = entry.isIntersecting || entry.boundingClientRect.top < rootTop;
        
        dotnetHelper.invokeMethodAsync('UpdateScrollState', elementId, isPastOrVisible);
    }, options);

    observer.observe(target);
}

export function scrollToElement(elementId, offsetPx = 0) {
    const element = document.getElementById(elementId);
    if (element) {
        const offset = Number(offsetPx) || 0;
        const y = element.getBoundingClientRect().top + window.pageYOffset - offset;
        window.scrollTo({ top: Math.max(0, y), behavior: 'smooth' });
    } else {
        console.warn(`[Scroll] Nie można przewinąć - brak elementu: ${elementId}`);
    }
}

export function scrollToTop(behavior = 'auto') {
    window.scrollTo({ top: 0, left: 0, behavior });
    document.documentElement.scrollTop = 0;
    document.body.scrollTop = 0;
}

// Backward compatibility for any remaining global JS interop call sites.
window.registerScrollObserver = registerScrollObserver;
window.scrollToElement = scrollToElement;
window.scrollToTop = scrollToTop;
