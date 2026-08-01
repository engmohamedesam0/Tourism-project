(function () {
    'use strict';

    var STORAGE_KEY = 'egyxplore-theme';
    var SYSTEM_DARK = window.matchMedia('(prefers-color-scheme: dark)');
    var toggleBtn = document.getElementById('themeToggle');

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
        if (!toggleBtn) return;
        var title = 'Light mode';
        if (mode === 'dark') title = 'Dark mode';
        else if (mode === 'system') title = 'System preference';
        toggleBtn.setAttribute('title', title);
        toggleBtn.setAttribute('aria-checked', mode === 'dark' ? 'true' : 'false');
    }

    function dispatchThemeChange() {
        var effective = getEffectiveTheme();
        var mode = getStoredMode();
        window.dispatchEvent(new CustomEvent('themechange', {
            detail: { theme: effective, mode: mode }
        }));
    }

    function toggle() {
        var current = getStoredMode();
        var cycle = ['light', 'dark', 'system'];
        var next = cycle[(cycle.indexOf(current) + 1) % cycle.length];
        localStorage.setItem(STORAGE_KEY, next);
        applyTheme(getEffectiveTheme());
        syncMode(next);
        updateToggleTitle(next);
        dispatchThemeChange();
    }

    function init() {
        var stored = getStoredMode();
        applyTheme(getEffectiveTheme());
        syncMode(stored);
        updateToggleTitle(stored);
    }

    // System preference listener
    SYSTEM_DARK.addEventListener('change', function () {
        if (getStoredMode() === 'system') {
            applyTheme(getEffectiveTheme());
            syncMode('system');
            dispatchThemeChange();
        }
    });

    // Toggle click
    if (toggleBtn) {
        toggleBtn.addEventListener('click', toggle);
    }

    // Chart.js adapter
    window.addEventListener('themechange', function (e) {
        var isDark = e.detail.theme === 'dark';
        try {
            if (typeof Chart !== 'undefined') {
                Chart.defaults.color = isDark ? '#E8DDD0' : '#333333';
                Chart.defaults.borderColor = isDark ? 'rgba(200,131,42,0.12)' : 'rgba(30,18,10,0.08)';
            }
        } catch (err) {
            // Chart.js not loaded or version mismatch — ignore
        }

        // Notify ArcGIS maps if present
        if (window.__egyxploreThemeChange) {
            window.__egyxploreThemeChange(isDark);
        }
    });

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
