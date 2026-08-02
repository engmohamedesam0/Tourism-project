(function () {
    'use strict';

    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

    var ANIM_MS = 1600;

    function onFilterIconClick(e) {
        var trigger = e.target.closest('.filter-icon-trigger');
        if (!trigger) return;

        var toggleBtn = trigger.closest('[data-filter-toggle]');
        if (toggleBtn) {
            var panelOpen = toggleBtn.getAttribute('aria-expanded') === 'true' ||
                            toggleBtn.classList.contains('show');
            trigger.classList.toggle('active', !panelOpen);
        } else {
            if (trigger.classList.contains('active')) {
                trigger.classList.remove('active');
            } else {
                trigger.classList.add('active');
                setTimeout(function () {
                    if (trigger.classList.contains('active') && !trigger.classList.contains('hover')) {
                        trigger.classList.remove('active');
                    }
                }, ANIM_MS);
            }
        }
    }

    function onFilterIconTouch(e) {
        var trigger = e.target.closest('.filter-icon-trigger');
        if (!trigger) return;

        var already = trigger.classList.contains('active');
        document.querySelectorAll('.filter-icon-trigger.active').forEach(function (el) {
            el.classList.remove('active');
        });
        if (!already) {
            trigger.classList.add('active');
            setTimeout(function () {
                trigger.classList.remove('active');
            }, ANIM_MS);
        }
    }

    function syncToggleIcons() {
        document.querySelectorAll('[data-filter-toggle]').forEach(function (btn) {
            var icon = btn.querySelector('.filter-icon-trigger');
            if (!icon) return;
            var expanded = btn.getAttribute('aria-expanded') === 'true' || btn.classList.contains('show');
            icon.classList.toggle('active', expanded);
        });
    }

    document.addEventListener('click', onFilterIconClick, true);
    document.addEventListener('touchstart', onFilterIconTouch);

    document.addEventListener('DOMContentLoaded', syncToggleIcons);
    window.addEventListener('load', syncToggleIcons);

    if (typeof MutationObserver !== 'undefined') {
        var observer = new MutationObserver(syncToggleIcons);
        observer.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['aria-expanded', 'class'] });
    }
})();
