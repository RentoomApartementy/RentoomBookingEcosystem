export function initSlider(dotNetHelper, sliderElement) {
    if (!sliderElement || !dotNetHelper) return;

    let isScrolling;
    const scrollHandler = () => {
        window.clearTimeout(isScrolling);
        isScrolling = setTimeout(() => {
            if (dotNetHelper) {
                const index = Math.round(sliderElement.scrollLeft / sliderElement.clientWidth);
                dotNetHelper.invokeMethodAsync('OnImageSwiped', index)
                    .catch(err => {
                        // Ignoruj błędy jeśli komponent został już usunięty
                        if (err && err.message && !err.message.includes("disposed")) {
                            console.error(err);
                        }
                    });
            }
        }, 100);
    };

    sliderElement.addEventListener('scroll', scrollHandler, { passive: true });
    
    // Zwracamy funkcję do odpięcia zdarzenia (opcjonalnie, dla czystości)
    return scrollHandler;
}

export function scrollToImage(sliderElement, index) {
    if (!sliderElement) return;
    sliderElement.scrollTo({
        left: sliderElement.clientWidth * index,
        behavior: 'smooth'
    });
}
