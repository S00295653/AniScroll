// Calcule la hauteur d'une carte anime (hauteur de la page - navbar fermée)
window.getCardHeight = function () {
    const navBar = document.querySelector('.main-nav-bar');
    const navHeight = navBar ? navBar.offsetHeight : 80;
    return window.innerHeight - navHeight;
};

// Retourne la position de scroll actuelle
window.getScrollPosition = function () {
    const scrollSection = document.querySelector('.main-scroll');
    if (!scrollSection) {
        return { scrollTop: 0, scrollHeight: 0, clientHeight: 0 };
    }

    return {
        scrollTop: scrollSection.scrollTop,
        scrollHeight: scrollSection.scrollHeight,
        clientHeight: scrollSection.clientHeight
    };
};

// Initialise les hauteurs des cartes
window.initializeCardHeights = function () {
    const cards = document.querySelectorAll('.anime-card-container');
    const cardHeight = window.getCardHeight();

    cards.forEach(card => {
        card.style.height = `${cardHeight}px`;
    });
};

// Initialise le scroll infini
window.initializeInfiniteScroll = function () {
    window.initializeCardHeights();

    window.addEventListener('resize', () => {
        window.initializeCardHeights();
    });
};

window.focusElement = function (element) {
    if (element) {
        element.focus();
    }
};