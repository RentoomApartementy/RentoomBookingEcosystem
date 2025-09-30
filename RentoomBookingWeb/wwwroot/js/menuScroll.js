export function init(dotNetHelper) {
    window.addEventListener('scroll', () => {
        const scrollTop = window.scrollY || document.documentElement.scrollTop;
        dotNetHelper.invokeMethodAsync('OnScrollChanged', scrollTop);
    });
}