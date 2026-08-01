(function () {
    'use strict';

    var lottieInstances = {};
    var toastQueue = [];
    var isProcessing = false;

    function initSuccessLottie(container) {
        if (!container || lottieInstances[container.id]) return;

        var url = container.getAttribute('data-lottie-url');
        if (!url || typeof lottie === 'undefined') return;

        var anim = lottie.loadAnimation({
            container: container,
            renderer: 'svg',
            loop: false,
            autoplay: false,
            path: url
        });

        lottieInstances[container.id] = anim;
        return anim;
    }

    function playSuccessAnimation(container) {
        var anim = initSuccessLottie(container);
        if (!anim) return;

        anim.goToAndStop(0, true);
        anim.play();
    }

    window.playSuccessAnimation = playSuccessAnimation;

    function createBadgeToast(title, body, rarity) {
        var toast = document.createElement('div');
        toast.className = 'badge-toast';
        toast.setAttribute('role', 'alert');
        toast.setAttribute('aria-live', 'assertive');

        var lottieId = 'success-lottie-toast-' + Math.random().toString(36).slice(2, 9);
        var rarityClass = rarity ? 'rarity-' + rarity.toLowerCase() : 'rarity-common';

        toast.innerHTML =
            '<div class="toast-header">' +
                '<div class="success-lottie" id="' + lottieId + '" data-lottie-url="/lottie/success.json"></div>' +
                '<div class="toast-title">' + escapeHtml(title) + '</div>' +
            '</div>' +
            '<div class="toast-body">' + escapeHtml(body) + '</div>' +
            '<div class="toast-badge ' + rarityClass + '">' + escapeHtml(rarity || 'Common') + '</div>';

        document.body.appendChild(toast);

        var lottieContainer = toast.querySelector('#' + lottieId);
        if (lottieContainer) {
            playSuccessAnimation(lottieContainer);
        }

        setTimeout(function () {
            toast.classList.add('leaving');
            setTimeout(function () {
                if (toast.parentNode) {
                    toast.parentNode.removeChild(toast);
                }
            }, 400);
        }, 4000);
    }

    function escapeHtml(text) {
        var div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function processBadgeQueue() {
        if (isProcessing || toastQueue.length === 0) return;
        isProcessing = true;

        var item = toastQueue.shift();
        createBadgeToast(item.title, item.body, item.rarity);

        setTimeout(function () {
            isProcessing = false;
            processBadgeQueue();
        }, 500);
    }

    window.showSuccessToast = function (title, body, rarity) {
        toastQueue.push({ title: title, body: body, rarity: rarity });
        processBadgeQueue();
    };

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('.success-lottie').forEach(function (el) {
            if (!el.id) {
                el.id = 'success-lottie-' + Math.random().toString(36).slice(2, 9);
            }
            initSuccessLottie(el);
        });

        var newBadges = window.__egyxploreNewBadges;
        var newBadgesIcon = window.__egyxploreNewBadgesIcon;
        if (newBadges && newBadges.length > 0) {
            newBadges.forEach(function (badge, index) {
                setTimeout(function () {
                    var name = typeof badge === 'object' ? (badge.name || badge) : badge;
                    var description = typeof badge === 'object' ? (badge.description || 'You earned a new badge!') : 'You earned a new badge!';
                    var rarity = typeof badge === 'object' ? (badge.rarity || 'Common') : 'Common';
                    var icon = newBadgesIcon && newBadgesIcon[index] ? newBadgesIcon[index] : '';

                    window.showSuccessToast(name, description, rarity);
                }, index * 600);
            });
        }
    });
})();
