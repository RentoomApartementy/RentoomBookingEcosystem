let dotNetHelper = null;
let observer = null;

export function init(helper) {
    dotNetHelper = helper;

    const target = document.querySelector('#scroll-anchor');

    if (!target) {
        console.warn("Scroll anchor element not found!");
        return;
    }

    const options = {
        root: null,         
        rootMargin: '600px', 
        threshold: 0.1       
    };

    observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting && dotNetHelper) {
                dotNetHelper.invokeMethodAsync('LoadMoreOnScroll');
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