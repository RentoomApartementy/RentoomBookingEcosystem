window.upsellStrip = {
    _instances: new Map(),

    init(scrollEl, dotnet) {
        if (!scrollEl) return;

        this.destroy(scrollEl);

        let ticking = false;

        const findActiveId = () => {
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
                    closestId = parseInt(
                        item.id.replace("strip-item-", ""),
                        10
                    );
                }
            });

            if (closestId !== null) {
                dotnet.invokeMethodAsync("SetActiveItem", closestId);
            }
        };

        const onScroll = () => {
            if (ticking) return;
            ticking = true;
            requestAnimationFrame(() => {
                findActiveId();
                ticking = false;
            });
        };

        scrollEl.addEventListener("scroll", onScroll, { passive: true });

        findActiveId();

        this._instances.set(scrollEl, { onScroll, dotnet });
    },

    destroy(scrollEl) {
        const instance = this._instances.get(scrollEl);
        if (instance) {
            scrollEl.removeEventListener("scroll", instance.onScroll);
            this._instances.delete(scrollEl);
        }
    },
};