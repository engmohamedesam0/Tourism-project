(function () {
    'use strict';

    var STORAGE_KEY = 'egyxplore-theme';
    var SYSTEM_DARK = window.matchMedia('(prefers-color-scheme: dark)');
    var toggleBtn = null;
    var svgElement = null;
    var svgDuration = 4.7;
    var svgAnimating = false;

    function getEffectiveTheme() {
        var stored = localStorage.getItem(STORAGE_KEY) || 'system';
        if (stored === 'system') {
            return SYSTEM_DARK.matches ? 'dark' : 'light';
        }
        return stored;
    }

    function getStoredMode() {
        return localStorage.getItem(STORAGE_KEY) || 'system';
    }

    function applyTheme(theme) {
        document.documentElement.setAttribute('data-theme', theme);
    }

    function syncMode(mode) {
        document.documentElement.setAttribute('data-theme-mode', mode);
    }

    function updateToggleTitle(mode) {
        toggleBtn = toggleBtn || document.getElementById('themeToggle');
        if (!toggleBtn) return;
        var effective = getEffectiveTheme();
        var title = effective === 'dark' ? 'Switch to Light mode' : 'Switch to Dark mode';
        toggleBtn.setAttribute('title', title);
        toggleBtn.setAttribute('aria-label', title);
        toggleBtn.setAttribute('aria-checked', effective === 'dark' ? 'true' : 'false');
    }

    function dispatchThemeChange() {
        var effective = getEffectiveTheme();
        var mode = getStoredMode();
        window.dispatchEvent(new CustomEvent('themechange', {
            detail: { theme: effective, mode: mode }
        }));
    }

    function getSvgElement() {
        if (svgElement && document.body.contains(svgElement)) return svgElement;
        var el = document.getElementById('themeToggleSvg');
        if (!el) return null;
        if (el.tagName && el.tagName.toLowerCase() === 'svg') {
            svgElement = el;
        } else if (el.contentDocument && el.contentDocument.documentElement) {
            svgElement = el.contentDocument.documentElement;
        }
        return svgElement;
    }

    function setThemeFrame(theme, animate) {
        var svg = getSvgElement();
        if (!svg) return;
        var targetTime = theme === 'dark' ? 0 : svgDuration * 0.496454;

        if (typeof svg.setCurrentTime !== 'function') return;

        if (animate === false) {
            try {
                if (typeof svg.pauseAnimations === 'function') svg.pauseAnimations();
                svg.setCurrentTime(targetTime);
            } catch (err) {}
            return;
        }

        if (svgAnimating) return;
        svgAnimating = true;

        var startTime = null;
        var startCurrentTime = 0;
        try {
            startCurrentTime = svg.getCurrentTime();
        } catch (e) {
            startCurrentTime = theme === 'dark' ? svgDuration * 0.496454 : 0;
        }
        var duration = 400;

        function step(timestamp) {
            if (!startTime) startTime = timestamp;
            var elapsed = timestamp - startTime;
            var progress = Math.min(elapsed / duration, 1);
            var eased = progress < 0.5
                ? 4 * progress * progress * progress
                : 1 - Math.pow(-2 * progress + 2, 3) / 2;
            var currentTime = startCurrentTime + (targetTime - startCurrentTime) * eased;
            try {
                svg.setCurrentTime(currentTime);
            } catch (e) {}
            if (progress < 1) {
                requestAnimationFrame(step);
            } else {
                try {
                    svg.setCurrentTime(targetTime);
                    if (typeof svg.pauseAnimations === 'function') svg.pauseAnimations();
                } catch (e) {}
                svgAnimating = false;
            }
        }

        try {
            if (typeof svg.unpauseAnimations === 'function') svg.unpauseAnimations();
        } catch (e) {}
        requestAnimationFrame(step);
    }

    function toggle() {
        var current = getStoredMode();
        var effective = getEffectiveTheme();
        var nextMode;
        if (current === 'system') {
            nextMode = effective === 'dark' ? 'light' : 'dark';
        } else {
            nextMode = current === 'dark' ? 'light' : 'dark';
        }
        localStorage.setItem(STORAGE_KEY, nextMode);
        var newEffective = getEffectiveTheme();
        applyTheme(newEffective);
        syncMode(nextMode);
        updateToggleTitle(nextMode);
        dispatchThemeChange();
        setThemeFrame(newEffective, true);
    }

    function init() {
        toggleBtn = document.getElementById('themeToggle');
        var stored = getStoredMode();
        var effective = getEffectiveTheme();
        applyTheme(effective);
        syncMode(stored);
        updateToggleTitle(stored);
        setThemeFrame(effective, false);
    }

    SYSTEM_DARK.addEventListener('change', function () {
        if (getStoredMode() === 'system') {
            var effective = getEffectiveTheme();
            applyTheme(effective);
            syncMode('system');
            updateToggleTitle('system');
            dispatchThemeChange();
            setThemeFrame(effective, true);
        }
    });

    document.addEventListener('click', function (e) {
        var btn = e.target.closest('#themeToggle');
        if (btn) {
            e.preventDefault();
            toggle();
        }
    });

    window.addEventListener('themechange', function (e) {
        var isDark = e.detail.theme === 'dark';
        try {
            if (typeof Chart !== 'undefined') {
                Chart.defaults.color = isDark ? '#E8DDD0' : '#333333';
                Chart.defaults.borderColor = isDark ? 'rgba(200,131,42,0.12)' : 'rgba(30,18,10,0.08)';
            }
        } catch (err) {}

        if (window.__egyxploreThemeChange) {
            window.__egyxploreThemeChange(isDark);
        }
    });

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();