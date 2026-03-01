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

window.startRowDrag = function (dotnetRef, panelEl, pointerId, dragIdx, rowHeight) {
    const rows = Array.from(
        panelEl.querySelectorAll('.clm-body .clm2-row:not(.clm2-row-new)')
    );
    if (!rows[dragIdx]) return;

    const dragRow   = rows[dragIdx];
    let   targetIdx = dragIdx;
    let   startY    = null;   // calibrated on first move so there is no jump

    // Capture the pointer on the dragged row so pointermove / pointerup keep
    // firing even when the pointer moves off the element quickly.
    dragRow.setPointerCapture(pointerId);

    dragRow.style.transition = 'none';
    dragRow.style.zIndex     = '100';
    dragRow.style.boxShadow  = '0 8px 32px rgba(0,0,0,0.55)';
    dragRow.style.opacity    = '0.96';

    function applyShifts() {
        rows.forEach((row, i) => {
            if (i === dragIdx) return;
            row.style.transition = 'transform 0.18s cubic-bezier(0.25,0.46,0.45,0.94)';
            if (dragIdx < targetIdx && i > dragIdx && i <= targetIdx) {
                row.style.transform = `translateY(${-rowHeight}px)`;
            } else if (dragIdx > targetIdx && i >= targetIdx && i < dragIdx) {
                row.style.transform = `translateY(${rowHeight}px)`;
            } else {
                row.style.transform = 'translateY(0px)';
            }
        });
    }

    function onMove(e) {
        if (startY === null) startY = e.clientY;
        const dy = e.clientY - startY;

        // The dragged row follows the pointer exactly, no transition.
        dragRow.style.transform = `translateY(${dy}px)`;

        // Recalculate the landing slot.
        const raw     = dragIdx + dy / rowHeight;
        const clamped = Math.max(0, Math.min(rows.length - 1, Math.round(raw)));
        if (clamped !== targetIdx) {
            targetIdx = clamped;
            applyShifts();
        }
    }

    function cleanup() {
        dragRow.removeEventListener('pointermove',   onMove);
        dragRow.removeEventListener('pointerup',     onUp);
        dragRow.removeEventListener('pointercancel', onCancel);
        rows.forEach(row => {
            row.style.transform  = '';
            row.style.transition = '';
            row.style.zIndex     = '';
            row.style.boxShadow  = '';
            row.style.opacity    = '';
        });
    }

    function onUp() {
        cleanup();
        dotnetRef.invokeMethodAsync('OnDragComplete', dragIdx, targetIdx);
    }

    function onCancel() {
        cleanup();
        dotnetRef.invokeMethodAsync('OnDragComplete', dragIdx, dragIdx);
    }

    dragRow.addEventListener('pointermove',   onMove);
    dragRow.addEventListener('pointerup',     onUp);
    dragRow.addEventListener('pointercancel', onCancel);
};


// ─────────────────────────────────────────────────────────────────────────────
//  JSCOLORPICKER  (jscolorpicker.com, button style, no alpha)
//
//  showClmColorPicker(anchorEl, currentColor, dotnetRef)
//    • Loads the library once (ES module from jscolorpicker.com CDN).
//    • Opens the picker dialog anchored ABOVE the swatch element.
//    • prompt() returns a Promise — resolves when the user picks or cancels.
//    • Calls dotnetRef.OnColorSelected(hexString) on confirmation.
// ─────────────────────────────────────────────────────────────────────────────
let _clmPickerClass = null;

async function loadJsColorPicker() {
    if (_clmPickerClass) return _clmPickerClass;

    // Base stylesheet
    if (!document.getElementById('jscp-stylesheet')) {
        const link = document.createElement('link');
        link.id   = 'jscp-stylesheet';
        link.rel  = 'stylesheet';
        link.href = 'https://www.jscolorpicker.com/css/colorpicker.min.css';
        document.head.appendChild(link);
    }

    // Hide the alpha slider row
    if (!document.getElementById('jscp-no-alpha')) {
        const style = document.createElement('style');
        style.id    = 'jscp-no-alpha';
        style.textContent = `
            .cp-alpha-group,
            .cp-alpha-slider,
            [class*="cp-alpha"] { display: none !important; }
        `;
        document.head.appendChild(style);
    }

    // Match the app's dark theme
    if (!document.documentElement.hasAttribute('data-cp-theme')) {
        document.documentElement.setAttribute('data-cp-theme', 'dark');
    }

    const mod       = await import('https://www.jscolorpicker.com/js/colorpicker.min.js');
    _clmPickerClass = mod.default;
    return _clmPickerClass;
}

window.showClmColorPicker = async function (anchorEl, currentColor, dotnetRef) {
    if (!anchorEl) return;

    let ColorPicker;
    try {
        ColorPicker = await loadJsColorPicker();
    } catch (err) {
        console.error('[AniScroll] Failed to load jscolorpicker:', err);
        return;
    }

    // headless: no toggle rendered; dialogPlacement: dialog appears above anchor.
    // prompt(true) opens dialog, resolves Promise when closed, then destroys instance.
    const picker = new ColorPicker(anchorEl, {
        headless        : true,
        dialogPlacement : 'top',
        color           : currentColor || null,
    });

    const color = await picker.prompt(true);

    if (color !== null && color !== undefined) {
        dotnetRef.invokeMethodAsync('OnColorSelected', color.string('hex'));
    }
};


// ─────────────────────────────────────────────────────────────────────────────
//  Misc helpers used elsewhere in the app
// ─────────────────────────────────────────────────────────────────────────────

window.getElementBoundingRect = function (el) {
    const r = el.getBoundingClientRect();
    return {
        top: r.top, bottom: r.bottom, left: r.left, right: r.right,
        width: r.width, height: r.height,
        viewportWidth: window.innerWidth, viewportHeight: window.innerHeight
    };
};

// ─── Color Picker helpers ────────────────────────────────────────────────────
// Add these functions to your existing JS file (e.g. wwwroot/js/app.js)

(function () {

    var _cpDrag = null; // active drag state

    // Get bounding rect of an element
    window.cpGetRect = function (el) {
        var r = el.getBoundingClientRect();
        return { Left: r.left, Top: r.top, Width: r.width, Height: r.height };
    };

    // Position the color picker popup relative to the anchor swatch.
    // Appears BELOW if there is room, otherwise ABOVE.
    // popupW / popupH = expected size of the popup (px)
    window.cpGetPosition = function (anchor, popupW, popupH) {
        var r   = anchor.getBoundingClientRect();
        var vw  = window.innerWidth;
        var vh  = window.innerHeight;
        var gap = 6; // px between swatch and popup

        var left = r.left;
        // Clamp horizontally so popup doesn't overflow viewport
        if (left + popupW > vw - 8) left = vw - popupW - 8;
        if (left < 8) left = 8;

        // Try below first
        var top = r.bottom + gap;
        if (top + popupH > vh - 8) {
            // Not enough room below → place above
            top = r.top - popupH - gap;
        }
        if (top < 8) top = 8;

        return { Left: left, Top: top };
    };

    // Start drag tracking for the SV square.
    // Attaches document-level pointermove/pointerup, calls dotnet.invokeMethod on each move.
    window.cpStartSvDrag = function (svEl, dotnet) {
        _cpStopDrag(); // cancel any existing drag

        var r = svEl.getBoundingClientRect();

        function onMove(e) {
            var pctX = Math.max(0, Math.min(1, (e.clientX - r.left) / r.width));
            var pctY = Math.max(0, Math.min(1, (e.clientY - r.top)  / r.height));
            dotnet.invokeMethodAsync('OnSvDrag', pctX, pctY);
        }
        function onUp() { _cpStopDrag(); }

        document.addEventListener('pointermove', onMove);
        document.addEventListener('pointerup',   onUp);
        document.addEventListener('pointercancel', onUp);

        _cpDrag = {
            remove: function () {
                document.removeEventListener('pointermove', onMove);
                document.removeEventListener('pointerup',   onUp);
                document.removeEventListener('pointercancel', onUp);
            }
        };
    };

    // Start drag tracking for the Hue strip.
    window.cpStartHueDrag = function (hueEl, dotnet) {
        _cpStopDrag();

        var r = hueEl.getBoundingClientRect();

        function onMove(e) {
            var pctX = Math.max(0, Math.min(1, (e.clientX - r.left) / r.width));
            dotnet.invokeMethodAsync('OnHueDrag', pctX);
        }
        function onUp() { _cpStopDrag(); }

        document.addEventListener('pointermove', onMove);
        document.addEventListener('pointerup',   onUp);
        document.addEventListener('pointercancel', onUp);

        _cpDrag = {
            remove: function () {
                document.removeEventListener('pointermove', onMove);
                document.removeEventListener('pointerup',   onUp);
                document.removeEventListener('pointercancel', onUp);
            }
        };
    };

    // Stop any active drag (called on pointerup or when picker closes)
    function _cpStopDrag() {
        if (_cpDrag) { _cpDrag.remove(); _cpDrag = null; }
    }
    window.cpStopDrag = _cpStopDrag;

})();

// ═══════════════════════════════════════════════════════════════════════
//  COLOR PICKER helpers
// ═══════════════════════════════════════════════════════════════════════

window.cpGetRect = function (el) {
    const r = el.getBoundingClientRect();
    return { left: r.left, top: r.top, width: r.width, height: r.height };
};

/**
 * Position the color picker popup near an anchor element.
 *
 * Strategy:
 *  1. Measure anchor + viewport
 *  2. Prefer BELOW the anchor (gap of 8px)
 *  3. If not enough room below → place ABOVE (gap of 8px above the anchor)
 *  4. Horizontally: align left edge with anchor, but clamp to stay inside
 *     viewport with a 12px safety margin on all sides.
 *  5. On very small screens (mobile) where even above/below doesn't fit well,
 *     center horizontally and use the position with more available space.
 *
 * @param {Element} anchorEl   - the swatch / button that was clicked
 * @param {number}  popupW     - expected popup width  (px)
 * @param {number}  popupH     - expected popup height (px)
 */
window.cpGetPosition = function (anchorEl, popupW, popupH) {
    const MARGIN = 12;   // min gap from viewport edges
    const GAP    = 8;    // gap between anchor and popup

    const a  = anchorEl.getBoundingClientRect();
    const vw = window.innerWidth;
    const vh = window.innerHeight;

    // Clamp popup width to available viewport
    const w = Math.min(popupW, vw - 2 * MARGIN);

    // ── Vertical: prefer below, fall back to above ──────────────────────
    const spaceBelow = vh - a.bottom - GAP;
    const spaceAbove = a.top - GAP;

    let top;
    if (spaceBelow >= popupH || spaceBelow >= spaceAbove) {
        // Place below anchor
        top = a.bottom + GAP;
        // If it overflows bottom, push it up (but never above margin)
        if (top + popupH > vh - MARGIN) {
            top = Math.max(MARGIN, vh - MARGIN - popupH);
        }
    } else {
        // Place above anchor
        top = a.top - GAP - popupH;
        // If it overflows top, push it down (but never below margin)
        if (top < MARGIN) {
            top = MARGIN;
        }
    }

    // ── Horizontal: left-align with anchor, clamp to viewport ──────────
    let left = a.left;
    // Shift left if it overflows right edge
    if (left + w > vw - MARGIN) {
        left = vw - MARGIN - w;
    }
    // Shift right if it overflows left edge
    if (left < MARGIN) {
        left = MARGIN;
    }

    return { left, top };
};

// ── SV drag ──────────────────────────────────────────────────────────────────
let _cpSvDotNet  = null;
let _cpSvEl      = null;
let _cpSvActive  = false;

window.cpStartSvDrag = function (svEl, dotNet) {
    _cpSvEl     = svEl;
    _cpSvDotNet = dotNet;
    _cpSvActive = true;

    const onMove = (cx, cy) => {
        const r = _cpSvEl.getBoundingClientRect();
        const px = Math.max(0, Math.min(1, (cx - r.left) / r.width));
        const py = Math.max(0, Math.min(1, (cy - r.top)  / r.height));
        _cpSvDotNet.invokeMethodAsync('OnSvDrag', px, py);
    };

    const onMouseMove = e => { if (_cpSvActive) onMove(e.clientX, e.clientY); };
    const onTouchMove = e => { if (_cpSvActive && e.touches.length) { e.preventDefault(); onMove(e.touches[0].clientX, e.touches[0].clientY); } };
    const stop = () => { _cpSvActive = false; document.removeEventListener('mousemove', onMouseMove); document.removeEventListener('mouseup', stop); document.removeEventListener('touchmove', onTouchMove); document.removeEventListener('touchend', stop); };

    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup',   stop);
    document.addEventListener('touchmove', onTouchMove, { passive: false });
    document.addEventListener('touchend',  stop);
};

// ── Hue drag ─────────────────────────────────────────────────────────────────
let _cpHueDotNet = null;
let _cpHueEl     = null;
let _cpHueActive = false;

window.cpStartHueDrag = function (hueEl, dotNet) {
    _cpHueEl     = hueEl;
    _cpHueDotNet = dotNet;
    _cpHueActive = true;

    const onMove = cx => {
        const r  = _cpHueEl.getBoundingClientRect();
        const px = Math.max(0, Math.min(1, (cx - r.left) / r.width));
        _cpHueDotNet.invokeMethodAsync('OnHueDrag', px);
    };

    const onMouseMove = e => { if (_cpHueActive) onMove(e.clientX); };
    const onTouchMove = e => { if (_cpHueActive && e.touches.length) { e.preventDefault(); onMove(e.touches[0].clientX); } };
    const stop = () => { _cpHueActive = false; document.removeEventListener('mousemove', onMouseMove); document.removeEventListener('mouseup', stop); document.removeEventListener('touchmove', onTouchMove); document.removeEventListener('touchend', stop); };

    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup',   stop);
    document.addEventListener('touchmove', onTouchMove, { passive: false });
    document.addEventListener('touchend',  stop);
};

window.cpStopDrag = function () {
    _cpSvActive  = false;
    _cpHueActive = false;
};


// ═══════════════════════════════════════════════════════════════════════
//  ROW DRAG (CustomListManager) — scroll-aware
// ═══════════════════════════════════════════════════════════════════════

/**
 * Drag-to-reorder rows inside a scrollable container.
 *
 * Changes vs original:
 *  - Accepts a `bodyEl` parameter (the scrollable .clm-body div)
 *  - All Y calculations use clientY (viewport-relative) so they work
 *    regardless of how far the list has been scrolled
 *  - Auto-scrolls the body when the ghost is dragged near the top or bottom
 *  - Calls OnDragMove(overIndex) on every move for live visual feedback
 *
 * @param {DotNetObjectReference} dotNet
 * @param {Element} panelEl   - .clm-panel  (used only as context, kept for compat)
 * @param {Element} bodyEl    - .clm-body   (the scrollable container)
 * @param {number}  pointerId
 * @param {number}  fromIndex
 * @param {number}  rowH      - row height in px (ROW_H constant)
 */
window.startRowDrag = function (dotNet, panelEl, bodyEl, pointerId, fromIndex, rowH) {
    // ── Collect all row elements inside bodyEl ───────────────────────────────
    const getRows = () => Array.from(bodyEl.querySelectorAll('.clm2-row[data-row-index]'));

    const rows = getRows();
    if (!rows.length) return;

    // ── Identify the dragged row & its starting position ─────────────────────
    const draggedEl = rows.find(r => parseInt(r.dataset.rowIndex) === fromIndex);
    if (!draggedEl) return;

    const draggedRect = draggedEl.getBoundingClientRect();

    // ── Create a floating ghost clone ────────────────────────────────────────
    const ghost = draggedEl.cloneNode(true);
    ghost.style.cssText = `
        position: fixed;
        left: ${draggedRect.left}px;
        top:  ${draggedRect.top}px;
        width: ${draggedRect.width}px;
        height: ${draggedRect.height}px;
        z-index: 99999;
        pointer-events: none;
        opacity: 0.92;
        box-shadow: 0 8px 32px rgba(0,0,0,0.6);
        border-radius: 10px;
        background: #2a2a3a;
        transition: none;
    `;
    document.body.appendChild(ghost);

    // Style dragged row as placeholder
    draggedEl.style.opacity    = '0.3';
    draggedEl.style.background = 'rgba(255,255,255,0.04)';

    // Offset from pointer to ghost top
    let startClientY = 0;
    // We'll receive the first move event to init startClientY from the stored pointer
    let lastClientY  = draggedRect.top + draggedRect.height / 2;
    let currentOver  = fromIndex;

    // Auto-scroll state
    let scrollRaf = null;
    const SCROLL_ZONE = 60; // px from edge to start auto-scrolling
    const SCROLL_SPEED = 6; // px per frame

    const autoScroll = () => {
        const bodyRect = bodyEl.getBoundingClientRect();
        const distTop    = lastClientY - bodyRect.top;
        const distBottom = bodyRect.bottom - lastClientY;

        if (distTop < SCROLL_ZONE && bodyEl.scrollTop > 0) {
            bodyEl.scrollTop -= SCROLL_SPEED * (1 - distTop / SCROLL_ZONE);
        } else if (distBottom < SCROLL_ZONE && bodyEl.scrollTop < bodyEl.scrollHeight - bodyEl.clientHeight) {
            bodyEl.scrollTop += SCROLL_SPEED * (1 - distBottom / SCROLL_ZONE);
        }

        scrollRaf = requestAnimationFrame(autoScroll);
    };
    scrollRaf = requestAnimationFrame(autoScroll);

    // ── Move handler ─────────────────────────────────────────────────────────
    const onMove = (clientX, clientY) => {
        lastClientY = clientY;

        // Move ghost
        ghost.style.top = `${clientY - rowH / 2}px`;

        // Compute which row we're hovering over using *viewport* Y
        const bodyRect = bodyEl.getBoundingClientRect();
        const relY     = clientY - bodyRect.top + bodyEl.scrollTop;
        // Skip the "new list" row at top (it's not a sortable row)
        const newRowH  = bodyEl.querySelector('.clm2-row-new')?.offsetHeight ?? rowH;
        const adjusted = relY - newRowH;

        let over = Math.floor(adjusted / rowH);
        over = Math.max(0, Math.min(rows.length - 1 - 1 /* skip new-row */, over));

        if (over !== currentOver) {
            currentOver = over;
            dotNet.invokeMethodAsync('OnDragMove', over);
        }
    };

    // ── End handler ──────────────────────────────────────────────────────────
    const onEnd = () => {
        cancelAnimationFrame(scrollRaf);

        document.removeEventListener('pointermove', onPointerMove);
        document.removeEventListener('pointerup',   onPointerUp);
        document.removeEventListener('pointercancel', onPointerUp);
        document.removeEventListener('touchmove',   onTouchMove);
        document.removeEventListener('touchend',    onTouchEnd);

        ghost.remove();
        draggedEl.style.opacity    = '';
        draggedEl.style.background = '';

        dotNet.invokeMethodAsync('OnDragComplete', fromIndex, currentOver);
    };

    // ── Event listeners ──────────────────────────────────────────────────────
    const onPointerMove   = e => onMove(e.clientX, e.clientY);
    const onPointerUp     = () => onEnd();
    const onTouchMove     = e => { if (e.touches.length) { e.preventDefault(); onMove(e.touches[0].clientX, e.touches[0].clientY); } };
    const onTouchEnd      = () => onEnd();

    document.addEventListener('pointermove',   onPointerMove);
    document.addEventListener('pointerup',     onPointerUp);
    document.addEventListener('pointercancel', onPointerUp);
    document.addEventListener('touchmove',     onTouchMove,  { passive: false });
    document.addEventListener('touchend',      onTouchEnd);
};


// ═══════════════════════════════════════════════════════════════════════
//  Other existing helpers (unchanged — kept for compatibility)
// ═══════════════════════════════════════════════════════════════════════

window.getElementBounds = function (el) {
    const r = el.getBoundingClientRect();
    return { left: r.left, width: r.width };
};

window.capturePointer = function (el, pointerId) {
    try { el.setPointerCapture(pointerId); } catch (e) { }
};

window.setBarWidth = function (containerEl, selector, pct, color) {
    const fill = containerEl.querySelector(selector);
    if (!fill) return;
    fill.style.width = pct.toFixed(1) + '%';
    if (color) fill.style.background = color;
};

window.setInputValue = function (el, value) {
    if (!el) return;
    const nativeInputValueSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
    nativeInputValueSetter.call(el, value);
    el.dispatchEvent(new Event('input', { bubbles: true }));
};

window.setAndSelectInput = function (el, value) {
    if (!el) return;
    window.setInputValue(el, value);
    el.select();
};

window.selectInputContent = function (el) {
    if (!el) return;
    el.select();
};

window.isModalScrollAtTop = function (el) {
    return !el || el.scrollTop <= 2;
};

window.focusElement = function (el) {
    if (el) el.focus();
};

window.getViewportHeight = function () {
    return window.visualViewport ? window.visualViewport.height : window.innerHeight;
};

window.preloadImages = function (urls) {
    urls.forEach(url => {
        const img = new Image();
        img.src = url;
    });
};