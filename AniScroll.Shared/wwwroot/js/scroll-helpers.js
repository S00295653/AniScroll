window.getViewportHeight = function () {
    return window.innerHeight;
};

window.getScrollInfo = function (element) {
    if (!element) {
        return { scrollTop: 0, scrollHeight: 0, clientHeight: 0 };
    }

    return {
        scrollTop: element.scrollTop,
        scrollHeight: element.scrollHeight,
        clientHeight: element.clientHeight
    };
};

window.setScrollTop = function (element, value) {
    if (element) {
        element.scrollTop = value;
    }
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

    if (!descWrapper) {
        return false;
    }

    const tolerance = 5;
    const isAtBottom = descWrapper.scrollTop + descWrapper.clientHeight >= descWrapper.scrollHeight - tolerance;

    return isAtBottom;
};

window.isDescriptionAtTop = function () {
    const descWrapper = document.querySelector('.anime-card.active .description-scroll-wrapper');

    if (!descWrapper) {
        return true;
    }

    const tolerance = 5;
    const isAtTop = descWrapper.scrollTop <= tolerance;

    return isAtTop;
};

window.focusElement = function (element) {
    if (element) {
        element.focus();
    }
};