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