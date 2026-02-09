class SliderManager {
    constructor(dotNetHelper, element) {
        this.dotNetHelper = dotNetHelper;
        this.element = element;
        this.resizeTimer = null;

        this.resizeHandler = this.onResize.bind(this);
        this.touchStartHandler = this.onTouchStart.bind(this);
        this.touchEndHandler = this.onTouchEnd.bind(this);

        this.touchStartX = 0;

        window.addEventListener('resize', this.resizeHandler);

        if (this.element) {
            this.element.addEventListener('touchstart', this.touchStartHandler, { passive: true });
            this.element.addEventListener('touchend', this.touchEndHandler, { passive: true });
        }

        this.onResize();
    }

    onResize() {
        if (this.resizeTimer) clearTimeout(this.resizeTimer);

        this.resizeTimer = setTimeout(() => {
            if (this.dotNetHelper) {
                this.dotNetHelper.invokeMethodAsync('UpdateItemPerPageAsync', window.innerWidth)
                    .catch(e => console.debug("Ignored Blazor call", e));
            }
        }, 100);
    }

    onTouchStart(e) {
        this.touchStartX = e.changedTouches[0].screenX;
    }

    onTouchEnd(e) {
        if (!this.dotNetHelper) return;

        const touchEndX = e.changedTouches[0].screenX;
        const distance = this.touchStartX - touchEndX;
        const minSwipeDistance = 50;

        if (distance > minSwipeDistance) {
            this.dotNetHelper.invokeMethodAsync('NextSlide').catch(() => {});
        } else if (distance < -minSwipeDistance) {
            this.dotNetHelper.invokeMethodAsync('PrevSlide').catch(() => {});
        }
    }

    dispose() {
        if (this.resizeTimer) {
            clearTimeout(this.resizeTimer);
            this.resizeTimer = null;
        }

        window.removeEventListener('resize', this.resizeHandler);

        if (this.element) {
            this.element.removeEventListener('touchstart', this.touchStartHandler);
            this.element.removeEventListener('touchend', this.touchEndHandler);
        }

        this.dotNetHelper = null;
        this.element = null;
    }
}

export function initSlider(dotNetHelper, element) {
    return new SliderManager(dotNetHelper, element);
}