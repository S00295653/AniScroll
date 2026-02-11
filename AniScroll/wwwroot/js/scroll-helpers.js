// ========== FONCTIONS POUR LE SCROLL INTERACTIF ==========

/**
 * Récupère la hauteur du viewport (utilisé pour dimensionner les cartes)
 */
window.getViewportHeight = function () {
    return window.innerHeight;
};

/**
 * Récupère les informations de scroll d'un élément
 * @param {HTMLElement} element - Élément DOM
 * @returns {Object} - { scrollTop, scrollHeight, clientHeight }
 */
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

/**
 * Définit la position de scroll d'un élément
 * @param {HTMLElement} element - Élément DOM
 * @param {number} value - Nouvelle position scrollTop
 */
window.setScrollTop = function (element, value) {
    if (element) {
        element.scrollTop = value;
    }
};

/**
 * Vérifie si un touch/click est dans la zone de description scrollable
 * @param {number} clientX - Position X du touch/click
 * @param {number} clientY - Position Y du touch/click
 * @returns {boolean} - true si dans la zone de description
 */
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

/**
 * Vérifie si la description est scrollée au bout (en bas)
 * @returns {boolean} - true si au bout du scroll
 */
window.isDescriptionAtBottom = function () {
    const descWrapper = document.querySelector('.anime-card.active .description-scroll-wrapper');
    
    if (!descWrapper) {
        return false;
    }

    // Tolérance de 5px pour considérer qu'on est au bout
    const tolerance = 5;
    const isAtBottom = descWrapper.scrollTop + descWrapper.clientHeight >= descWrapper.scrollHeight - tolerance;
    
    return isAtBottom;
};

/**
 * Vérifie si la description est scrollée au sommet (en haut)
 * @returns {boolean} - true si au sommet du scroll
 */
window.isDescriptionAtTop = function () {
    const descWrapper = document.querySelector('.anime-card.active .description-scroll-wrapper');
    
    if (!descWrapper) {
        return true;
    }

    // Tolérance de 5px
    const tolerance = 5;
    const isAtTop = descWrapper.scrollTop <= tolerance;
    
    return isAtTop;
};