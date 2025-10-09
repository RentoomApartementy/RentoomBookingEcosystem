let dotNetHelper = null;
let timeoutId = null;

function isNearBottom() {
    const buffer = 150;
    return (window.innerHeight + window.scrollY) >= document.documentElement.scrollHeight - buffer;
}

function handleScroll() {
    if (!dotNetHelper) return;

    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => {
        if (isNearBottom()) {
            dotNetHelper.invokeMethodAsync('LoadMoreOnScroll');
        }
    }, 200);
}

export function init(helper) {
    dotNetHelper = helper;
    window.addEventListener('scroll', handleScroll, { passive: true });
}

export function unregister() {
    window.removeEventListener('scroll', handleScroll);
    dotNetHelper = null;
}
