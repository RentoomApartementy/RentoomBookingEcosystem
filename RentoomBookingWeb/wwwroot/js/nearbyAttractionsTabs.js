// Scrolls the currently active category tab into view within the horizontally
// scrollable tab strip. Used after a swipe on the attractions list switches tabs.
export function scrollActiveTabIntoView(container) {
    if (!container) return;

    const active = container.querySelector('.nearby__tab.is-active');
    if (active) {
        active.scrollIntoView({ behavior: 'smooth', inline: 'center', block: 'nearest' });
    }
}
