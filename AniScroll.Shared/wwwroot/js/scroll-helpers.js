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

/**
 * Position the color picker popup near an anchor element.
 *
 * KEY FIX: instead of trusting the estimated popupH passed from Blazor
 * (which is wrong because the popup hasn't painted yet), we find the
 * actual .cp-popup element in the DOM and measure it directly.
 * The popup must be in the DOM (even off-screen) before this is called.
 *
 * - Prefers BELOW the anchor (gap 8px).
 * - Falls back to ABOVE: BOTTOM of popup = TOP of anchor - gap.
 * - Clamps horizontally within viewport.
 */
window.cpGetPosition = function (anchorEl, popupW, _estimatedPopupH) {
    const MARGIN = 12;
    const GAP    = 8;

    const a  = anchorEl.getBoundingClientRect();
    const vw = window.innerWidth;
    const vh = window.innerHeight;

    // Measure the real rendered popup height instead of the estimate
    const popupEl = document.querySelector('.cp-popup');
    const popupH  = popupEl ? popupEl.getBoundingClientRect().height : _estimatedPopupH;
    const w       = Math.min(popupW, vw - 2 * MARGIN);

    const spaceBelow = vh - a.bottom - GAP;
    const spaceAbove = a.top - GAP;

    let top;
    if (spaceBelow >= popupH || spaceBelow >= spaceAbove) {
        // Place below
        top = a.bottom + GAP;
        if (top + popupH > vh - MARGIN) top = vh - MARGIN - popupH;
    } else {
        // Place above: BOTTOM of popup = TOP of anchor - gap
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

// Random vivid swatch color (used when user adds a list without picking a color)
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
// ═══════════════════════════════════════════════════════════════════════

window.startRowDrag = function (dotNet, panelEl, bodyEl, pointerId, fromIndex, rowH) {
    const allRows = Array.from(bodyEl.querySelectorAll('.clm2-row[data-row-index]'));
    const count   = allRows.length;
    if (!count) return;

    const draggedEl = allRows.find(r => parseInt(r.dataset.rowIndex) === fromIndex);
    if (!draggedEl) return;

    const draggedRect = draggedEl.getBoundingClientRect();

    // Ghost clone
    const ghost = draggedEl.cloneNode(true);
    Object.assign(ghost.style, {
        position:      'fixed',
        left:          draggedRect.left   + 'px',
        top:           draggedRect.top    + 'px',
        width:         draggedRect.width  + 'px',
        height:        draggedRect.height + 'px',
        zIndex:        '99999',
        pointerEvents: 'none',
        opacity:       '0.93',
        boxShadow:     '0 8px 32px rgba(0,0,0,0.55)',
        borderRadius:  '10px',
        background:    '#2a2a3a',
        transition:    'none',
        willChange:    'top',
    });
    document.body.appendChild(ghost);

    // Make dragged row invisible (spacer)
    draggedEl.style.opacity       = '0';
    draggedEl.style.pointerEvents = 'none';

    const halfH = draggedRect.height / 2;
    let targetIndex = fromIndex;
    let lastClientY = draggedRect.top + halfH;

    const newRowEl = bodyEl.querySelector('.clm2-row-new');
    const newRowH  = newRowEl ? newRowEl.offsetHeight : 0;

    const SCROLL_ZONE = 60, SCROLL_SPEED = 8;
    let scrollRaf = null;

    const applyShifts = () => {
        const bodyTopScrolled     = bodyEl.getBoundingClientRect().top - bodyEl.scrollTop;
        const ghostCentreInScroll = lastClientY - bodyTopScrolled;

        let best = fromIndex, bestDist = Infinity;
        for (let i = 0; i < count; i++) {
            const naturalCentre = newRowH + i * rowH + rowH / 2;
            const dist = Math.abs(ghostCentreInScroll - naturalCentre);
            if (dist < bestDist) { bestDist = dist; best = i; }
        }
        targetIndex = best;

        allRows.forEach((row, i) => {
            if (i === fromIndex) { row.style.transform = ''; return; }
            let shift = 0;
            if (fromIndex < targetIndex) {
                if (i > fromIndex && i <= targetIndex) shift = -rowH;
            } else {
                if (i >= targetIndex && i < fromIndex) shift = +rowH;
            }
            row.style.transition = 'transform 0.12s ease';
            row.style.transform  = shift ? `translateY(${shift}px)` : '';
        });
    };

    const scrollTick = () => {
        const br     = bodyEl.getBoundingClientRect();
        const dTop   = lastClientY - br.top;
        const dBot   = br.bottom - lastClientY;
        const maxTop = bodyEl.scrollHeight - bodyEl.clientHeight;
        if (dTop < SCROLL_ZONE && bodyEl.scrollTop > 0) {
            bodyEl.scrollTop -= SCROLL_SPEED * Math.max(0.05, 1 - dTop / SCROLL_ZONE);
            applyShifts();
        } else if (dBot < SCROLL_ZONE && bodyEl.scrollTop < maxTop) {
            bodyEl.scrollTop += SCROLL_SPEED * Math.max(0.05, 1 - dBot / SCROLL_ZONE);
            applyShifts();
        }
        scrollRaf = requestAnimationFrame(scrollTick);
    };
    scrollRaf = requestAnimationFrame(scrollTick);

    const onMove = (clientX, clientY) => {
        lastClientY      = clientY;
        ghost.style.top  = `${clientY - halfH}px`;
        applyShifts();
    };

    const onEnd = () => {
        cancelAnimationFrame(scrollRaf);
        document.removeEventListener('pointermove',   onPM);
        document.removeEventListener('pointerup',     onPU);
        document.removeEventListener('pointercancel', onPU);
        document.removeEventListener('touchmove',     onTM);
        document.removeEventListener('touchend',      onTE);
        allRows.forEach(r => { r.style.transform = ''; r.style.transition = ''; });
        draggedEl.style.opacity       = '';
        draggedEl.style.pointerEvents = '';
        ghost.remove();
        dotNet.invokeMethodAsync('OnDragComplete', fromIndex, targetIndex);
    };

    const onPM = e => onMove(e.clientX, e.clientY);
    const onPU = () => onEnd();
    const onTM = e => { if (e.touches.length) { e.preventDefault(); onMove(e.touches[0].clientX, e.touches[0].clientY); } };
    const onTE = () => onEnd();

    document.addEventListener('pointermove',   onPM);
    document.addEventListener('pointerup',     onPU);
    document.addEventListener('pointercancel', onPU);
    document.addEventListener('touchmove',     onTM, { passive: false });
    document.addEventListener('touchend',      onTE);
};