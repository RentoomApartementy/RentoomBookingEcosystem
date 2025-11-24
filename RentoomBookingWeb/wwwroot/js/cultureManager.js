export function setCultureCookieAndReload(cookieName) {
    const url = new URL(window.location.href);
    const culture = url.searchParams.get('culture');
    const uiCulture = url.searchParams.get('ui-culture');

    if (culture && uiCulture) {
        const cookieValue = `c=${culture}|uic=${uiCulture}`;

        document.cookie = `${cookieName}=${cookieValue}; path=/; max-age=${365 * 24 * 60 * 60}`;

        url.searchParams.delete('culture');
        url.searchParams.delete('ui-culture');

        window.location.href = url.toString();
    }
}