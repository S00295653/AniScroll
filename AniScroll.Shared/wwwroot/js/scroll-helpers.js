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

// --- Image preloader ----------------------------------------------------------
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

// --- Dynamic height measurement -----------------------------------------------
window.getElementScrollHeight = function (selector) {
    const el = document.querySelector(selector);
    return el ? el.scrollHeight : 0;
};

// --- Element bounds helper (clickable score & episode bars) -------------------
window.getElementBounds = function (element) {
    if (!element) return { left: 0, width: 0 };
    const rect = element.getBoundingClientRect();
    return { left: rect.left, width: rect.width };
};

// --- Pointer capture helper (slider bars) ------------------------------------
// Captures the pointer to the element so pointermove fires even outside it.
window.capturePointer = function (element, pointerId) {
    if (element && element.setPointerCapture) {
        try { element.setPointerCapture(pointerId); } catch (e) { }
    }
};

// --- Select all input content on focus (clear-on-click) ----------------------
window.selectInputContent = function (element) {
    if (element) {
        setTimeout(function () {
            try { element.select(); } catch (e) { }
        }, 0);
    }
};

// --- Set input value then select all (score/episode focus: shows raw value to edit) ---
window.setAndSelectInput = function (element, value) {
    if (element) {
        element.value = value;
        setTimeout(function () {
            try { element.select(); } catch (e) { }
        }, 0);
    }
};

// --- Direct bar fill update (bypass Blazor re-render for smooth drag) ---------
// containerElement: the bar container (@ref)
// fillSelector: CSS selector for the fill div inside it
// widthPct: 0..100
// bgColor: optional hex/css color (null to skip)
window.setBarWidth = function (containerElement, fillSelector, widthPct, bgColor) {
    if (!containerElement) return;
    var fill = containerElement.querySelector(fillSelector);
    if (!fill) return;
    fill.style.transition = 'none';
    fill.style.width = Math.max(0, Math.min(100, widthPct)).toFixed(2) + '%';
    if (bgColor) fill.style.background = bgColor;
};

// --- Direct input value update (for score/ep display during drag) -------------
window.setInputValue = function (element, value) {
    if (element) element.value = (value != null) ? value : '';
};