const observers = new WeakMap();

function hasOverflow(element) {
    return element.scrollHeight > element.clientHeight + 1;
}

export function measureOverflow(element) {
    const entry = observers.get(element);
    if (!entry) return;

    entry.dotNetReference
        .invokeMethodAsync("SetReviewOverflow", hasOverflow(element))
        .catch(() => disconnectOverflow(element));
}

export function observeOverflow(element, dotNetReference) {
    disconnectOverflow(element);

    const observer = new ResizeObserver(() => measureOverflow(element));
    observers.set(element, { observer, dotNetReference });
    observer.observe(element);
    requestAnimationFrame(() => measureOverflow(element));
}

export function disconnectOverflow(element) {
    const entry = observers.get(element);
    if (!entry) return;

    entry.observer.disconnect();
    observers.delete(element);
}
