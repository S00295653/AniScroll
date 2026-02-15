/**
 * Animation Helpers - AniScroll
 * Helpers optionnels pour améliorer les transitions
 */

// Force reflow après changement de classe pour garantir la transition
window.forceReflow = function (element) {
    if (element) {
        void element.offsetHeight;
    }
};

// Détecte si l'appareil préfère les animations réduites
window.prefersReducedMotion = function () {
    return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
};

// Applique une transition avec garantie de fluidité
window.smoothTransition = function (element, callback) {
    if (!element) return;

    // Si l'utilisateur préfère les animations réduites, skip
    if (window.prefersReducedMotion()) {
        if (callback) callback();
        return;
    }

    // Force reflow avant la transition
    void element.offsetHeight;

    // Applique le changement
    if (callback) callback();

    // Force reflow après
    void element.offsetHeight;
};

// Vérifie si une transition est en cours
window.isTransitioning = function (element) {
    if (!element) return false;

    const style = window.getComputedStyle(element);
    const duration = parseFloat(style.transitionDuration);
    
    return duration > 0;
};

// Attend la fin d'une transition
window.waitForTransition = function (element) {
    return new Promise((resolve) => {
        if (!element || !window.isTransitioning(element)) {
            resolve();
            return;
        }

        const handleTransitionEnd = () => {
            element.removeEventListener('transitionend', handleTransitionEnd);
            resolve();
        };

        element.addEventListener('transitionend', handleTransitionEnd);

        // Timeout de sécurité (500ms max)
        setTimeout(() => {
            element.removeEventListener('transitionend', handleTransitionEnd);
            resolve();
        }, 500);
    });
};

// Synchronise plusieurs transitions
window.syncTransitions = function (elements, callback) {
    if (!elements || elements.length === 0) {
        if (callback) callback();
        return;
    }

    const promises = Array.from(elements).map(el => window.waitForTransition(el));

    Promise.all(promises).then(() => {
        if (callback) callback();
    });
};

// Optimise la performance pendant une animation
window.optimizeForAnimation = function (element) {
    if (!element) return;

    element.style.willChange = 'transform, opacity';
    
    // Nettoie après l'animation
    const cleanup = () => {
        setTimeout(() => {
            element.style.willChange = 'auto';
        }, 500);
    };

    element.addEventListener('transitionend', cleanup, { once: true });
};

// Détecte la hauteur réelle d'un élément (même caché)
window.getActualHeight = function (element) {
    if (!element) return 0;

    const clone = element.cloneNode(true);
    clone.style.visibility = 'hidden';
    clone.style.position = 'absolute';
    clone.style.display = 'block';
    clone.style.maxHeight = 'none';
    
    document.body.appendChild(clone);
    const height = clone.offsetHeight;
    document.body.removeChild(clone);
    
    return height;
};

// Calcule le layout optimal pour un titre
window.calculateTitleLayout = function (titleElement, maxWidth) {
    if (!titleElement) return { lines: 1, height: 0 };

    const text = titleElement.textContent;
    const fontSize = parseFloat(window.getComputedStyle(titleElement).fontSize);
    const lineHeight = parseFloat(window.getComputedStyle(titleElement).lineHeight) || fontSize * 1.2;
    
    // Estimation du nombre de lignes
    const avgCharWidth = fontSize * 0.6;
    const charsPerLine = Math.floor(maxWidth / avgCharWidth);
    const estimatedLines = Math.ceil(text.length / charsPerLine);
    
    return {
        lines: estimatedLines,
        height: estimatedLines * lineHeight,
        fontSize: fontSize,
        lineHeight: lineHeight
    };
};

// Debug helper : log les transitions en cours
window.debugTransitions = function () {
    const elements = document.querySelectorAll('*');
    const transitioning = [];

    elements.forEach(el => {
        if (window.isTransitioning(el)) {
            transitioning.push({
                element: el,
                class: el.className,
                duration: window.getComputedStyle(el).transitionDuration
            });
        }
    });

    if (transitioning.length > 0) {
        console.log('🎬 Transitions en cours:', transitioning);
    } else {
        console.log('✅ Aucune transition en cours');
    }
};

// Mesure la performance d'une transition
window.measureTransitionPerformance = function (element, callback) {
    if (!element) return;

    const startTime = performance.now();
    let frameCount = 0;
    let animationId;

    const measureFrame = () => {
        frameCount++;
        
        if (window.isTransitioning(element)) {
            animationId = requestAnimationFrame(measureFrame);
        } else {
            const endTime = performance.now();
            const duration = endTime - startTime;
            const fps = Math.round((frameCount / duration) * 1000);

            console.log(`📊 Transition performance:
                Duration: ${duration.toFixed(2)}ms
                Frames: ${frameCount}
                FPS: ${fps}
            `);

            if (callback) callback({ duration, frameCount, fps });
        }
    };

    measureFrame();
};

// Applique une transition avec callback
window.transitionWithCallback = function (element, applyChanges, onComplete) {
    if (!element) {
        if (onComplete) onComplete();
        return;
    }

    // Applique les changements
    if (applyChanges) applyChanges();

    // Attend la fin de la transition
    window.waitForTransition(element).then(() => {
        if (onComplete) onComplete();
    });
};

// Export pour utilisation dans Blazor
window.animationHelpers = {
    forceReflow: window.forceReflow,
    prefersReducedMotion: window.prefersReducedMotion,
    smoothTransition: window.smoothTransition,
    isTransitioning: window.isTransitioning,
    waitForTransition: window.waitForTransition,
    syncTransitions: window.syncTransitions,
    optimizeForAnimation: window.optimizeForAnimation,
    getActualHeight: window.getActualHeight,
    calculateTitleLayout: window.calculateTitleLayout,
    debugTransitions: window.debugTransitions,
    measureTransitionPerformance: window.measureTransitionPerformance,
    transitionWithCallback: window.transitionWithCallback
};

console.log('✨ Animation Helpers chargés');