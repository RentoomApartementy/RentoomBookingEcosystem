let dotNetHelper = null;
let scrollEl = null;
let throttled = false;
const THRESHOLD_PX = 300;
const THROTTLE_MS = 400;

function handleScroll() {
    if (throttled || !scrollEl || !dotNetHelper) return;
    const distanceToBottom = scrollEl.scrollHeight - (scrollEl.scrollTop + scrollEl.clientHeight);
    if (distanceToBottom <= THRESHOLD_PX) {
        throttled = true;
        dotNetHelper.invokeMethodAsync('LoadMoreMonthsOnScroll');
        setTimeout(() => { throttled = false; }, THROTTLE_MS);
    }
}

export function init(helper, monthsElement) {
    dotNetHelper = helper;
    scrollEl = monthsElement;
    if (!scrollEl) return;
    scrollEl.addEventListener('scroll', handleScroll, { passive: true });
}

export function unregister() {
    if (scrollEl) scrollEl.removeEventListener('scroll', handleScroll);
    dotNetHelper = null;
    scrollEl = null;
}
