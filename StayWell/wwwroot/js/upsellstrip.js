window.upsellStrip = {
    _instances: new Map(),
    _resizeListeners: new Map(),

    getWindowWidth() {
        return window.innerWidth;
    },

    onWindowResize(dotnet) {
        const handler = () => {
            dotnet.invokeMethodAsync("OnWindowResize", window.innerWidth).catch(() => {
                this.offWindowResize(dotnet);
            });
        };

        window.addEventListener("resize", handler, { passive: true });
        this._resizeListeners.set(dotnet, handler);
    },

    offWindowResize(dotnet) {
        const handler = this._resizeListeners.get(dotnet);
        if (handler) {
            window.removeEventListener("resize", handler);
            this._resizeListeners.delete(dotnet);
        }
    },

    init(scrollEl, dotnet) {
        if (!(scrollEl instanceof Element)) {
            console.warn("upsellStrip.init: scrollEl must be a valid DOM Element");
            return;
        }

        this.destroy(scrollEl);

        let ticking = false;
        let rafId = 0;
        let isDragging = false;
        let startX = 0;
        let startScrollLeft = 0;
        let carouselActive = false;
        let disposed = false;

        const isCarouselMode = () => scrollEl.scrollWidth > scrollEl.clientWidth + 4;

        const safeInvoke = (method, ...args) => {
            if (disposed) return;
            dotnet.invokeMethodAsync(method, ...args).catch(() => {
                disposed = true;
                this.destroy(scrollEl);
            });
        };

        const findActiveId = () => {
            if (disposed || !isCarouselMode()) return;

            const items = scrollEl.querySelectorAll(".upsell-strip-item[id]");
            if (!items.length) return;

            const scrollCenter = scrollEl.scrollLeft + scrollEl.offsetWidth / 2;
            let closestId = null;
            let closestDist = Infinity;

            items.forEach((item) => {
                const itemCenter = item.offsetLeft + item.offsetWidth / 2;
                const dist = Math.abs(scrollCenter - itemCenter);
                if (dist < closestDist) {
                    closestDist = dist;
                    closestId = parseInt(item.id.replace("strip-item-", ""), 10);
                }
            });

            if (closestId !== null) {
                safeInvoke("SetActiveItem", closestId);
            }
        };

        const onScroll = () => {
            if (disposed || !isCarouselMode() || ticking) return;
            ticking = true;

            rafId = requestAnimationFrame(() => {
                if (disposed) {
                    ticking = false;
                    return;
                }

                findActiveId();
                ticking = false;
            });
        };

        const onMouseDown = (e) => {
            if (disposed || !isCarouselMode()) return;
            isDragging = true;
            startX = e.pageX - scrollEl.offsetLeft;
            startScrollLeft = scrollEl.scrollLeft;
            scrollEl.classList.add("is-dragging");
        };

        const onMouseMove = (e) => {
            if (disposed || !isDragging || !isCarouselMode()) return;
            e.preventDefault();
            const x = e.pageX - scrollEl.offsetLeft;
            const walk = (x - startX) * 1.2;
            scrollEl.scrollLeft = startScrollLeft - walk;
        };

        const onMouseUp = () => {
            if (!isDragging) return;
            isDragging = false;
            scrollEl.classList.remove("is-dragging");
        };

        const onResize = () => {
            if (disposed) return;

            const nowCarousel = isCarouselMode();
            if (nowCarousel !== carouselActive) {
                carouselActive = nowCarousel;
                if (nowCarousel) {
                    findActiveId();
                }
            }
        };

        const resizeObserver = new ResizeObserver(onResize);
        resizeObserver.observe(scrollEl);

        scrollEl.addEventListener("scroll", onScroll, { passive: true });
        scrollEl.addEventListener("mousedown", onMouseDown);
        window.addEventListener("mousemove", onMouseMove);
        window.addEventListener("mouseup", onMouseUp);

        carouselActive = isCarouselMode();
        findActiveId();

        this._instances.set(scrollEl, {
            onScroll,
            onMouseDown,
            onMouseMove,
            onMouseUp,
            resizeObserver,
            dispose() {
                disposed = true;
                if (rafId) {
                    cancelAnimationFrame(rafId);
                    rafId = 0;
                }
            },
        });
    },

    scrollToNext(scrollEl) {
        if (!scrollEl || scrollEl.scrollWidth <= scrollEl.clientWidth + 4) return;
        const itemWidth = scrollEl.querySelector(".upsell-strip-item")?.offsetWidth ?? 260;
        scrollEl.scrollBy({ left: itemWidth + 12, behavior: "smooth" });
    },

    scrollToPrev(scrollEl) {
        if (!scrollEl || scrollEl.scrollWidth <= scrollEl.clientWidth + 4) return;
        const itemWidth = scrollEl.querySelector(".upsell-strip-item")?.offsetWidth ?? 260;
        scrollEl.scrollBy({ left: -(itemWidth + 12), behavior: "smooth" });
    },

    destroy(scrollEl) {
        const instance = this._instances.get(scrollEl);
        if (instance) {
            instance.dispose?.();
            scrollEl.removeEventListener("scroll", instance.onScroll);
            scrollEl.removeEventListener("mousedown", instance.onMouseDown);
            window.removeEventListener("mousemove", instance.onMouseMove);
            window.removeEventListener("mouseup", instance.onMouseUp);
            instance.resizeObserver.disconnect();
            this._instances.delete(scrollEl);
        }
    },
};