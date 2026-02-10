const observers = new WeakMap();

export function observeElement(dotNetHelper, element) {
    if (!element) return;

    // Zabezpieczenie: jeśli element już jest obserwowany, odepnij stary observer
    if (observers.has(element)) {
        observers.get(element).disconnect();
        observers.delete(element);
    }

    const observer = new IntersectionObserver(entries => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                dotNetHelper.invokeMethodAsync('LoadMedia')
                    .catch(err => {
                        if (err && err.message && !err.message.includes("disposed")) {
                            console.error(err);
                        }
                    });

                observer.unobserve(entry.target);
                observer.disconnect();
                observers.delete(entry.target);
            }
        });
    }, { threshold: 0.1 });

    observer.observe(element);
    observers.set(element, observer);
}

export function unobserveElement(element) {
    if (element && observers.has(element)) {
        observers.get(element).disconnect();
        observers.delete(element);
    }
}