let dotNetHelper = null;
let scrollEl = null;
let throttled = false;
const THRESHOLD_PX = 300;
const THROTTLE_MS = 400;

let previewEls = [];
let previewEnd = null;

function handleScroll() {
    if (throttled || !scrollEl || !dotNetHelper) return;
    const distanceToBottom = scrollEl.scrollHeight - (scrollEl.scrollTop + scrollEl.clientHeight);
    if (distanceToBottom <= THRESHOLD_PX) {
        throttled = true;
        dotNetHelper.invokeMethodAsync('LoadMoreMonthsOnScroll');
        setTimeout(() => { throttled = false; }, THROTTLE_MS);
    }
}

function clearPreview() {
    for (const el of previewEls) {
        el.classList.remove('abw-day-preview', 'abw-day-preview-end');
    }
    previewEls = [];
    previewEnd = null;
}

function showPreview(day) {
    const start = scrollEl.dataset.selStart;
    const end = day.dataset.date;
    if (!start || end <= start) {
        clearPreview();
        return;
    }
    if (end === previewEnd) return;

    clearPreview();
    for (const el of scrollEl.querySelectorAll('.abw-day[data-date]')) {
        const date = el.dataset.date;
        if (date <= start || date > end) continue;
        el.classList.add(date === end ? 'abw-day-preview-end' : 'abw-day-preview');
        previewEls.push(el);
    }
    previewEnd = end;
}

function handlePointerOver(e) {
    if (!scrollEl || !scrollEl.classList.contains('abw-awaiting-end')) {
        clearPreview();
        return;
    }
    const day = e.target.closest?.('.abw-day[data-date]:not(:disabled)');
    if (day) {
        showPreview(day);
    } else {
        clearPreview();
    }
}

export function init(helper, monthsElement) {
    dotNetHelper = helper;
    scrollEl = monthsElement;
    if (!scrollEl) return;
    scrollEl.addEventListener('scroll', handleScroll, { passive: true });
    scrollEl.addEventListener('mouseover', handlePointerOver);
    scrollEl.addEventListener('focusin', handlePointerOver);
    scrollEl.addEventListener('mouseleave', clearPreview);
    scrollEl.addEventListener('click', clearPreview, true);
}

export function unregister() {
    if (scrollEl) {
        scrollEl.removeEventListener('scroll', handleScroll);
        scrollEl.removeEventListener('mouseover', handlePointerOver);
        scrollEl.removeEventListener('focusin', handlePointerOver);
        scrollEl.removeEventListener('mouseleave', clearPreview);
        scrollEl.removeEventListener('click', clearPreview, true);
    }
    clearPreview();
    dotNetHelper = null;
    scrollEl = null;
}
