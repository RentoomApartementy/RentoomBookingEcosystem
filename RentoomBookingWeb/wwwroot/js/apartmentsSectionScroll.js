let dotNetHelper = null;
let scrollEl = null;
let throttled = false;

const THRESHOLD_PX = 600;
const THROTTLE_MS = 200;

function handleScroll() {
    if (throttled || !scrollEl || !dotNetHelper) return;

    const distanceToEnd = scrollEl.scrollWidth - (scrollEl.scrollLeft + scrollEl.clientWidth);

    if (distanceToEnd <= THRESHOLD_PX) {
        throttled = true;
        try {
            dotNetHelper.invokeMethodAsync('LoadMoreOnScroll');
        } catch (e) {
            console.error("Failed to invoke LoadMoreOnScroll", e);
        }
        setTimeout(() => { throttled = false; }, THROTTLE_MS);
    }
}

export function init(helper, wrapperElement) {
    dotNetHelper = helper;

    const target = wrapperElement?.querySelector?.('.carousel-list-wrapper');

    if (!target) {
        console.warn("Apartments section carousel scroll container not found.");
        return;
    }

    scrollEl = target;
    scrollEl.addEventListener('scroll', handleScroll, { passive: true });

    // In case the first page already fits without overflow, check once after layout.
    setTimeout(handleScroll, 300);
}

export function unregister() {
    if (scrollEl) {
        scrollEl.removeEventListener('scroll', handleScroll);
        scrollEl = null;
    }
    dotNetHelper = null;
    throttled = false;
}
