let scrollHandler = null;
let isDisposed = false;

export function init(dotNetHelper) {
    if (!dotNetHelper || scrollHandler) return;

    isDisposed = false;
    scrollHandler = () => {
        if (isDisposed || !dotNetHelper) return;

        const scrollTop = window.scrollY || document.documentElement.scrollTop;
        dotNetHelper.invokeMethodAsync('OnScrollChanged', scrollTop).catch(() => {
            // Ignore: this usually means Blazor circuit is disconnected or component was disposed.
        });
    };

    window.addEventListener('scroll', scrollHandler);
}

export function dispose() {
    isDisposed = true;

    if (scrollHandler) {
        window.removeEventListener('scroll', scrollHandler);
        scrollHandler = null;
    }
}
