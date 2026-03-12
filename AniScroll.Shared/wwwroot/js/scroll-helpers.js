// ═══════════════════════════════════════════════════════════════════════
//  scroll-helpers.js  —  AniScroll
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


// ── Element bounds / pointer helpers ────────────────────────────────────────

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


// ── Input helpers ────────────────────────────────────────────────────────────

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
    const onTM = e => { if (_cpSvActive && e.touches.length) { e.preventDefault(); onMove(e.touches[0].clientX, e.touches[0].clientY); } };
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
        _cpHueDotNet.invokeMethodAsync('OnHueDrag', Math.max(0, Math.min(1, (cx - r.left) / r.width)));
    };
    const onMM = e => { if (_cpHueActive) onMove(e.clientX); };
    const onTM = e => { if (_cpHueActive && e.touches.length) { e.preventDefault(); onMove(e.touches[0].clientX); } };
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
            const mid = others[i].offsetTop + rowH / 2;
            if (centreY > mid) best = i + 1;
        }
        toIndex = best;

        const anchor = others[best] ?? null;
        if (anchor) {
            body.insertBefore(ph, anchor);
        } else {
            const last = others[others.length - 1];
            if (last && last.nextSibling) {
                body.insertBefore(ph, last.nextSibling);
            } else {
                body.appendChild(ph);
            }
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
            if ((oy === 'auto' || oy === 'scroll') && node.scrollHeight > node.clientHeight) {
                return;
            }
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
//  AniList OAuth helpers
// ═══════════════════════════════════════════════════════════════════════

/**
 * Returns { token: string|null, isNewAuth: bool }
 * isNewAuth=true  → fresh OAuth redirect  → show ProfilePanel so user sees "Import"
 * isNewAuth=false → previously saved token → silent auto-import in background
 */
window.aniListGetToken = function () {
    try {
        const hash = window.location.hash;

        // Parse #access_token=…&token_type=Bearer&expires_in=…
        if (hash && hash.includes('access_token=')) {
            const params = new URLSearchParams(hash.substring(1));
            const token = params.get('access_token');
            if (token) {
                // Persist & clean URL so the token doesn't show in browser history
                localStorage.setItem('anilist_token', token);
                history.replaceState(null, '', window.location.pathname + window.location.search);
                return { token: token, isNewAuth: true };
            }
        }

        // Fall back to a previously saved token (silent auto-import path)
        const saved = localStorage.getItem('anilist_token');
        return { token: saved || null, isNewAuth: false };
    } catch (e) {
        console.warn('[oauth-helpers] aniListGetToken error:', e);
        return { token: null, isNewAuth: false };
    }
};

/** Persists the token to localStorage. */
window.aniListSaveToken = function (token) {
    try {
        if (token) localStorage.setItem('anilist_token', token);
    } catch (e) {
        console.warn('[oauth-helpers] aniListSaveToken error:', e);
    }
};

/** Removes the persisted token from localStorage. */
window.aniListRemoveToken = function () {
    try {
        localStorage.removeItem('anilist_token');
    } catch (e) {
        console.warn('[oauth-helpers] aniListRemoveToken error:', e);
    }
};

// ═══════════════════════════════════════════════════════════════════════
//  ESCAPE KEY — global handler for PC keyboard support
// ═══════════════════════════════════════════════════════════════════════

let _escHandler = null;
let _escDotNet = null;
let _sfpEscDotNet = null;

window.sfpRegisterEscape = function (dotNet) { _sfpEscDotNet = dotNet; };
window.sfpUnregisterEscape = function () { _sfpEscDotNet = null; };

window.registerEscapeKey = function (dotNet) {
    _escDotNet = dotNet;

    if (_escHandler) {
        document.removeEventListener('keydown', _escHandler, { capture: true });
    }

    _escHandler = function (e) {
        if (e.key === 'Escape') {
            if (document.querySelector('.sfp-overlay')) {
                // SFP ouvert : on intercepte l'event en capture avant Blazor
                e.preventDefault();
                e.stopPropagation();          // ← empêche Blazor de voir l'event
                if (_sfpEscDotNet) {
                    _sfpEscDotNet.invokeMethodAsync('HandleEscapeFromJS');
                }
                return;
            }
            e.preventDefault();
            _escDotNet.invokeMethodAsync('HandleEscapeKey');
        }
    };

    // capture: true → on intercepte avant tous les handlers bubble (dont Blazor)
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
//  SFP (Sort & Filter Panel) — drag-release protection
// ═══════════════════════════════════════════════════════════════════════

(function () {
    let _sfpDownInside = false;

    window.sfpRegisterPanelDragProtect = function (overlayEl, panelEl) {
        if (!overlayEl || !panelEl) return function () { };

        const onPanelDown = () => { _sfpDownInside = true; };
        const onOverlayDown = () => { _sfpDownInside = false; };
        const onDocUp = () => { setTimeout(() => { _sfpDownInside = false; }, 0); };
        const onOverlayClick = (e) => {
            if (_sfpDownInside) {
                e.stopImmediatePropagation();
                _sfpDownInside = false;
            }
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
// ═══════════════════════════════════════════════════════════════════════

(function () {
    let _dotNet = null;
    let isDragging = false;
    let startX = 0, startY = 0;
    let curX = 0, curY = 0;
    let axis = 'none'; // 'none' | 'horizontal' | 'vertical'
    let hasMoved = false;
    let suppressNextClick = false;

    const AXIS_LOCK = 8, MOVE_THRESHOLD = 8;

    function card() { return document.querySelector('.anime-card.active'); }

    function paint() {
        const c = card();
        if (!c) return;

        if (axis !== 'horizontal') {
            c.style.transform = `translateY(${curY}px)`;
        }

        const rOp = curX > 5 ? Math.min(1, (curX - 5) / 50) : 0;
        const lOp = curX < -5 ? Math.min(1, (-curX - 5) / 50) : 0;
        const q = s => c.querySelector(s);

        const fr = q('.swipe-feather-right'), fl = q('.swipe-feather-left');
        const hr = q('.swipe-hint-right'), hl = q('.swipe-hint-left');
        const img = q('.anime-image');

        if (fr) fr.style.opacity = rOp;
        if (fl) fl.style.opacity = lOp;
        if (hr) hr.style.opacity = rOp;
        if (hl) hl.style.opacity = lOp;

        if (img) {
            if (curX !== 0) {
                const rot = Math.max(-16, Math.min(16, curX * 0.022));
                img.style.transform = `translateX(${curX}px) rotate(${rot}deg)`;
            } else {
                img.style.transform = '';
            }
        }
    }

    function onMove(x, y) {
        if (!isDragging) return;
        const dx = x - startX, dy = y - startY;
        if (Math.abs(dx) > MOVE_THRESHOLD || Math.abs(dy) > MOVE_THRESHOLD) hasMoved = true;
        if (axis === 'none' && (Math.abs(dx) > AXIS_LOCK || Math.abs(dy) > AXIS_LOCK))
            axis = Math.abs(dx) > Math.abs(dy) ? 'horizontal' : 'vertical';
        if (axis === 'vertical') { curY = dy; curX = 0; }
        else if (axis === 'horizontal') { curX = dx; curY = 0; }
        paint();
    }

    const HORIZ_THRESHOLD = 85;

    function onEnd() {
        if (!isDragging) return;
        isDragging = false;
        if (hasMoved) {
            suppressNextClick = true;
            setTimeout(() => { suppressNextClick = false; }, 100);
        }

        // Snap-back: Blazor's vdom diff won't touch the DOM if style didn't change
        // in its own render tree, so we animate back to center in JS directly.
        if (axis === 'horizontal' && Math.abs(curX) < HORIZ_THRESHOLD) {
            const c = card();
            if (c) {
                const q = s => c.querySelector(s);
                // Fade out feathers & hints immediately
                ['.swipe-feather-right', '.swipe-feather-left',
                    '.swipe-hint-right', '.swipe-hint-left'].forEach(sel => {
                        const el = q(sel); if (el) el.style.opacity = 0;
                    });
                // Animate image back to center
                const img = q('.anime-image');
                if (img) {
                    img.classList.add('image-with-transition');
                    img.style.transform = '';
                    setTimeout(() => img.classList.remove('image-with-transition'), 300);
                }
            }
        }

        if (_dotNet) _dotNet.invokeMethodAsync('OnDragEnd', curX, curY, axis, hasMoved);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    window.initCardDrag = function (dotNetRef) {
        _dotNet = dotNetRef;
    };

    window.startCardDrag = function (x, y) {
        isDragging = true;
        startX = x; startY = y;
        curX = 0; curY = 0;
        axis = 'none'; hasMoved = false;
    };

    // ── Native listeners ──────────────────────────────────────────────────────

    document.addEventListener('mousemove', e => onMove(e.clientX, e.clientY));
    document.addEventListener('mouseup', () => { if (isDragging) onEnd(); });

    document.addEventListener('touchmove', e => {
        if (isDragging && e.touches.length) {
            onMove(e.touches[0].clientX, e.touches[0].clientY);
            e.preventDefault();
        }
    }, { passive: false });

    document.addEventListener('touchend', () => { if (isDragging) onEnd(); });
    document.addEventListener('touchcancel', () => { if (isDragging) onEnd(); });

    // Suppress the click that fires right after a drag (capture phase → before Blazor)
    document.addEventListener('click', e => {
        if (suppressNextClick) {
            e.stopPropagation();
            e.preventDefault();
            suppressNextClick = false;
        }
    }, true);
})();