// ═══════════════════════════════════════════════════════════════════════
//  scroll-helpers.js  —  AniScroll (patched: MAUI drag + OAuth cross-tab)
// ═══════════════════════════════════════════════════════════════════════

// ── Viewport / scroll helpers ────────────────────────────────────────────────

window.getViewportHeight = function () {
    const scrollSection = document.querySelector('.main-scroll');
    if (scrollSection) return scrollSection.clientHeight;
    return window.visualViewport ? window.visualViewport.height : window.innerHeight;
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

window.getElementScrollHeight = function (selector) {
    const el = document.querySelector(selector);
    return el ? el.scrollHeight : 0;
};

window.focusElement = function (element) {
    if (element) element.focus();
};

window.isModalScrollAtTop = function (element) {
    if (!element) return true;
    return element.scrollTop <= 2;
};

window.scrollElementToRight = function (el) {
    if (!el) return;
    requestAnimationFrame(function () { el.scrollLeft = el.scrollWidth; });
};


// ── Description zone helpers ─────────────────────────────────────────────────

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
    return descWrapper.scrollTop + descWrapper.clientHeight >= descWrapper.scrollHeight - 5;
};

window.isDescriptionAtTop = function () {
    const descWrapper = document.querySelector('.anime-card.active .description-scroll-wrapper');
    if (!descWrapper) return true;
    return descWrapper.scrollTop <= 5;
};


// ── Image preloader ──────────────────────────────────────────────────────────

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


// ── Element bounds / pointer helpers ─────────────────────────────────────────

window.getElementBounds = function (element) {
    if (!element) return { left: 0, width: 0 };
    const rect = element.getBoundingClientRect();
    return { left: rect.left, width: rect.width };
};

window.getElementBoundingRect = function (el) {
    const r = el.getBoundingClientRect();
    return {
        top: r.top, bottom: r.bottom, left: r.left, right: r.right,
        width: r.width, height: r.height,
        viewportWidth: window.innerWidth, viewportHeight: window.innerHeight
    };
};

window.capturePointer = function (element, pointerId) {
    if (element && element.setPointerCapture) {
        try { element.setPointerCapture(pointerId); } catch (e) { }
    }
};


// ── Input helpers ─────────────────────────────────────────────────────────────

window.selectInputContent = function (element) {
    if (element) {
        setTimeout(function () { try { element.select(); } catch (e) { } }, 0);
    }
};

window.setAndSelectInput = function (element, value) {
    if (element) {
        const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
        setter.call(element, value);
        element.dispatchEvent(new Event('input', { bubbles: true }));
        setTimeout(function () { try { element.select(); } catch (e) { } }, 0);
    }
};

window.setInputValue = function (element, value) {
    if (!element) return;
    const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
    setter.call(element, value != null ? value : '');
    element.dispatchEvent(new Event('input', { bubbles: true }));
};

window.setBarWidth = function (containerElement, fillSelector, widthPct, bgColor) {
    if (!containerElement) return;
    const fill = containerElement.querySelector(fillSelector);
    if (!fill) return;
    fill.style.transition = 'none';
    fill.style.width = Math.max(0, Math.min(100, widthPct)).toFixed(2) + '%';
    if (bgColor) fill.style.background = bgColor;
};


// ═══════════════════════════════════════════════════════════════════════
//  COLOR PICKER
// ═══════════════════════════════════════════════════════════════════════

window.cpGetRect = function (el) {
    const r = el.getBoundingClientRect();
    return { left: r.left, top: r.top, width: r.width, height: r.height };
};

window.cpGetPosition = function (anchorEl, popupW, _estimatedPopupH) {
    const MARGIN = 12;
    const GAP = 8;

    const a = anchorEl.getBoundingClientRect();
    const vw = window.innerWidth;
    const vh = window.innerHeight;

    const popupEl = document.querySelector('.cp-popup');
    const popupH = popupEl ? popupEl.getBoundingClientRect().height : _estimatedPopupH;
    const w = Math.min(popupW, vw - 2 * MARGIN);

    const spaceBelow = vh - a.bottom - GAP;
    const spaceAbove = a.top - GAP;

    let top;
    if (spaceBelow >= popupH || spaceBelow >= spaceAbove) {
        top = a.bottom + GAP;
        if (top + popupH > vh - MARGIN) top = vh - MARGIN - popupH;
    } else {
        top = a.top - GAP - popupH;
        if (top < MARGIN) top = MARGIN;
    }

    let left = a.left;
    if (left + w > vw - MARGIN) left = vw - MARGIN - w;
    if (left < MARGIN) left = MARGIN;

    return { left, top };
};

// SV drag
let _cpSvDotNet = null, _cpSvEl = null, _cpSvActive = false;

window.cpStartSvDrag = function (svEl, dotNet) {
    _cpSvEl = svEl; _cpSvDotNet = dotNet; _cpSvActive = true;
    const onMove = (cx, cy) => {
        const r = _cpSvEl.getBoundingClientRect();
        _cpSvDotNet.invokeMethodAsync('OnSvDrag',
            Math.max(0, Math.min(1, (cx - r.left) / r.width)),
            Math.max(0, Math.min(1, (cy - r.top) / r.height)));
    };
    const onMM = e => { if (_cpSvActive) onMove(e.clientX, e.clientY); };
    const onTM = e => {
        if (_cpSvActive && e.touches.length) {
            e.preventDefault();
            onMove(e.touches[0].clientX, e.touches[0].clientY);
        }
    };
    const stop = () => {
        _cpSvActive = false;
        document.removeEventListener('mousemove', onMM);
        document.removeEventListener('mouseup', stop);
        document.removeEventListener('touchmove', onTM);
        document.removeEventListener('touchend', stop);
    };
    document.addEventListener('mousemove', onMM);
    document.addEventListener('mouseup', stop);
    document.addEventListener('touchmove', onTM, { passive: false });
    document.addEventListener('touchend', stop);
};

// Hue drag
let _cpHueDotNet = null, _cpHueEl = null, _cpHueActive = false;

window.cpStartHueDrag = function (hueEl, dotNet) {
    _cpHueEl = hueEl; _cpHueDotNet = dotNet; _cpHueActive = true;
    const onMove = cx => {
        const r = _cpHueEl.getBoundingClientRect();
        _cpHueDotNet.invokeMethodAsync('OnHueDrag',
            Math.max(0, Math.min(1, (cx - r.left) / r.width)));
    };
    const onMM = e => { if (_cpHueActive) onMove(e.clientX); };
    const onTM = e => {
        if (_cpHueActive && e.touches.length) {
            e.preventDefault();
            onMove(e.touches[0].clientX);
        }
    };
    const stop = () => {
        _cpHueActive = false;
        document.removeEventListener('mousemove', onMM);
        document.removeEventListener('mouseup', stop);
        document.removeEventListener('touchmove', onTM);
        document.removeEventListener('touchend', stop);
    };
    document.addEventListener('mousemove', onMM);
    document.addEventListener('mouseup', stop);
    document.addEventListener('touchmove', onTM, { passive: false });
    document.addEventListener('touchend', stop);
};

window.cpStopDrag = function () { _cpSvActive = false; _cpHueActive = false; };

window.cpRandomSwatchColor = function () {
    const swatches = [
        '#ef4444', '#f97316', '#f59e0b', '#eab308',
        '#84cc16', '#22c55e', '#10b981', '#14b8a6',
        '#06b6d4', '#0ea5e9', '#3b82f6', '#6366f1',
        '#8b5cf6', '#a855f7', '#d946ef', '#ec4899',
    ];
    return swatches[Math.floor(Math.random() * swatches.length)];
};


// ═══════════════════════════════════════════════════════════════════════
//  ROW DRAG — live visual reorder for CustomListManager
// ═══════════════════════════════════════════════════════════════════════

window.startRowDrag = function (dotNet, _panel, body, pointerId, fromIndex, startClientY) {
    if (!body) return;

    const rows = Array.from(body.querySelectorAll('.clm2-row:not(.clm2-row-new)'));
    if (fromIndex < 0 || fromIndex >= rows.length) return;

    const dragged = rows[fromIndex];
    const bodyRect = body.getBoundingClientRect();
    const origRect = dragged.getBoundingClientRect();

    let rowH = dragged.offsetHeight + 8;
    if (rows.length > 1) {
        const r0 = rows[0].getBoundingClientRect();
        const r1 = rows[Math.min(1, rows.length - 1)].getBoundingClientRect();
        if (rows.length > 1) rowH = r1.top - r0.top;
    }

    const initialBodyY = origRect.top - bodyRect.top + body.scrollTop;

    body.style.position = 'relative';
    dragged.style.position = 'absolute';
    dragged.style.left = '0';
    dragged.style.right = '0';
    dragged.style.zIndex = '50';
    dragged.style.top = initialBodyY + 'px';
    dragged.style.opacity = '0.5';
    dragged.style.boxShadow = '0 8px 28px rgba(0,0,0,0.55)';
    dragged.style.transition = 'none';

    const ph = document.createElement('div');
    ph.style.height = rowH + 'px';
    ph.style.flexShrink = '0';
    body.insertBefore(ph, dragged);

    let toIndex = fromIndex;
    let ended = false;

    try { dragged.setPointerCapture(pointerId); } catch (_) { }

    function onMove(clientY) {
        const delta = clientY - startClientY;
        const newTop = Math.max(0, initialBodyY + delta);
        dragged.style.top = newTop + 'px';

        const centreY = newTop + rowH / 2;
        const others = Array.from(body.querySelectorAll('.clm2-row:not(.clm2-row-new)'))
            .filter(r => r !== dragged);

        let best = 0;
        for (let i = 0; i < others.length; i++) {
            if (centreY > others[i].offsetTop + rowH / 2) best = i + 1;
        }
        toIndex = best;

        const anchor = others[best] ?? null;
        if (anchor) {
            body.insertBefore(ph, anchor);
        } else {
            const last = others[others.length - 1];
            if (last && last.nextSibling) body.insertBefore(ph, last.nextSibling);
            else body.appendChild(ph);
        }
    }

    function onEnd() {
        if (ended) return;
        ended = true;

        document.removeEventListener('pointermove', onPM);
        document.removeEventListener('pointerup', onPU);
        document.removeEventListener('pointercancel', onPU);
        document.removeEventListener('touchmove', onTM);
        document.removeEventListener('touchend', onTE);

        dragged.style.position = '';
        dragged.style.left = '';
        dragged.style.right = '';
        dragged.style.zIndex = '';
        dragged.style.top = '';
        dragged.style.opacity = '';
        dragged.style.boxShadow = '';
        dragged.style.transition = '';
        body.style.position = '';
        ph.remove();

        try { dragged.releasePointerCapture(pointerId); } catch (_) { }
        dotNet.invokeMethodAsync('OnDragComplete', fromIndex, toIndex);
    }

    const onPM = e => onMove(e.clientY);
    const onPU = () => onEnd();
    const onTM = e => { if (e.touches.length) { e.preventDefault(); onMove(e.touches[0].clientY); } };
    const onTE = () => onEnd();

    document.addEventListener('pointermove', onPM);
    document.addEventListener('pointerup', onPU);
    document.addEventListener('pointercancel', onPU);
    document.addEventListener('touchmove', onTM, { passive: false });
    document.addEventListener('touchend', onTE);
};


// ═══════════════════════════════════════════════════════════════════════
//  MODAL SCROLL — stop momentum + edge swipe intercept
// ═══════════════════════════════════════════════════════════════════════

window.stopMomentumScroll = function (el) {
    if (!el) return;
    el.scrollTop = el.scrollTop;
};

let _edgeSwipeCleanup = null;

window.registerEdgeSwipeInterceptor = function (scrollEl, edgeZone) {
    if (!scrollEl) return;
    edgeZone = edgeZone || 40;

    if (_edgeSwipeCleanup) { _edgeSwipeCleanup(); _edgeSwipeCleanup = null; }

    const onTouchStart = (e) => {
        if (!e.touches.length) return;
        const x = e.touches[0].clientX;
        if (x <= edgeZone) {
            scrollEl.scrollTop = scrollEl.scrollTop;
            scrollEl.style.overflowY = 'hidden';
            const restore = () => {
                scrollEl.style.overflowY = '';
                document.removeEventListener('touchend', restore);
                document.removeEventListener('touchcancel', restore);
            };
            document.addEventListener('touchend', restore, { once: true });
            document.addEventListener('touchcancel', restore, { once: true });
        }
    };

    scrollEl.addEventListener('touchstart', onTouchStart, { passive: true, capture: true });

    _edgeSwipeCleanup = () => {
        scrollEl.removeEventListener('touchstart', onTouchStart, { capture: true });
        scrollEl.style.overflowY = '';
    };
};

window.unregisterEdgeSwipeInterceptor = function () {
    if (_edgeSwipeCleanup) { _edgeSwipeCleanup(); _edgeSwipeCleanup = null; }
};

window.preventWheelScroll = function (el) {
    if (!el || el._noWheel) return;
    el._noWheel = function (e) {
        let node = e.target;
        while (node && node !== el) {
            const oy = window.getComputedStyle(node).overflowY;
            if ((oy === 'auto' || oy === 'scroll') && node.scrollHeight > node.clientHeight) return;
            node = node.parentElement;
        }
        e.preventDefault();
    };
    el.addEventListener('wheel', el._noWheel, { passive: false });
};

window.removeWheelScrollPrevention = function (el) {
    if (!el || !el._noWheel) return;
    el.removeEventListener('wheel', el._noWheel);
    delete el._noWheel;
};


// ═══════════════════════════════════════════════════════════════════════
//  AniList OAuth helpers — cross-tab communication
// ═══════════════════════════════════════════════════════════════════════

var _aniListTokenKey = 'anilist_token';
var _aniListTokenSignalKey = 'anilist_token_signal';
var _aniListTokenCallback = null;
var _aniListPollInterval = null;

window.aniListGetToken = function () {
    try {
        var hash = window.location.hash;
        if (hash && hash.includes('access_token=')) {
            var params = new URLSearchParams(hash.substring(1));
            var token = params.get('access_token');
            if (token) {
                localStorage.setItem(_aniListTokenKey, token);
                // Signal other tabs
                localStorage.setItem(_aniListTokenSignalKey, Date.now().toString());
                history.replaceState(null, '', window.location.pathname + window.location.search);
                // Try to close this tab (works if opened via window.open)
                setTimeout(function () {
                    try { window.close(); } catch (e) { }
                }, 400);
                return { token: token, isNewAuth: true };
            }
        }
        var saved = localStorage.getItem(_aniListTokenKey);
        return { token: saved || null, isNewAuth: false };
    } catch (e) {
        console.warn('[oauth] aniListGetToken error:', e);
        return { token: null, isNewAuth: false };
    }
};

window.aniListSaveToken = function (token) {
    try { if (token) localStorage.setItem(_aniListTokenKey, token); }
    catch (e) { console.warn('[oauth] aniListSaveToken error:', e); }
};

window.aniListRemoveToken = function () {
    try {
        localStorage.removeItem(_aniListTokenKey);
        localStorage.removeItem(_aniListTokenSignalKey);
    } catch (e) { console.warn('[oauth] aniListRemoveToken error:', e); }
};

/**
 * Start listening for token arrival from another tab via localStorage events + polling.
 * Called after opening the OAuth popup.
 */
window.aniListListenForToken = function (dotNetRef) {
    window.aniListStopListening();

    // Remember the current token value so we only react to NEW writes
    var tokenAtStart = null;
    try { tokenAtStart = localStorage.getItem(_aniListTokenKey); } catch (e) { }

    _aniListTokenCallback = function (e) {
        if (e.key === _aniListTokenSignalKey || e.key === _aniListTokenKey) {
            var token = null;
            try { token = localStorage.getItem(_aniListTokenKey); } catch (ex) { }
            if (token && token !== tokenAtStart) {
                window.aniListStopListening();
                dotNetRef.invokeMethodAsync('OnTokenFromOtherTab', token);
            }
        }
    };
    window.addEventListener('storage', _aniListTokenCallback);

    // Polling fallback (storage events don't fire in same-tab or some WebViews)
    var pollCount = 0;
    _aniListPollInterval = setInterval(function () {
        pollCount++;
        if (pollCount > 300) { // 5 min max
            window.aniListStopListening();
            return;
        }
        var token = null;
        try { token = localStorage.getItem(_aniListTokenKey); } catch (ex) { }
        if (token && token !== tokenAtStart) {
            window.aniListStopListening();
            dotNetRef.invokeMethodAsync('OnTokenFromOtherTab', token);
        }
    }, 1000);
};

window.aniListStopListening = function () {
    if (_aniListTokenCallback) {
        window.removeEventListener('storage', _aniListTokenCallback);
        _aniListTokenCallback = null;
    }
    if (_aniListPollInterval) {
        clearInterval(_aniListPollInterval);
        _aniListPollInterval = null;
    }
};


// ═══════════════════════════════════════════════════════════════════════
//  ESCAPE KEY
// ═══════════════════════════════════════════════════════════════════════

let _escHandler = null;
let _escDotNet = null;
let _sfpEscDotNet = null;

window.sfpRegisterEscape = function (dotNet) { _sfpEscDotNet = dotNet; };
window.sfpUnregisterEscape = function () { _sfpEscDotNet = null; };

window.registerEscapeKey = function (dotNet) {
    _escDotNet = dotNet;
    if (_escHandler) document.removeEventListener('keydown', _escHandler, { capture: true });

    _escHandler = function (e) {
        if (e.key !== 'Escape') return;
        if (document.querySelector('.sfp-overlay')) {
            e.preventDefault();
            e.stopPropagation();
            if (_sfpEscDotNet) _sfpEscDotNet.invokeMethodAsync('HandleEscapeFromJS');
            return;
        }
        e.preventDefault();
        _escDotNet.invokeMethodAsync('HandleEscapeKey');
    };

    document.addEventListener('keydown', _escHandler, { capture: true });
};

window.unregisterEscapeKey = function () {
    if (_escHandler) {
        document.removeEventListener('keydown', _escHandler, { capture: true });
        _escHandler = null;
    }
    _escDotNet = null;
};


// ═══════════════════════════════════════════════════════════════════════
//  SFP — drag-release protection
// ═══════════════════════════════════════════════════════════════════════

(function () {
    let _sfpDownInside = false;

    window.sfpRegisterPanelDragProtect = function (overlayEl, panelEl) {
        if (!overlayEl || !panelEl) return function () { };

        const onPanelDown = () => { _sfpDownInside = true; };
        const onOverlayDown = () => { _sfpDownInside = false; };
        const onDocUp = () => { setTimeout(() => { _sfpDownInside = false; }, 0); };
        const onOverlayClick = (e) => {
            if (_sfpDownInside) { e.stopImmediatePropagation(); _sfpDownInside = false; }
        };

        panelEl.addEventListener('pointerdown', onPanelDown, { capture: true });
        overlayEl.addEventListener('pointerdown', onOverlayDown, { capture: true });
        overlayEl.addEventListener('click', onOverlayClick, { capture: true });
        document.addEventListener('pointerup', onDocUp, { capture: true });

        return function cleanup() {
            panelEl.removeEventListener('pointerdown', onPanelDown, { capture: true });
            overlayEl.removeEventListener('pointerdown', onOverlayDown, { capture: true });
            overlayEl.removeEventListener('click', onOverlayClick, { capture: true });
            document.removeEventListener('pointerup', onDocUp, { capture: true });
        };
    };

    window.sfpIsPointerDownInPanel = function () { return _sfpDownInside; };
})();


// ═══════════════════════════════════════════════════════════════════════
//  CARD DRAG — native JS, zero Blazor overhead on every move frame
//  PATCHED: added pointer events for MAUI WebView compatibility
// ═══════════════════════════════════════════════════════════════════════

(function () {
    let _dotNet = null;
    let isDragging = false;
    let startX = 0, startY = 0;
    let curX = 0, curY = 0;
    let axis = 'none';   // 'none' | 'horizontal' | 'vertical'
    let hasMoved = false;
    let suppressNextClick = false;

    // RAF throttle
    let _rafId = 0;

    // Cache of active card's children — populated at drag-start, never queried mid-drag
    let _cached = {};

    // Flag: next time a new .active card appears, run the entrance animation
    let _pendingEntrance = false;

    // Track the last active card element to detect genuine card changes
    let _lastActiveCard = null;

    // Snapshot of every card's baseY at drag-start
    let _cardBaseTransforms = [];

    const AXIS_LOCK = 8;
    const MOVE_THRESHOLD = 8;
    const HORIZ_THRESHOLD = 85;

    // ── Helpers ───────────────────────────────────────────────────────────────

    function activeCard() {
        return document.querySelector('.anime-card.active');
    }

    function resetImage(img) {
        if (!img) return;
        img.style.transition = 'none';
        img.style.opacity = '';
        img.style.transform = '';
        void img.offsetWidth;
        img.style.transition = '';
    }

    // Check if any overlay/modal is open (drag should be suppressed)
    function isOverlayOpen() {
        return !!(
            document.querySelector('.anime-detail-overlay') ||
            document.querySelector('.lists-overlay') ||
            document.querySelector('.sfp-overlay') ||
            document.querySelector('.swset-overlay') ||
            document.querySelector('.pp-overlay') ||
            document.querySelector('.clm-overlay') ||
            document.querySelector('.search-results-panel.visible') ||
            document.querySelector('.list-editor-overlay') ||
            document.querySelector('.delete-confirm-overlay') ||
            document.querySelector('.sp-overlay')
        );
    }

    // ── Entrance animation ────────────────────────────────────────────────────
    function runEntrance() {
        const c = activeCard();
        if (!c) return;

        c.style.opacity = '0';
        c.style.transform = 'translateY(0px)';

        requestAnimationFrame(() => {
            requestAnimationFrame(() => {
                c.style.opacity = '';
                c.style.transform = '';

                c.classList.remove('card-entering');
                void c.offsetWidth;
                c.classList.add('card-entering');
                setTimeout(() => c.classList.remove('card-entering'), 500);

                resetImage(c.querySelector('.anime-image'));
            });
        });
    }

    // ── Paint — uses cached elements, never queries DOM mid-drag ─────────────

    function paint() {
        if (axis === 'vertical') {
            _cardBaseTransforms.forEach(item => {
                item.el.style.transform = `translateY(${item.baseY + curY}px)`;
            });
            return;
        }

        if (axis !== 'horizontal') return;

        const rOp = curX > 5 ? Math.min(1, (curX - 5) / 50) : 0;
        const lOp = curX < -5 ? Math.min(1, (-curX - 5) / 50) : 0;

        if (_cached.fr) _cached.fr.style.opacity = rOp;
        if (_cached.fl) _cached.fl.style.opacity = lOp;
        if (_cached.hr) _cached.hr.style.opacity = rOp;
        if (_cached.hl) _cached.hl.style.opacity = lOp;

        if (_cached.img) {
            if (curX !== 0) {
                const rot = Math.max(-16, Math.min(16, curX * 0.022));
                _cached.img.style.transform = `translateX(${curX}px) rotate(${rot}deg)`;
            } else {
                _cached.img.style.transform = '';
            }
        }
    }

    // ── Move — RAF-throttled, at most one paint per display frame ─────────────

    function onMove(x, y) {
        if (!isDragging) return;
        const dx = x - startX, dy = y - startY;
        if (Math.abs(dx) > MOVE_THRESHOLD || Math.abs(dy) > MOVE_THRESHOLD) hasMoved = true;
        if (axis === 'none' && (Math.abs(dx) > AXIS_LOCK || Math.abs(dy) > AXIS_LOCK))
            axis = Math.abs(dx) > Math.abs(dy) ? 'horizontal' : 'vertical';

        if (axis === 'vertical') { curY = dy; curX = 0; }
        else if (axis === 'horizontal') { curX = dx; curY = 0; }

        if (!_rafId) _rafId = requestAnimationFrame(() => { _rafId = 0; paint(); });
    }

    // ── End ───────────────────────────────────────────────────────────────────

    function onEnd() {
        if (!isDragging) return;
        isDragging = false;

        // Cancel any pending RAF so no stale paint fires after release
        if (_rafId) { cancelAnimationFrame(_rafId); _rafId = 0; }

        // Restore expensive GPU effects now that drag is over
        const c = activeCard();
        if (c) c.classList.remove('dragging');

        if (hasMoved) {
            suppressNextClick = true;
            setTimeout(() => { suppressNextClick = false; }, 100);
        }

        const horizValid = axis === 'horizontal' && Math.abs(curX) >= HORIZ_THRESHOLD;
        const isRight = curX > 0;

        // ── HORIZONTAL: snap-back ─────────────────────────────────────────────
        if (axis === 'horizontal' && !horizValid) {
            const card = activeCard();
            if (card) {
                ['.swipe-feather-right', '.swipe-feather-left',
                    '.swipe-hint-right', '.swipe-hint-left'].forEach(sel => {
                        const el = card.querySelector(sel); if (el) el.style.opacity = 0;
                    });
                const img = card.querySelector('.anime-image');
                if (img) {
                    img.classList.add('image-with-transition');
                    void img.offsetWidth;
                    img.style.transform = '';
                    setTimeout(() => img.classList.remove('image-with-transition'), 300);
                }
            }
            if (_dotNet) _dotNet.invokeMethodAsync('OnDragEnd', curX, curY, axis, hasMoved);
            return;
        }

        // ── HORIZONTAL: validated — Tinder fly-off ────────────────────────────
        if (horizValid) {
            const card = activeCard();
            if (card) {
                const flyX = isRight ? 920 : -920;
                const flyY = -90;
                const rot = isRight ? 38 : -38;

                const img = card.querySelector('.anime-image');
                if (img) {
                    img.style.transition =
                        'transform 0.44s cubic-bezier(0.4, 0, 0.8, 0.6), ' +
                        'opacity   0.36s ease-in 0.06s';
                    img.style.transform =
                        `translateX(${flyX}px) translateY(${flyY}px) rotate(${rot}deg)`;
                    img.style.opacity = '0';
                }

                ['.swipe-feather-right', '.swipe-feather-left',
                    '.swipe-hint-right', '.swipe-hint-left'].forEach(sel => {
                        const el = card.querySelector(sel);
                        if (el) { el.style.transition = 'opacity 0.12s'; el.style.opacity = 0; }
                    });
            }

            _pendingEntrance = true;
            if (_dotNet) _dotNet.invokeMethodAsync('OnDragEnd', curX, curY, axis, hasMoved);
            return;
        }

        // ── VERTICAL + fallback ───────────────────────────────────────────────
        if (_dotNet) _dotNet.invokeMethodAsync('OnDragEnd', curX, curY, axis, hasMoved);
    }

    // ── MutationObserver ──────────────────────────────────────────────────────
    const _observer = new MutationObserver(() => {
        const c = activeCard();
        if (!c || c === _lastActiveCard) return;
        _lastActiveCard = c;

        if (_pendingEntrance) {
            _pendingEntrance = false;
            Promise.resolve().then(() => runEntrance());
            return;
        }

        resetImage(c.querySelector('.anime-image'));
    });

    _observer.observe(document.body, {
        subtree: true,
        attributes: true,
        attributeFilter: ['class'],
    });

    // ── Native pointerdown on active card (MAUI WebView fix) ──────────────────
    // Uses event delegation on document. Bypasses Blazor's event system which
    // can fail to forward mousedown/touchstart in MAUI WebView2.
    document.addEventListener('pointerdown', function (e) {
        if (!_dotNet || isDragging) return;

        // Don't interfere with overlays/modals
        if (isOverlayOpen()) return;

        // Check if the pointerdown is on the active card
        var card = e.target.closest('.anime-card.active');
        if (!card) return;

        // Don't intercept clicks on interactive elements
        var tag = e.target.tagName.toLowerCase();
        if (tag === 'button' || tag === 'input' || tag === 'a' || tag === 'textarea' || tag === 'select') return;
        if (e.target.closest('button') || e.target.closest('a') || e.target.closest('input') || e.target.closest('textarea')) return;

        // Start drag via native JS
        window.startCardDrag(e.clientX, e.clientY);
    }, { passive: false, capture: false });

    // ── Public API ────────────────────────────────────────────────────────────

    window.initCardDrag = function (dotNetRef) { _dotNet = dotNetRef; };

    window.startCardDrag = function (x, y) {
        if (isDragging) return; // Prevent double-start

        isDragging = true;
        startX = x; startY = y;
        curX = 0; curY = 0;
        axis = 'none'; hasMoved = false;

        // Snapshot every card's current translateY for vertical neighbour-peek
        _cardBaseTransforms = [];
        document.querySelectorAll('.anime-card').forEach(card => {
            const m = card.style.transform.match(/translateY\(([-\d.]+)px\)/);
            _cardBaseTransforms.push({ el: card, baseY: m ? parseFloat(m[1]) : 0 });
        });

        // Cache active card children once — paint() never querySelector mid-drag
        _cached = { fr: null, fl: null, hr: null, hl: null, img: null };
        const c = activeCard();
        if (c) {
            _cached.fr = c.querySelector('.swipe-feather-right');
            _cached.fl = c.querySelector('.swipe-feather-left');
            _cached.hr = c.querySelector('.swipe-hint-right');
            _cached.hl = c.querySelector('.swipe-hint-left');
            _cached.img = c.querySelector('.anime-image');

            // Disable box-shadow + backdrop-filter for the duration of the drag.
            c.classList.add('dragging');
        }
    };

    window.animateCardEntrance = function () {
        runEntrance();
    };

    window.snapCardsBack = function () {
        _cardBaseTransforms.forEach(item => {
            item.el.style.transition = 'transform 0.32s cubic-bezier(0.25, 0.46, 0.45, 0.94)';
            item.el.style.transform = `translateY(${item.baseY}px)`;
        });
        setTimeout(() => {
            _cardBaseTransforms.forEach(item => { item.el.style.transition = ''; });
        }, 340);
    };

    // ── Native listeners ──────────────────────────────────────────────────────

    // Pointer events (universal — works on MAUI, desktop, touch, pen)
    document.addEventListener('pointermove', e => {
        if (isDragging) {
            onMove(e.clientX, e.clientY);
            e.preventDefault();
        }
    }, { passive: false });
    document.addEventListener('pointerup', () => { if (isDragging) onEnd(); });
    document.addEventListener('pointercancel', () => { if (isDragging) onEnd(); });

    // Mouse fallback (older browsers without pointer events)
    document.addEventListener('mousemove', e => { if (isDragging) onMove(e.clientX, e.clientY); });
    document.addEventListener('mouseup', () => { if (isDragging) onEnd(); });

    // Touch fallback
    document.addEventListener('touchmove', e => {
        if (isDragging && e.touches.length) {
            onMove(e.touches[0].clientX, e.touches[0].clientY);
            e.preventDefault();
        }
    }, { passive: false });

    document.addEventListener('touchend', () => { if (isDragging) onEnd(); });
    document.addEventListener('touchcancel', () => { if (isDragging) onEnd(); });

    // Suppress the ghost click that fires right after a drag
    document.addEventListener('click', e => {
        if (suppressNextClick) {
            e.stopPropagation();
            e.preventDefault();
            suppressNextClick = false;
        }
    }, true);

    // ── Mouse wheel navigation between cards ─────────────────────────────
    let _wheelCooldown = false;

    document.addEventListener('wheel', e => {
        if (!_dotNet || isDragging || _wheelCooldown) return;

        // Don't handle wheel when overlays/modals are open
        if (isOverlayOpen()) return;

        // Only trigger on meaningful scroll delta
        if (Math.abs(e.deltaY) < 10) return;

        _wheelCooldown = true;

        // deltaY > 0 = scroll down = next card = negative offset
        const yOffset = e.deltaY > 0 ? -100 : 100;
        _dotNet.invokeMethodAsync('OnDragEnd', 0, yOffset, 'vertical', false);

        // Cooldown prevents rapid-fire navigation
        setTimeout(() => { _wheelCooldown = false; }, 350);

        e.preventDefault();
    }, { passive: false });
})();

// ═══════════════════════════════════════════════════════════════════════
//  SHARE
// ═══════════════════════════════════════════════════════════════════════

window.shareAnime = async function (title, url) {
    if (navigator.share) {
        try {
            await navigator.share({ title: title, url: url });
            return 'shared';
        } catch (e) {
            if (e.name === 'AbortError') return 'cancelled';
        }
    }
    // Fallback: copy URL to clipboard
    try {
        await navigator.clipboard.writeText(url);
        return 'copied';
    } catch (e) {
        return 'error';
    }
};