export function initSwipe(dotNetHelper, element) {
    let startX = 0;
    let startY = 0;
    const minSwipeDistance = 50;

    function onTouchStart(e) {
        startX = e.changedTouches[0].screenX;
        startY = e.changedTouches[0].screenY;
    }

    function onTouchEnd(e) {
        const endX = e.changedTouches[0].screenX;
        const endY = e.changedTouches[0].screenY;
        const distanceX = startX - endX;
        const distanceY = startY - endY;

        // Ignore mostly-vertical swipes so the modal can still be scrolled/dismissed normally.
        if (Math.abs(distanceX) < Math.abs(distanceY)) return;

        if (distanceX > minSwipeDistance) {
            dotNetHelper.invokeMethodAsync('SwipeNext').catch(() => {});
        } else if (distanceX < -minSwipeDistance) {
            dotNetHelper.invokeMethodAsync('SwipePrev').catch(() => {});
        }
    }

    element.addEventListener('touchstart', onTouchStart, { passive: true });
    element.addEventListener('touchend', onTouchEnd, { passive: true });

    return {
        dispose() {
            element.removeEventListener('touchstart', onTouchStart);
            element.removeEventListener('touchend', onTouchEnd);
        }
    };
}
