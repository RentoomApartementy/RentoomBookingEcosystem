window.buttonHelper = {
    createRipple: function (event, element) {
        const rect = element.getBoundingClientRect();
        const x = event.clientX - rect.left;
        const y = event.clientY - rect.top;

        element.style.setProperty('--ripple-x', x + 'px');
        element.style.setProperty('--ripple-y', y + 'px');
        
        // Force a reflow to restart animation if needed
        element.classList.remove('ripple-active');
        void element.offsetWidth;
        element.classList.add('ripple-active');
    }
};