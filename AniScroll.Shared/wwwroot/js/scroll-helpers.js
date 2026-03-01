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