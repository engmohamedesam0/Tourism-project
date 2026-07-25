// Floating AI assistant: open/close toggle + live chat against /AiChat/Send.
(function () {
    document.addEventListener('DOMContentLoaded', function () {
        var btn = document.getElementById('aiAssistantBtn');
        var panel = document.getElementById('aiAssistantPanel');
        var closeBtn = panel ? panel.querySelector('.ai-widget-close') : null;
        var messagesEl = document.getElementById('aiChatMessages');
        var form = document.getElementById('aiChatForm');
        var input = document.getElementById('aiChatInput');
        var sendBtn = document.getElementById('aiChatSendBtn');

        if (!btn || !panel) return;

        var sendUrl = panel.getAttribute('data-send-url');
        var tripUrlTemplate = panel.getAttribute('data-trip-url-template') || '';
        var thinkingText = panel.getAttribute('data-thinking-text') || 'Thinking…';
        var errorText = panel.getAttribute('data-error-text') || "Sorry, something went wrong. Please try again.";
        var viewTripText = panel.getAttribute('data-view-trip-text') || 'View trip';
        var historyUrl = panel.getAttribute('data-history-url') || '';
        var historySessionUrlTemplate = panel.getAttribute('data-history-session-url-template') || '';
        var historyEmptyText = panel.getAttribute('data-history-empty-text') || 'No saved conversations yet.';
        var historyErrorText = panel.getAttribute('data-history-error-text') || 'Could not load history.';

        var MAX_HISTORY = 12;
        var history = [];
        var sending = false;
        var currentSessionId = null;
        var historyPanel = document.getElementById('aiWidgetHistory');

        function escapeHtml(text) {
            var div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }

        function openWidget() {
            panel.classList.add('ai-widget-open');
            panel.setAttribute('aria-hidden', 'false');
            btn.setAttribute('aria-expanded', 'true');
            if (input) input.focus();
        }

        function closeWidget() {
            panel.classList.remove('ai-widget-open');
            panel.setAttribute('aria-hidden', 'true');
            btn.setAttribute('aria-expanded', 'false');
        }

        btn.addEventListener('click', function () {
            if (panel.classList.contains('ai-widget-open')) {
                closeWidget();
            } else {
                openWidget();
            }
        });

        if (closeBtn) {
            closeBtn.addEventListener('click', closeWidget);
        }

        function getAntiforgeryToken() {
            var f = document.getElementById('antiforgeryForm');
            var tokenInput = f ? f.querySelector('input[name="__RequestVerificationToken"]') : null;
            return tokenInput ? tokenInput.value : '';
        }

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

        async function loadHistorySession(id) {
            var url = historySessionUrlTemplate.replace('__ID__', id);
            try {
                var response = await fetch(url, {
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
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

        var historyBtn = document.getElementById('aiHistoryBtn');
        if (historyBtn) {
            historyBtn.addEventListener('click', async function () {
                if (!historyUrl) return;
                var listEl = document.getElementById('aiHistoryList');
                listEl.innerHTML = '<div class="ai-history-empty">Loading…</div>';
                panel.classList.add('ai-widget-history-mode');
                if (historyPanel) historyPanel.hidden = false;

                try {
                var response = await fetch(historyUrl, {
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                var sessions = await response.json();
                listEl.innerHTML = '';
                if (!sessions || sessions.length === 0) {
                    listEl.innerHTML = '<div class="ai-history-empty">' + escapeHtml(historyEmptyText) + '</div>';
                    return;
                }
                    sessions.forEach(function (s) {
                        var item = document.createElement('div');
                        item.className = 'ai-history-item';
                        item.setAttribute('tabindex', '0');
                        item.setAttribute('role', 'button');
                        var dateStr = new Date(s.updatedDate).toLocaleDateString();
                        item.innerHTML = '<div class="ai-history-item-title">' + escapeHtml(s.title) + '</div>' +
                                         '<div class="ai-history-item-date">' + escapeHtml(dateStr) + '</div>';
                        item.addEventListener('click', function () { loadHistorySession(s.id); });
                        item.addEventListener('keydown', function (e) { if (e.key === 'Enter') loadHistorySession(s.id); });
                        listEl.appendChild(item);
                    });
                } catch (err) {
                    listEl.innerHTML = '<div class="ai-history-empty">' + escapeHtml(historyErrorText) + '</div>';
                }
            });
        }

        var historyBackBtn = document.getElementById('aiHistoryBackBtn');
        if (historyBackBtn) {
            historyBackBtn.addEventListener('click', function () {
                panel.classList.remove('ai-widget-history-mode');
                if (historyPanel) historyPanel.hidden = true;
            });
        }

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

                appendMessage('assistant', reply, data && data.tripSaved ? { tripId: data.tripPlanId } : {});
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
    });
})();
