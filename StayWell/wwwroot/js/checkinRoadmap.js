(function () {
    function getScrollContainer() {
        return document.getElementById("main-body") || document.scrollingElement || document.documentElement;
    }

    function getViewportMid(container) {
        if (container === document.scrollingElement || container === document.documentElement) {
            return window.innerHeight / 2;
        }
        var rect = container.getBoundingClientRect();
        return rect.top + rect.height / 2;
    }

    function update(state) {
        var steps = Array.prototype.slice.call(document.querySelectorAll("[data-checkin-step]"));
        if (steps.length === 0) return;

        var container = state.container;
        var midY = getViewportMid(container);

        var activeIndex = -1;
        var closestDist = Infinity;

        steps.forEach(function (step, i) {
            var rect = step.getBoundingClientRect();
            var stepMid = rect.top + rect.height / 2;
            var dist = Math.abs(stepMid - midY);

            if (dist < closestDist) {
                closestDist = dist;
                activeIndex = i;
            }
        });

        var scrollTop, scrollHeight, clientHeight;
        if (container === document.scrollingElement || container === document.documentElement) {
            scrollTop = window.scrollY || document.documentElement.scrollTop;
            scrollHeight = document.documentElement.scrollHeight;
            clientHeight = window.innerHeight;
        } else {
            scrollTop = container.scrollTop;
            scrollHeight = container.scrollHeight;
            clientHeight = container.clientHeight;
        }

        var nearBottom = (scrollTop + clientHeight) >= (scrollHeight - 24);
        if (nearBottom) {
            activeIndex = steps.length - 1;
        }

        steps.forEach(function (step, i) {
            var isActive = i === activeIndex;
            var isCompleted = i < activeIndex;

            step.classList.toggle("checkin-step--active", isActive);
            step.classList.toggle("checkin-step--completed", isCompleted);

            var connector = step.querySelector(".checkin-step__connector");
            if (!connector) return;

            var connectorRect = connector.getBoundingClientRect();
            var progress;

            if (i < activeIndex) {
                progress = 100;
            } else if (i > activeIndex) {
                progress = 0;
            } else {
                if (connectorRect.height <= 0) {
                    progress = 0;
                } else {
                    var raw = (midY - connectorRect.top) / connectorRect.height;
                    progress = Math.max(0, Math.min(1, raw)) * 100;
                }
            }

            connector.style.setProperty("--checkin-progress", progress + "%");
        });
    }

    window.setupCheckInRoadmap = function () {
        if (window._checkinRoadmapState) {
            window.teardownCheckInRoadmap();
        }

        var container = getScrollContainer();
        var state = { container: container };

        var rafPending = false;
        var schedule = function () {
            if (rafPending) return;
            rafPending = true;
            requestAnimationFrame(function () {
                rafPending = false;
                update(state);
            });
        };

        state.handler = schedule;

        container.addEventListener("scroll", schedule, { passive: true });
        window.addEventListener("resize", schedule);

        window._checkinRoadmapState = state;

        // Initial paint after layout settles.
        setTimeout(schedule, 50);
        setTimeout(schedule, 250);
    };

    window.teardownCheckInRoadmap = function () {
        var state = window._checkinRoadmapState;
        if (!state) return;

        if (state.container && state.handler) {
            state.container.removeEventListener("scroll", state.handler);
        }
        if (state.handler) {
            window.removeEventListener("resize", state.handler);
        }

        delete window._checkinRoadmapState;
    };
})();
