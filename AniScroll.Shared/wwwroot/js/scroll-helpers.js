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
        scrollTop:    element.scrollTop,
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
    const GAP    = 8;

    const a  = anchorEl.getBoundingClientRect();
    const vw = window.innerWidth;
    const vh = window.innerHeight;

    const popupEl = document.querySelector('.cp-popup');
    const popupH  = popupEl ? popupEl.getBoundingClientRect().height : _estimatedPopupH;
    const w       = Math.min(popupW, vw - 2 * MARGIN);

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
    if (left < MARGIN)          left = MARGIN;

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
            Math.max(0, Math.min(1, (cy - r.top)  / r.height)));
    };
    const onMM = e => { if (_cpSvActive) onMove(e.clientX, e.clientY); };
    const onTM = e => { if (_cpSvActive && e.touches.length) { e.preventDefault(); onMove(e.touches[0].clientX, e.touches[0].clientY); } };
    const stop = () => {
        _cpSvActive = false;
        document.removeEventListener('mousemove', onMM);
        document.removeEventListener('mouseup',   stop);
        document.removeEventListener('touchmove', onTM);
        document.removeEventListener('touchend',  stop);
    };
    document.addEventListener('mousemove', onMM);
    document.addEventListener('mouseup',   stop);
    document.addEventListener('touchmove', onTM, { passive: false });
    document.addEventListener('touchend',  stop);
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
        document.removeEventListener('mouseup',   stop);
        document.removeEventListener('touchmove', onTM);
        document.removeEventListener('touchend',  stop);
    };
    document.addEventListener('mousemove', onMM);
    document.addEventListener('mouseup',   stop);
    document.addEventListener('touchmove', onTM, { passive: false });
    document.addEventListener('touchend',  stop);
};

window.cpStopDrag = function () { _cpSvActive = false; _cpHueActive = false; };

window.cpRandomSwatchColor = function () {
    const swatches = [
        '#ef4444','#f97316','#f59e0b','#eab308',
        '#84cc16','#22c55e','#10b981','#14b8a6',
        '#06b6d4','#0ea5e9','#3b82f6','#6366f1',
        '#8b5cf6','#a855f7','#d946ef','#ec4899',
    ];
    return swatches[Math.floor(Math.random() * swatches.length)];
};


// ═══════════════════════════════════════════════════════════════════════
//  ROW DRAG — live visual reorder + scroll-aware
//
//  FIX 1: pointerStartY was initialized to null and the first pointermove
//  was wasted just capturing the start position. For short drags, toIndex
//  stayed at fromIndex → C# early-returned without saving anything.
//  Solution: capture dragStartClientY immediately from the dragged row's
//  bounding rect centre, so every move event contributes to positioning.
//
//  FIX 2: The ended guard ensures OnDragComplete fires exactly once even
//  when both pointerup and touchend fire for the same finger-lift.
//
//  INDEX SPACE: `best` is an index into `otherRows` (all rows minus the
//  dragged element), which has length N-1. This maps directly to the
//  post-RemoveAt array in C#. C# should NOT decrement toIndex.
// ═══════════════════════════════════════════════════════════════════════

window.startRowDrag = function (dotNet, panel, body, pointerId, fromIndex, rowH, topBoundaryEl) {
    const rows = Array.from(body.querySelectorAll('.clm2-row:not(.clm2-row-new)'));
    if (!rows[fromIndex]) return;

    // ── Top boundary: ghost must never appear above the "new list" row ──
    let minTopPx = 0;
    try {
        if (topBoundaryEl) {
            const boundRect = topBoundaryEl.getBoundingClientRect();
            const bodyRect  = body.getBoundingClientRect();
            minTopPx = boundRect.bottom - bodyRect.top + body.scrollTop;
        }
    } catch (e) { /* ignore */ }

    const dragged  = rows[fromIndex];
    const origRect = dragged.getBoundingClientRect();
    const bodyRect = body.getBoundingClientRect();

    const origIndex    = fromIndex;
    const initialBodyY = origRect.top - bodyRect.top + body.scrollTop;

    // FIX 1: capture the drag start Y from the row's centre immediately,
    // so the very first pointermove event already contributes to movement.
    const dragStartClientY = origRect.top + rowH / 2;

    let toIndex = fromIndex;

    // Lift the dragged row out of normal flow
    dragged.style.position  = 'absolute';
    dragged.style.left      = '0';
    dragged.style.right     = '0';
    dragged.style.zIndex    = '100';
    dragged.style.boxShadow = '0 8px 24px rgba(0,0,0,0.5)';
    dragged.style.top       = initialBodyY + 'px';
    body.style.position     = 'relative';

    // Placeholder to maintain layout height while item is floating
    const placeholder         = document.createElement('div');
    placeholder.style.height    = rowH + 'px';
    placeholder.style.flexShrink = '0';
    body.insertBefore(placeholder, dragged);

    // ── Guard: ensure onEnd() fires at most once ──────────────────────
    let ended = false;

    function onEnd() {
        if (ended) return;
        ended = true;

        document.removeEventListener('pointermove',   onPM);
        document.removeEventListener('pointerup',     onPU);
        document.removeEventListener('pointercancel', onPU);
        document.removeEventListener('touchmove',     onTM);
        document.removeEventListener('touchend',      onTE);

        dragged.style.position  = '';
        dragged.style.left      = '';
        dragged.style.right     = '';
        dragged.style.zIndex    = '';
        dragged.style.boxShadow = '';
        dragged.style.top       = '';
        body.style.position     = '';
        placeholder.remove();

        try { dragged.releasePointerCapture && dragged.releasePointerCapture(pointerId); } catch (_) {}

        // Notify Blazor — called exactly once
        dotNet.invokeMethodAsync('OnDragComplete', origIndex, toIndex);
    }

    function onMove(e) {
        const clientY = e.touches ? e.touches[0].clientY : e.clientY;

        // FIX 1: delta from the captured start position, applied to initial body position
        const delta  = clientY - dragStartClientY;
        const newTop = Math.max(minTopPx, initialBodyY + delta);
        dragged.style.top = newTop + 'px';

        // Ghost centre in body-scroll space
        const centreY = newTop + rowH / 2;

        // Fresh query so placeholder-shifted offsetTops are accurate
        const otherRows = Array.from(body.querySelectorAll('.clm2-row:not(.clm2-row-new)'))
                               .filter(r => r !== dragged);

        // Count how many rows have their midpoint above our ghost centre.
        // `best` is an index into otherRows (size N-1), which maps directly
        // to the post-RemoveAt insertion index expected by C#.
        let best = 0;
        for (let i = 0; i < otherRows.length; i++) {
            if (centreY > otherRows[i].offsetTop + rowH / 2) best = i + 1;
        }
        toIndex = best;

        // Move placeholder to the target drop position
        const insertBefore = otherRows[best] || null;
        if (insertBefore) {
            body.insertBefore(placeholder, insertBefore);
        } else {
            const lastRow = otherRows[otherRows.length - 1];
            if (lastRow && lastRow.nextSibling) {
                body.insertBefore(placeholder, lastRow.nextSibling);
            } else {
                body.appendChild(placeholder);
            }
        }
    }

    const onPM = e => onMove(e);
    const onPU = () => onEnd();
    const onTM = e => { if (e.touches.length) { e.preventDefault(); onMove(e); } };
    const onTE = () => onEnd();

    document.addEventListener('pointermove',   onPM);
    document.addEventListener('pointerup',     onPU);
    document.addEventListener('pointercancel', onPU);
    document.addEventListener('touchmove',     onTM, { passive: false });
    document.addEventListener('touchend',      onTE);

    try { dragged.setPointerCapture(pointerId); } catch (_) {}
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
                document.removeEventListener('touchend',    restore);
                document.removeEventListener('touchcancel', restore);
            };
            document.addEventListener('touchend',    restore, { once: true });
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