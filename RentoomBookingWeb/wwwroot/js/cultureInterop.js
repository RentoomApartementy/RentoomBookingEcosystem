window.rentoomCultureInterop = window.rentoomCultureInterop || {};
Object.assign(window.rentoomCultureInterop, {
    setHtmlLang: function (twoLetterLangCode) {
        if (!twoLetterLangCode) {
            return;
        }

        if (document.documentElement.lang !== twoLetterLangCode) {
            document.documentElement.lang = twoLetterLangCode;
        }
    }
});
