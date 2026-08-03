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

export function scrollStartDayNearTop(daySelector, containerSelector) {
    const day = document.querySelector(daySelector);
    const container = document.querySelector(containerSelector);
    if (!day || !container) {
        return;
    }
    const monthEl = day.closest('.abw-month');
    const titleEl = monthEl ? monthEl.querySelector('.abw-month-title') : null;
    const headerHeight = titleEl ? titleEl.getBoundingClientRect().height : 0;
    const dayTopInContainer = day.getBoundingClientRect().top - container.getBoundingClientRect().top + container.scrollTop;
    container.scrollTop = Math.max(0, dayTopInContainer - headerHeight);
}

// Scrolls just enough that panelId's bottom edge lands right above barId's top edge
// (e.g. a fixed mobile bottom bar). Never scrolls up if the panel is already fully
// visible above the bar — only pushes it into view when needed. barId is optional;
// if it doesn't exist or is display:none (e.g. desktop), its height is treated as 0.
//
// StateHasChanged() only queues a Blazor render — it doesn't guarantee the DOM has
// been patched by the time this JS call runs (especially right after the panel was
// removed by clearing the selection and now needs to be re-created). If the element
// isn't there yet, wait for it via MutationObserver instead of giving up immediately,
// with a bounded timeout in case the selection gets cleared again before it appears.
export function scrollElementAboveBar(panelId, barId, timeoutMs = 2000) {
    const panel = document.getElementById(panelId);
    if (panel) {
        performScrollAboveBar(panel, barId);
        return;
    }

    const observer = new MutationObserver(() => {
        const target = document.getElementById(panelId);
        if (target) {
            observer.disconnect();
            clearTimeout(timeoutHandle);
            performScrollAboveBar(target, barId);
        }
    });
    observer.observe(document.body, { childList: true, subtree: true });

    const timeoutHandle = setTimeout(() => {
        observer.disconnect();
        console.warn(`[Scroll] Nie można przewinąć - brak elementu: ${panelId}`);
    }, timeoutMs);
}

function performScrollAboveBar(panel, barId) {
    const bar = barId ? document.getElementById(barId) : null;
    const barHeight = bar ? bar.getBoundingClientRect().height : 0;
    const rect = panel.getBoundingClientRect();
    const desiredBottom = window.innerHeight - barHeight;
    const delta = rect.bottom - desiredBottom;
    if (delta > 0) {
        window.scrollTo({ top: window.pageYOffset + delta, behavior: 'smooth' });
    }
}

// Backward compatibility for any remaining global JS interop call sites.
window.registerScrollObserver = registerScrollObserver;
window.scrollToElement = scrollToElement;
window.scrollToTop = scrollToTop;
window.scrollStartDayNearTop = scrollStartDayNearTop;
window.scrollElementAboveBar = scrollElementAboveBar;
