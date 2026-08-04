// Floating AI assistant: premium draggable, resizable, stateful widget.
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var btn = document.getElementById('aiAssistantBtn');
        var panel = document.getElementById('aiAssistantPanel');
        var closeBtn = panel ? panel.querySelector('.ai-widget-close') : null;
        var messagesEl = document.getElementById('aiChatMessages');
        var form = document.getElementById('aiChatForm');
        var input = document.getElementById('aiChatInput');
        var sendBtn = document.getElementById('aiChatSendBtn');

        if (!btn || !panel) return;

        // ============================================================
        // Configuration
        // ============================================================
        var STORAGE_KEY_POS = 'aiWidgetPosition';
        var STORAGE_KEY_SIZE = 'aiWidgetSize';
        var STORAGE_KEY_OPEN = 'aiWidgetOpen';
        var STORAGE_KEY_VERSION = 'aiWidgetVersion';
        var STORAGE_KEY_PANEL_POS = 'aiWidgetPanelPosition';
        var CURRENT_VERSION = 2;

        var SNAP_THRESHOLD = 80;
        var SNAP_MARGIN = 20;
        var DEFAULT_BOTTOM = 24;
        var DEFAULT_RIGHT = 24;
        var DRAG_THRESHOLD = 4;

        var MIN_WIDTH = 340;
        var MIN_HEIGHT = 440;
        var MAX_WIDTH_RATIO = 0.90;
        var MAX_HEIGHT_RATIO = 0.92;

        // ============================================================
        // Data attributes (preserved from original)
        // ============================================================
        var sendUrl = panel.getAttribute('data-send-url');
        var tripUrlTemplate = panel.getAttribute('data-trip-url-template') || '';
        var thinkingText = panel.getAttribute('data-thinking-text') || 'Thinking…';
        var errorText = panel.getAttribute('data-error-text') || "Sorry, something went wrong. Please try again.";
        var viewTripText = panel.getAttribute('data-view-trip-text') || 'View trip';
        var historyUrl = panel.getAttribute('data-history-url') || '';
        var historySessionUrlTemplate = panel.getAttribute('data-history-session-url-template') || '';
        var historyDeleteUrlTemplate = panel.getAttribute('data-history-delete-url-template') || '';
        var historyEmptyText = panel.getAttribute('data-history-empty-text') || 'No saved conversations yet.';
        var historyErrorText = panel.getAttribute('data-history-error-text') || 'Could not load history.';

        // ============================================================
        // State
        // ============================================================
        var MAX_HISTORY = 12;
        var history = [];
        var sending = false;
        var currentSessionId = null;
        var historyPanel = document.getElementById('aiWidgetHistory');

        var btnRect = { x: 0, y: 0, width: 58, height: 58 };
        var panelRect = { x: 0, y: 0, width: 380, height: 520 };
        var isOpen = false;
        var isDraggingBtn = false;
        var isDraggingPanel = false;
        var isResizing = false;
        var resizeDir = '';
        var dragStart = { x: 0, y: 0 };
        var elemStart = { x: 0, y: 0, width: 0, height: 0 };
        var btnDragOccurred = false;

        // RAF-based smooth update
        var rafId = null;
        var pendingUpdate = null;

        // ============================================================
        // Helpers
        // ============================================================
        function clamp(val, min, max) {
            return Math.max(min, Math.min(max, val));
        }

        function getAntiforgeryToken() {
            var f = document.getElementById('antiforgeryForm');
            var tokenInput = f ? f.querySelector('input[name="__RequestVerificationToken"]') : null;
            return tokenInput ? tokenInput.value : '';
        }

        function escapeHtml(text) {
            var div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }

        function getViewport() {
            return { width: window.innerWidth, height: window.innerHeight };
        }

        // ============================================================
        // RAF Loop for instant-feel dragging
        // ============================================================
        function scheduleUpdate(fn) {
            pendingUpdate = fn;
            if (!rafId) {
                rafId = requestAnimationFrame(flushUpdate);
            }
        }

        function flushUpdate() {
            rafId = null;
            if (pendingUpdate) {
                pendingUpdate();
                pendingUpdate = null;
            }
        }

        // ============================================================
        // Storage
        // ============================================================
        function savePosition(x, y) {
            try { localStorage.setItem(STORAGE_KEY_POS, JSON.stringify({ x: x, y: y })); } catch (e) {}
        }

        function loadPosition() {
            try {
                var raw = localStorage.getItem(STORAGE_KEY_POS);
                if (raw) {
                    var pos = JSON.parse(raw);
                    if (typeof pos.x === 'number' && typeof pos.y === 'number') return pos;
                }
            } catch (e) {}
            return null;
        }

        function saveSize(w, h) {
            try { localStorage.setItem(STORAGE_KEY_SIZE, JSON.stringify({ width: w, height: h })); } catch (e) {}
        }

        function loadSize() {
            try {
                var raw = localStorage.getItem(STORAGE_KEY_SIZE);
                if (raw) {
                    var size = JSON.parse(raw);
                    if (typeof size.width === 'number' && typeof size.height === 'number') return size;
                }
            } catch (e) {}
            return null;
        }

        function savePanelPosition(x, y) {
            try { localStorage.setItem(STORAGE_KEY_PANEL_POS, JSON.stringify({ x: x, y: y })); } catch (e) {}
        }

        function loadPanelPosition() {
            try {
                var raw = localStorage.getItem(STORAGE_KEY_PANEL_POS);
                if (raw) {
                    var pos = JSON.parse(raw);
                    if (typeof pos.x === 'number' && typeof pos.y === 'number') return pos;
                }
            } catch (e) {}
            return null;
        }

        function saveOpenState(open) {
            try { localStorage.setItem(STORAGE_KEY_OPEN, open ? '1' : '0'); } catch (e) {}
        }

        function loadOpenState() {
            try { return localStorage.getItem(STORAGE_KEY_OPEN) === '1'; } catch (e) {}
            return false;
        }

        function initStorageVersion() {
            try {
                var v = localStorage.getItem(STORAGE_KEY_VERSION);
                if (v != CURRENT_VERSION) {
                    localStorage.removeItem(STORAGE_KEY_POS);
                    localStorage.removeItem(STORAGE_KEY_SIZE);
                    localStorage.removeItem(STORAGE_KEY_OPEN);
                    localStorage.removeItem(STORAGE_KEY_PANEL_POS);
                    localStorage.setItem(STORAGE_KEY_VERSION, CURRENT_VERSION);
                }
            } catch (e) {}
        }

        // ============================================================
        // Position / Size Management
        // ============================================================
        function applyBtnPosition(x, y, animate) {
            var vp = getViewport();
            btnRect.x = clamp(x, 0, vp.width - btnRect.width);
            btnRect.y = clamp(y, 0, vp.height - btnRect.height);

            if (animate) {
                btn.style.transition = 'left 0.25s cubic-bezier(0.16, 1, 0.3, 1), top 0.25s cubic-bezier(0.16, 1, 0.3, 1)';
            } else {
                btn.style.transition = 'none';
            }

            btn.style.left = btnRect.x + 'px';
            btn.style.top = btnRect.y + 'px';
            btn.style.right = 'auto';
            btn.style.bottom = 'auto';
        }

        function applyPanelPosition(x, y, animate) {
            var vp = getViewport();
            var pw = panelRect.width;
            var ph = panelRect.height;

            panelRect.x = clamp(x, 0, vp.width - pw);
            panelRect.y = clamp(y, 0, vp.height - ph);

            if (animate) {
                panel.style.transition = 'left 0.25s cubic-bezier(0.16, 1, 0.3, 1), top 0.25s cubic-bezier(0.16, 1, 0.3, 1), opacity 250ms cubic-bezier(0.16, 1, 0.3, 1), transform 250ms cubic-bezier(0.16, 1, 0.3, 1)';
            } else {
                panel.style.transition = 'none';
            }

            panel.style.left = panelRect.x + 'px';
            panel.style.top = panelRect.y + 'px';
            panel.style.right = 'auto';
            panel.style.bottom = 'auto';
        }

        function applyPanelSize(w, h, animate) {
            var vp = getViewport();
            w = clamp(w, MIN_WIDTH, Math.min(vp.width - 16, MAX_WIDTH_RATIO * vp.width));
            h = clamp(h, MIN_HEIGHT, Math.min(vp.height - 16, MAX_HEIGHT_RATIO * vp.height));

            panelRect.width = w;
            panelRect.height = h;

            if (animate) {
                panel.style.transition = 'width 0.15s ease, height 0.15s ease';
            }

            panel.style.width = w + 'px';
            panel.style.height = h + 'px';
        }

        function snapButtonToEdge(x, y) {
            var vp = getViewport();
            var btnCenterX = x + btnRect.width / 2;

            // Snap to nearest side edge (left or right) like Messenger chat heads
            if (btnCenterX < vp.width / 2) {
                x = SNAP_MARGIN;
            } else {
                x = vp.width - btnRect.width - SNAP_MARGIN;
            }

            // Clamp vertical position within safe screen bounds
            y = clamp(y, SNAP_MARGIN, vp.height - btnRect.height - SNAP_MARGIN);

            applyBtnPosition(x, y, true);
            savePosition(x, y);

            if (isOpen) {
                var newPos = calculatePanelPosition();
                applyPanelPosition(newPos.x, newPos.y, true);
                savePanelPosition(newPos.x, newPos.y);
            }
        }

        // Calculate panel position anchored near the button
        function calculatePanelPosition() {
            var vp = getViewport();
            // FIX: Always sync btnRect from actual DOM position — anchors to the real icon location
            var _fabRect = btn.getBoundingClientRect();
            btnRect.x = _fabRect.left;
            btnRect.y = _fabRect.top;
            var bx = btnRect.x;
            var by = btnRect.y;
            var bw = btnRect.width;
            var bh = btnRect.height;
            var pw = panelRect.width;
            var ph = panelRect.height;
            var gap = 10;

            // Determine which quadrant the button is in
            var btnCenterX = bx + bw / 2;
            var btnCenterY = by + bh / 2;
            var isRight = btnCenterX > vp.width / 2;
            var isBottom = btnCenterY > vp.height / 2;

            var px, py;

            // Position panel above or below, aligned to the button edge
            if (isBottom) {
                // Button in bottom — panel opens ABOVE
                py = by - ph - gap;
                if (py < 4) py = 4;
            } else {
                // Button in top — panel opens BELOW
                py = by + bh + gap;
                if (py + ph > vp.height - 4) py = vp.height - ph - 4;
            }

            if (isRight) {
                // Button on right — panel aligns right edge to button right edge
                px = bx + bw - pw;
                if (px < 4) px = 4;
            } else {
                // Button on left — panel aligns left edge to button left edge
                px = bx;
                if (px + pw > vp.width - 4) px = vp.width - pw - 4;
            }

            // Final clamping
            px = clamp(px, 4, vp.width - pw - 4);
            py = clamp(py, 4, vp.height - ph - 4);

            return { x: px, y: py };
        }

        // ============================================================
        // Resize Handle (created dynamically)
        // ============================================================
        var handleElement = null;

        function createResizeHandle() {
            var handle = document.createElement('div');
            handle.className = 'ai-resize-handle-se';
            handle.setAttribute('aria-label', 'Resize window');

            // Three-dot grip icon via SVG
            handle.innerHTML = '<svg width="12" height="12" viewBox="0 0 12 12" fill="currentColor">' +
                '<circle cx="9" cy="3" r="1.2"/>' +
                '<circle cx="5" cy="7" r="1.2"/>' +
                '<circle cx="9" cy="7" r="1.2"/>' +
                '<circle cx="1" cy="11" r="1.2"/>' +
                '<circle cx="5" cy="11" r="1.2"/>' +
                '<circle cx="9" cy="11" r="1.2"/>' +
                '</svg>';

            panel.appendChild(handle);
            handleElement = handle;
        }

        // ============================================================
        // Hamburger Menu
        // ============================================================
        var menuBtn = document.getElementById('aiMenuBtn');
        var menuDropdown = document.getElementById('aiMenuDropdown');

        function setupMenu() {
            if (!menuBtn || !menuDropdown) return;

            menuBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                var isVisible = menuDropdown.classList.contains('ai-menu-visible');
                if (isVisible) {
                    menuDropdown.classList.remove('ai-menu-visible');
                } else {
                    menuDropdown.classList.add('ai-menu-visible');
                }
            });

            // Close menu when clicking elsewhere
            document.addEventListener('click', function (e) {
                if (menuDropdown && !menuBtn.contains(e.target) && !menuDropdown.contains(e.target)) {
                    menuDropdown.classList.remove('ai-menu-visible');
                }
            });

            // Menu actions
            var newChatAction = document.getElementById('aiMenuNewChat');
            if (newChatAction) {
                newChatAction.addEventListener('click', function () {
                    messagesEl.innerHTML = '';
                    history = [];
                    currentSessionId = null;
                    // Re-add welcome message
                    var desc = panel.getAttribute('data-welcome-text') || panel.querySelector('.ai-chat-bubble') ? '' : '';
                    var welcomeWrap = document.createElement('div');
                    welcomeWrap.className = 'ai-chat-msg ai-chat-msg-assistant';
                    var welcomeBubble = document.createElement('div');
                    welcomeBubble.className = 'ai-chat-bubble';
                    welcomeBubble.textContent = panel.getAttribute('data-welcome-text') || 'Hello! How can I help you today?';
                    welcomeWrap.appendChild(welcomeBubble);
                    messagesEl.appendChild(welcomeWrap);
                    menuDropdown.classList.remove('ai-menu-visible');
                    if (input) input.focus();
                });
            }

            var clearChatAction = document.getElementById('aiMenuClearChat');
            if (clearChatAction) {
                clearChatAction.addEventListener('click', function () {
                    messagesEl.innerHTML = '';
                    history = [];
                    menuDropdown.classList.remove('ai-menu-visible');
                    if (input) input.focus();
                });
            }
        }

        // ============================================================
        // Open / Close
        // ============================================================
        function openWidget() {
            isOpen = true;
            panel.classList.add('ai-widget-open');
            panel.setAttribute('aria-hidden', 'false');
            btn.setAttribute('aria-expanded', 'true');

            // FIX: Always anchor panel to the icon's current position.
            // Never reuse a stale saved position — the icon may have moved since last open.
            var pos = calculatePanelPosition();

            // Set position without transition first
            panel.style.transition = 'none';
            panel.style.left = pos.x + 'px';
            panel.style.top = pos.y + 'px';
            panel.style.right = 'auto';
            panel.style.bottom = 'auto';
            panelRect.x = pos.x;
            panelRect.y = pos.y;

            // Calculate transform-origin from button center
            var btnCenterX = btnRect.x + btnRect.width / 2;
            var btnCenterY = btnRect.y + btnRect.height / 2;

            var originX = ((btnCenterX - pos.x) / panelRect.width) * 100;
            var originY = ((btnCenterY - pos.y) / panelRect.height) * 100;
            originX = clamp(originX, 0, 100);
            originY = clamp(originY, 0, 100);

            panel.style.transformOrigin = originX + '% ' + originY + '%';

            // Force reflow before enabling animation
            panel.offsetHeight;

            // Now animate in
            panel.style.transition = 'opacity 250ms cubic-bezier(0.16, 1, 0.3, 1), transform 250ms cubic-bezier(0.16, 1, 0.3, 1)';

            saveOpenState(true);
            savePanelPosition(pos.x, pos.y);

            if (input) input.focus();
        }

        function closeWidget() {
            isOpen = false;
            // Save current panel position before closing
            savePanelPosition(panelRect.x, panelRect.y);
            panel.classList.remove('ai-widget-open');
            panel.setAttribute('aria-hidden', 'true');
            btn.setAttribute('aria-expanded', 'false');
            saveOpenState(false);

            // Close menu if open
            if (menuDropdown) menuDropdown.classList.remove('ai-menu-visible');
        }

        function toggleWidget(e) {
            if (btnDragOccurred) {
                btnDragOccurred = false;
                if (e) e.preventDefault();
                return;
            }
            if (isOpen) {
                closeWidget();
            } else {
                openWidget();
            }
        }

        // ============================================================
        // Dragging: Floating Button (RAF-based for instant response)
        // ============================================================
        function onBtnPointerDown(e) {
            if (e.target.closest('.ai-widget-history-toggle') || e.target.closest('.ai-widget-close')) {
                return;
            }
            if (e.button !== undefined && e.button !== 0) return;

            isDraggingBtn = true;
            btnDragOccurred = false;
            dragStart.x = e.clientX;
            dragStart.y = e.clientY;
            elemStart.x = btnRect.x;
            elemStart.y = btnRect.y;

            // Kill ALL transitions immediately for zero-lag
            btn.style.transition = 'none';
            btn.style.willChange = 'left, top';
            btn.classList.add('is-dragging');
            if (panel && isOpen) {
                panel.style.transition = 'none';
                panel.style.willChange = 'left, top';
                panel.classList.add('is-dragging');
            }

            btn.setPointerCapture(e.pointerId);
            e.preventDefault();
        }

        function onBtnPointerMove(e) {
            if (!isDraggingBtn) return;
            e.preventDefault();

            var clientX = e.clientX;
            var clientY = e.clientY;
            var dx = clientX - dragStart.x;
            var dy = clientY - dragStart.y;

            if (!btnDragOccurred && (Math.abs(dx) > DRAG_THRESHOLD || Math.abs(dy) > DRAG_THRESHOLD)) {
                btnDragOccurred = true;
            }

            // Direct DOM update via RAF for instant feel
            scheduleUpdate(function () {
                var vp = getViewport();
                var newX = clamp(elemStart.x + dx, 0, vp.width - btnRect.width);
                var newY = clamp(elemStart.y + dy, 0, vp.height - btnRect.height);

                btnRect.x = newX;
                btnRect.y = newY;
                btn.style.left = newX + 'px';
                btn.style.top = newY + 'px';

                // Move panel with button if open
                if (isOpen) {
                    var offsetX = panelRect.x - (elemStart.x + (dx - (newX - elemStart.x - dx + dx)));
                    // Simpler: just use the delta
                    var basePanelX = elemStart.panelX !== undefined ? elemStart.panelX : panelRect.x;
                    var basePanelY = elemStart.panelY !== undefined ? elemStart.panelY : panelRect.y;

                    // We need to store initial panel position at drag start
                    var npx = clamp(elemStart.panelStartX + dx, 0, vp.width - panelRect.width);
                    var npy = clamp(elemStart.panelStartY + dy, 0, vp.height - panelRect.height);

                    panelRect.x = npx;
                    panelRect.y = npy;
                    panel.style.left = npx + 'px';
                    panel.style.top = npy + 'px';
                }
            });
        }

        // Override onBtnPointerDown to capture panel start position
        var origOnBtnPointerDown = onBtnPointerDown;
        onBtnPointerDown = function (e) {
            if (e.target.closest('.ai-widget-history-toggle') || e.target.closest('.ai-widget-close')) {
                return;
            }
            if (e.button !== undefined && e.button !== 0) return;

            isDraggingBtn = true;
            btnDragOccurred = false;
            dragStart.x = e.clientX;
            dragStart.y = e.clientY;
            elemStart.x = btnRect.x;
            elemStart.y = btnRect.y;
            elemStart.panelStartX = panelRect.x;
            elemStart.panelStartY = panelRect.y;

            btn.style.transition = 'none';
            btn.style.willChange = 'left, top';
            btn.classList.add('is-dragging');
            if (panel && isOpen) {
                panel.style.transition = 'none';
                panel.style.willChange = 'left, top';
                panel.classList.add('is-dragging');
            }

            btn.setPointerCapture(e.pointerId);
            e.preventDefault();
        };

        function onBtnPointerUp(e) {
            if (!isDraggingBtn) return;
            isDraggingBtn = false;
            btn.style.willChange = '';
            btn.style.transition = '';
            btn.classList.remove('is-dragging');
            if (panel) {
                panel.style.willChange = '';
                panel.classList.remove('is-dragging');
                panel.style.transition = '';
            }
            snapButtonToEdge(btnRect.x, btnRect.y);
            savePosition(btnRect.x, btnRect.y);
            if (isOpen) savePanelPosition(panelRect.x, panelRect.y);
        }

        // ============================================================
        // Dragging: Chat Window (RAF-based)
        // ============================================================
        function onPanelHeaderPointerDown(e) {
            if (!isOpen) return;
            if (e.target.closest('.ai-widget-close') || e.target.closest('.ai-widget-history-toggle') || e.target.closest('.ai-menu-btn') || e.target.closest('.ai-menu-dropdown')) {
                return;
            }
            if (e.target.closest('.ai-resize-handle-se')) return;
            if (e.button !== undefined && e.button !== 0) return;

            e.preventDefault();
            e.stopPropagation();
            isDraggingPanel = true;
            dragStart.x = e.clientX;
            dragStart.y = e.clientY;
            elemStart.x = panelRect.x;
            elemStart.y = panelRect.y;

            panel.classList.add('is-dragging');
            panel.style.transition = 'none';
            panel.style.willChange = 'left, top';
            panel.setPointerCapture(e.pointerId);
        }

        function onPanelPointerMove(e) {
            if (!isDraggingPanel) return;
            e.preventDefault();

            var clientX = e.clientX;
            var clientY = e.clientY;
            var dx = clientX - dragStart.x;
            var dy = clientY - dragStart.y;

            scheduleUpdate(function () {
                var vp = getViewport();
                var newX = clamp(elemStart.x + dx, 0, vp.width - panelRect.width);
                var newY = clamp(elemStart.y + dy, 0, vp.height - panelRect.height);

                panelRect.x = newX;
                panelRect.y = newY;
                panel.style.left = newX + 'px';
                panel.style.top = newY + 'px';
            });
        }

        function onPanelPointerUp(e) {
            if (!isDraggingPanel) return;
            isDraggingPanel = false;
            panel.classList.remove('is-dragging');
            panel.style.willChange = '';
            panel.style.transition = '';
            savePanelPosition(panelRect.x, panelRect.y);
        }

        // ============================================================
        // Resizing (RAF-based)
        // ============================================================
        function onResizePointerDown(e) {
            if (!isOpen) return;
            e.preventDefault();
            e.stopPropagation();

            isResizing = true;
            resizeDir = 'se';
            dragStart.x = e.clientX;
            dragStart.y = e.clientY;
            elemStart.x = panelRect.x;
            elemStart.y = panelRect.y;
            elemStart.width = panelRect.width;
            elemStart.height = panelRect.height;

            panel.classList.add('is-resizing');
            panel.style.transition = 'none';
            panel.style.willChange = 'width, height';
            panel.setPointerCapture(e.pointerId);
        }

        function onResizePointerMove(e) {
            if (!isResizing) return;
            e.preventDefault();

            var clientX = e.clientX;
            var clientY = e.clientY;
            var dx = clientX - dragStart.x;
            var dy = clientY - dragStart.y;

            scheduleUpdate(function () {
                var vp = getViewport();
                var newW = clamp(elemStart.width + dx, MIN_WIDTH, Math.min(vp.width - panelRect.x - 4, MAX_WIDTH_RATIO * vp.width));
                var newH = clamp(elemStart.height + dy, MIN_HEIGHT, Math.min(vp.height - panelRect.y - 4, MAX_HEIGHT_RATIO * vp.height));

                panelRect.width = newW;
                panelRect.height = newH;
                panel.style.width = newW + 'px';
                panel.style.height = newH + 'px';
            });
        }

        function onResizePointerUp(e) {
            if (!isResizing) return;
            isResizing = false;
            panel.classList.remove('is-resizing');
            panel.style.willChange = '';
            panel.style.transition = '';
            saveSize(panelRect.width, panelRect.height);
        }

        // ============================================================
        // Message helpers (preserved from original)
        // ============================================================
        function appendMessage(role, text, options) {
            options = options || {};
            var wrap = document.createElement('div');
            wrap.className = 'ai-chat-msg ai-chat-msg-' + role;

            var bubble = document.createElement('div');
            bubble.className = 'ai-chat-bubble';
            bubble.textContent = text;
            wrap.appendChild(bubble);

            if (options.tripId) {
                var link = document.createElement('a');
                link.className = 'ai-chat-trip-link';
                link.href = tripUrlTemplate.replace('__ID__', options.tripId);
                link.textContent = viewTripText + ' →';
                bubble.appendChild(document.createElement('br'));
                bubble.appendChild(link);
            }

            if (options.photos && options.photos.length) {
                var gallery = document.createElement('div');
                gallery.className = 'ai-chat-photos';
                options.photos.forEach(function (url) {
                    var trimmed = url.trim();
                    if (!trimmed) return;
                    var a = document.createElement('a');
                    a.className = 'ai-chat-photos-a';
                    a.href = trimmed;
                    a.target = '_blank';
                    a.rel = 'noopener';
                    var img = document.createElement('img');
                    img.className = 'ai-chat-photo';
                    img.src = trimmed;
                    img.alt = '';
                    img.loading = 'lazy';
                    a.appendChild(img);
                    gallery.appendChild(a);
                });
                bubble.appendChild(document.createElement('br'));
                bubble.appendChild(gallery);
            }

            messagesEl.appendChild(wrap);
            messagesEl.scrollTop = messagesEl.scrollHeight;
            return wrap;
        }

        function appendTyping() {
            var wrap = document.createElement('div');
            wrap.className = 'ai-chat-msg ai-chat-msg-assistant ai-chat-typing';
            var bubble = document.createElement('div');
            bubble.className = 'ai-chat-bubble';
            bubble.textContent = thinkingText;
            wrap.appendChild(bubble);
            messagesEl.appendChild(wrap);
            messagesEl.scrollTop = messagesEl.scrollHeight;
            return wrap;
        }

        function setSending(isSending) {
            sending = isSending;
            if (input) input.disabled = isSending;
            if (sendBtn) sendBtn.disabled = isSending;
        }

        // ============================================================
        // History (preserved from original)
        // ============================================================
        async function loadHistorySession(id) {
            var url = historySessionUrlTemplate.replace('__ID__', id);
            try {
                var response = await fetch(url, {
                    credentials: 'same-origin',
                    headers: {
                        'RequestVerificationToken': getAntiforgeryToken(),
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });
                if (!response.ok) return;
                var data = await response.json();
                messagesEl.innerHTML = '';
                history = [];
                currentSessionId = data.id;
                if (data.messages && Array.isArray(data.messages)) {
                    data.messages.forEach(function (m) {
                        appendMessage(m.role, m.content);
                        history.push({ role: m.role, content: m.content });
                    });
                }
                panel.classList.remove('ai-widget-history-mode');
                if (historyPanel) historyPanel.hidden = true;
                if (input) input.focus();
            } catch (err) {
                // silently keep current view on error
            }
        }

        // ============================================================
        // Send message (preserved from original)
        // ============================================================
        async function sendMessage(text) {
            appendMessage('user', text);
            history.push({ role: 'user', content: text });

            var typingEl = appendTyping();
            setSending(true);

            var formData = new FormData();
            formData.append('Message', text);
            formData.append('History', JSON.stringify(history.slice(0, -1)));
            formData.append('ChatSessionId', currentSessionId || '');

            try {
                var response = await fetch(sendUrl, {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: {
                        'RequestVerificationToken': getAntiforgeryToken(),
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    body: formData
                });

                typingEl.remove();

                if (!response.ok) {
                    appendMessage('error', errorText);
                    setSending(false);
                    return;
                }

                var data = await response.json();
                var reply = data && data.reply ? data.reply : errorText;

                var extraOpts = {};
                if (data && data.tripSaved) extraOpts.tripId = data.tripPlanId;
                if (data && data.photoUrls && data.photoUrls.length > 0) extraOpts.photos = data.photoUrls;
                appendMessage('assistant', reply, extraOpts);
                history.push({ role: 'assistant', content: reply });

                if (history.length > MAX_HISTORY) {
                    history = history.slice(history.length - MAX_HISTORY);
                }

                if (data && data.chatSessionId) {
                    currentSessionId = data.chatSessionId;
                }
            } catch (err) {
                typingEl.remove();
                appendMessage('error', errorText);
            } finally {
                setSending(false);
                if (input) input.focus();
            }
        }

        // ============================================================
        // Initialization
        // ============================================================
        function init() {
            initStorageVersion();

            var vp = getViewport();
            var savedPos = loadPosition();
            var savedSize = loadSize();
            var wasOpen = loadOpenState();

            // Button position
            if (savedPos) {
                applyBtnPosition(savedPos.x, savedPos.y, false);
            } else {
                btnRect.x = vp.width - btnRect.width - DEFAULT_RIGHT;
                btnRect.y = vp.height - btnRect.height - DEFAULT_BOTTOM;
                applyBtnPosition(btnRect.x, btnRect.y, false);
            }
            // FIX: Re-read actual rendered position after CSS layout so btnRect is always accurate
            var _initDom = btn.getBoundingClientRect();
            btnRect.x = _initDom.left;
            btnRect.y = _initDom.top;

            // Panel size
            if (savedSize) {
                applyPanelSize(savedSize.width, savedSize.height, false);
            } else {
                panelRect.width = Math.min(380, vp.width - 44);
                panelRect.height = Math.min(520, vp.height - 90);
                applyPanelSize(panelRect.width, panelRect.height, false);
            }

            // Resize handle
            createResizeHandle();

            // Menu
            setupMenu();

            // Always start with panel closed on navigation (only floating button visible)
            closeWidget();

            // ============================================================
            // Event listeners
            // ============================================================
            btn.addEventListener('click', function (e) { toggleWidget(e); });

            if (closeBtn) {
                closeBtn.addEventListener('click', closeWidget);
            }

            // Button drag — attach to document for guaranteed capture
            btn.addEventListener('pointerdown', onBtnPointerDown);
            btn.addEventListener('pointermove', onBtnPointerMove);
            btn.addEventListener('pointerup', onBtnPointerUp);
            btn.addEventListener('pointercancel', onBtnPointerUp);
            btn.addEventListener('lostpointercapture', onBtnPointerUp);

            // Panel drag (header)
            var header = panel.querySelector('.ai-widget-header');
            if (header) {
                header.addEventListener('pointerdown', onPanelHeaderPointerDown);
            }
            panel.addEventListener('pointermove', onPanelPointerMove);
            panel.addEventListener('pointerup', onPanelPointerUp);
            panel.addEventListener('pointercancel', onPanelPointerUp);
            panel.addEventListener('lostpointercapture', onPanelPointerUp);

            // Resize
            if (handleElement) {
                handleElement.addEventListener('pointerdown', onResizePointerDown);
            }
            panel.addEventListener('pointermove', onResizePointerMove);
            panel.addEventListener('pointerup', onResizePointerUp);
            panel.addEventListener('pointercancel', onResizePointerUp);
            panel.addEventListener('lostpointercapture', onResizePointerUp);

            // Escape key
            document.addEventListener('keydown', function (e) {
                if (e.key === 'Escape' && isOpen) {
                    closeWidget();
                    btn.focus();
                }
            });

            // History helpers: relative time + Today/Yesterday/Older grouping
            function historyDayDiff(isoDate) {
                var d = new Date(isoDate);
                if (isNaN(d.getTime())) return null;
                var now = new Date();
                var startToday = new Date(now.getFullYear(), now.getMonth(), now.getDate());
                var startOfDay = new Date(d.getFullYear(), d.getMonth(), d.getDate());
                return Math.round((startToday - startOfDay) / 86400000);
            }

            function historyGroupLabel(isoDate) {
                var diff = historyDayDiff(isoDate);
                if (diff === null) return 'Older';
                if (diff === 0) return 'Today';
                if (diff === 1) return 'Yesterday';
                return 'Older';
            }

            function formatHistoryTime(isoDate) {
                var diff = historyDayDiff(isoDate);
                if (diff === null) return '';
                var d = new Date(isoDate);
                if (diff === 0) return d.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
                if (diff === 1) return 'Yesterday';
                if (diff < 7) return d.toLocaleDateString([], { weekday: 'short' });
                return d.toLocaleDateString();
            }

            async function deleteHistorySession(id, itemEl, listEl) {
                if (!historyDeleteUrlTemplate) return;
                try {
                    var url = historyDeleteUrlTemplate.replace('__ID__', id);
                    var response = await fetch(url, {
                        method: 'POST',
                        credentials: 'same-origin',
                        headers: {
                            'RequestVerificationToken': getAntiforgeryToken(),
                            'X-Requested-With': 'XMLHttpRequest'
                        }
                    });
                    if (!response.ok) return;
                    itemEl.remove();
                    if (!listEl.querySelector('.ai-history-item')) {
                        listEl.innerHTML = '<div class="ai-history-empty">' + escapeHtml(historyEmptyText) + '</div>';
                    }
                } catch (err) { /* keep the item on failure */ }
            }

            async function loadHistory() {
                if (!historyUrl) return;
                var listEl = document.getElementById('aiHistoryList');
                listEl.innerHTML = '<div class="ai-history-empty">Loading…</div>';
                panel.classList.add('ai-widget-history-mode');
                if (historyPanel) historyPanel.hidden = false;

                try {
                    var response = await fetch(historyUrl, {
                        credentials: 'same-origin',
                        headers: {
                            'RequestVerificationToken': getAntiforgeryToken(),
                            'X-Requested-With': 'XMLHttpRequest'
                        }
                    });
                    var sessions = await response.json();
                    listEl.innerHTML = '';
                    if (!sessions || sessions.length === 0) {
                        listEl.innerHTML = '<div class="ai-history-empty">' + escapeHtml(historyEmptyText) + '</div>';
                        return;
                    }

                    // Group sessions into Today / Yesterday / Older (server already
                    // returns them newest-first).
                    var groups = {};
                    sessions.forEach(function (s) {
                        var label = historyGroupLabel(s.updatedDate);
                        if (!groups[label]) groups[label] = [];
                        groups[label].push(s);
                    });

                    ['Today', 'Yesterday', 'Older'].forEach(function (label) {
                        if (!groups[label] || groups[label].length === 0) return;
                        var groupEl = document.createElement('div');
                        groupEl.className = 'ai-history-group';
                        var groupTitle = document.createElement('div');
                        groupTitle.className = 'ai-history-group-title';
                        groupTitle.textContent = label;
                        groupEl.appendChild(groupTitle);

                        groups[label].forEach(function (s) {
                            var item = document.createElement('div');
                            item.className = 'ai-history-item';
                            item.setAttribute('tabindex', '0');
                            item.setAttribute('role', 'button');
                            item.setAttribute('aria-label', s.title);

                            var previewHtml = s.preview
                                ? '<div class="ai-history-item-preview">' + escapeHtml(s.preview) + '</div>'
                                : '';
                            var deleteBtnHtml = historyDeleteUrlTemplate
                                ? '<button type="button" class="ai-history-item-delete" aria-label="Delete conversation" title="Delete conversation"><i class="bi bi-trash3"></i></button>'
                                : '';

                            item.innerHTML =
                                '<div class="ai-history-item-main">' +
                                    '<div class="ai-history-item-title">' + escapeHtml(s.title) + '</div>' +
                                    previewHtml +
                                    '<div class="ai-history-item-date">' + escapeHtml(formatHistoryTime(s.updatedDate)) + '</div>' +
                                '</div>' +
                                deleteBtnHtml;

                            item.addEventListener('click', function (e) {
                                if (e.target.closest('.ai-history-item-delete')) return;
                                loadHistorySession(s.id);
                            });
                            item.addEventListener('keydown', function (e) {
                                if (e.key === 'Enter' && !e.target.closest('.ai-history-item-delete')) {
                                    loadHistorySession(s.id);
                                }
                            });

                            var delBtn = item.querySelector('.ai-history-item-delete');
                            if (delBtn) {
                                delBtn.addEventListener('click', function (e) {
                                    e.stopPropagation();
                                    deleteHistorySession(s.id, item, listEl);
                                });
                            }

                            groupEl.appendChild(item);
                        });
                        listEl.appendChild(groupEl);
                    });
                } catch (err) {
                    listEl.innerHTML = '<div class="ai-history-empty">' + escapeHtml(historyErrorText) + '</div>';
                }
            }

            // History button
            var historyBtn = document.getElementById('aiHistoryBtn');
            if (historyBtn) {
                historyBtn.addEventListener('click', loadHistory);
            }

            // History back button
            var historyBackBtn = document.getElementById('aiHistoryBackBtn');
            if (historyBackBtn) {
                historyBackBtn.addEventListener('click', function () {
                    panel.classList.remove('ai-widget-history-mode');
                    if (historyPanel) historyPanel.hidden = true;
                });
            }

            // Form submit
            if (form) {
                form.addEventListener('submit', function (e) {
                    e.preventDefault();
                    if (sending || !input) return;
                    var text = input.value.trim();
                    if (!text) return;
                    input.value = '';
                    sendMessage(text);
                });
            }

            // Save state on unload
            window.addEventListener('beforeunload', function () {
                savePosition(btnRect.x, btnRect.y);
                saveSize(panelRect.width, panelRect.height);
                saveOpenState(false);
                if (isOpen) savePanelPosition(panelRect.x, panelRect.y);
            });

            // Handle viewport resize
            window.addEventListener('resize', function () {
                var newVp = getViewport();

                btnRect.x = clamp(btnRect.x, 0, newVp.width - btnRect.width);
                btnRect.y = clamp(btnRect.y, 0, newVp.height - btnRect.height);
                applyBtnPosition(btnRect.x, btnRect.y, false);
                savePosition(btnRect.x, btnRect.y);

                if (isOpen) {
                    panelRect.x = clamp(panelRect.x, 0, newVp.width - panelRect.width);
                    panelRect.y = clamp(panelRect.y, 0, newVp.height - panelRect.height);
                    applyPanelPosition(panelRect.x, panelRect.y, false);
                    applyPanelSize(panelRect.width, panelRect.height, false);
                    savePanelPosition(panelRect.x, panelRect.y);
                    saveSize(panelRect.width, panelRect.height);
                }
            });
        }

        init();
    });
})();