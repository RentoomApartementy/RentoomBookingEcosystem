let dotNetHelper = null;
let observer = null;

export function init(helper) {
    dotNetHelper = helper;
    setupObserver();
}

function setupObserver() {
    const target = document.querySelector('#scroll-anchor');

    if (!target) {
        console.warn("Scroll anchor element not found! Retrying in 100ms...");
        setTimeout(setupObserver, 100);
        return;
    }

    if (observer) {
        observer.disconnect();
    }

    const options = {
        root: null,         
        rootMargin: '400px', 
        threshold: 0.1       
    };

    observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting && dotNetHelper) {
                // Zapobieganie wywołaniom jeśli helper nie jest gotowy
                try {
                    dotNetHelper.invokeMethodAsync('LoadMoreOnScroll');
                } catch (e) {
                    console.error("Failed to invoke LoadMoreOnScroll", e);
                }
            }
        });
    }, options);

    observer.observe(target);
}

export function unregister() {
    if (observer) {
        observer.disconnect();
        observer = null;
    }
    dotNetHelper = null;
}