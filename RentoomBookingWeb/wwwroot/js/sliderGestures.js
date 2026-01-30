export function initSlider(dotNetHelper, element) {
    function checkWidth() {
        dotNetHelper.invokeMethodAsync('UpdateItemPerPageAsync', window.innerWidth);
    }

    window.addEventListener('resize', checkWidth);
    checkWidth(); 

    let touchStartX = 0;
    let touchEndX = 0;
    const minSwipeDistance = 50; 

    element.addEventListener('touchstart', (e) => {
        touchStartX = e.changedTouches[0].screenX;
    }, { passive: true });

    element.addEventListener('touchend', (e) => {
        touchEndX = e.changedTouches[0].screenX;
        handleGesture();
    }, { passive: true });

    function handleGesture() {
        const distance = touchStartX - touchEndX;

        if (distance > minSwipeDistance) {
            dotNetHelper.invokeMethodAsync('NextSlide');
        }
        else if (distance < -minSwipeDistance) {
            dotNetHelper.invokeMethodAsync('PrevSlide');
        }
    }
}