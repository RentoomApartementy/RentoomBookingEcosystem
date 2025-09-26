export function init(dotNetHelper) {
    const updateItemsPerPage = () => {
        const width = window.innerWidth;
        dotNetHelper.invokeMethodAsync('UpdateItemPerPageAsync', width);
    };

    updateItemsPerPage();

    window.addEventListener('resize', updateItemsPerPage);
}
