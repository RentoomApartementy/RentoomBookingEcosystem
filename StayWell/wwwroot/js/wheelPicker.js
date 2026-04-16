window.WheelPicker = {
    init: function (elementId, initialIndex) {
        const el = document.getElementById(elementId);
        if (!el) return;
        const itemHeight = 48;
        el.scrollTop = initialIndex * itemHeight;
    },
    getSelectedIndex: function (elementId) {
        const el = document.getElementById(elementId);
        if (!el) return 0;
        const itemHeight = 48;
        return Math.round(el.scrollTop / itemHeight);
    }
};
