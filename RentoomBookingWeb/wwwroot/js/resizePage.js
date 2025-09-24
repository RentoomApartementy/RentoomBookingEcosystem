window.getWindowWidth = () => window.innerWidth;

window.registerResizeHandler = (dotNetHelper) => {
    window.addEventListener('resize', () => {
        dotNetHelper.invokeMethodAsync('UpdateItemsPerPageAsync');
    });
};