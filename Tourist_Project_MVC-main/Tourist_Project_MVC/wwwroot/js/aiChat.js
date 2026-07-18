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

        var MAX_HISTORY = 12;
        var history = [];
        var sending = false;

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

        async function sendMessage(text) {
            appendMessage('user', text);
            history.push({ role: 'user', content: text });

            var typingEl = appendTyping();
            setSending(true);

            try {
                var response = await fetch(sendUrl, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': getAntiforgeryToken(),
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    body: JSON.stringify({
                        message: text,
                        history: history.slice(0, -1)
                    })
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
