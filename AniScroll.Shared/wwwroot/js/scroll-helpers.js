window.getViewportHeight = function () {
    const scrollSection = document.querySelector('.main-scroll');
    if (scrollSection) return scrollSection.clientHeight;
    return window.innerHeight;
};

window.getScrollSectionHeight = function () {
    const scrollSection = document.querySelector('.main-scroll');
    if (scrollSection) return scrollSection.clientHeight;
    return window.innerHeight;
};

window.getScrollInfo = function (element) {
    if (!element) return { scrollTop: 0, scrollHeight: 0, clientHeight: 0 };
    return {
        scrollTop: element.scrollTop,
        scrollHeight: element.scrollHeight,
        clientHeight: element.clientHeight
    };
};

window.setScrollTop = function (element, value) {
    if (element) element.scrollTop = value;
};

window.isTouchingDescriptionZone = function (clientX, clientY) {
    const elements = document.elementsFromPoint(clientX, clientY);
    for (let element of elements) {
        if (element.classList.contains('description-scroll-zone') ||
            element.classList.contains('description-full') ||
            element.classList.contains('description-scroll-wrapper') ||
            element.closest('.description-scroll-wrapper')) {
            return true;
        }
    }
    return false;
};

window.isDescriptionAtBottom = function () {
    const descWrapper = document.querySelector('.anime-card.active .description-scroll-wrapper');
    if (!descWrapper) return false;
    const tolerance = 5;
    return descWrapper.scrollTop + descWrapper.clientHeight >= descWrapper.scrollHeight - tolerance;
};

window.isDescriptionAtTop = function () {
    const descWrapper = document.querySelector('.anime-card.active .description-scroll-wrapper');
    if (!descWrapper) return true;
    const tolerance = 5;
    return descWrapper.scrollTop <= tolerance;
};

window.focusElement = function (element) {
    if (element) element.focus();
};

// Returns true if the modal scroll container is at the very top (for pull-to-close).
window.isModalScrollAtTop = function (element) {
    if (!element) return true;
    return element.scrollTop <= 2;
};

// ─── Image preloader ──────────────────────────────────────────────────────────
// Creates hidden Image() objects so the browser fetches and caches the URLs.
// The popup banner / cover then appears instantly when opened.
// Called from Blazor with an array of URL strings.

const _preloadedUrls = new Set();

window.preloadImages = function (urls) {
    if (!urls || !urls.length) return;
    for (const url of urls) {
        if (!url || _preloadedUrls.has(url)) continue;
        _preloadedUrls.add(url);
        const img = new Image();
        img.src = url;
    }
};