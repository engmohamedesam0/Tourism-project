(function () {
    'use strict';

    function getNavOffset() {
        var raw = getComputedStyle(document.documentElement).getPropertyValue('--nav-offset').trim();
        var val = parseInt(raw, 10);
        return isNaN(val) ? 80 : val;
    }

    /* =========================================
       Mobile sidebar toggle
       ========================================= */
    function initSidebar() {
        var toggle = document.getElementById('docsSidebarToggle');
        var sidebar = document.getElementById('docsSidebar');
        var backdrop = document.getElementById('docsSidebarBackdrop');
        if (!toggle || !sidebar || !backdrop) return;

        function open() {
            sidebar.classList.add('open');
            backdrop.classList.add('show');
            document.body.style.overflow = 'hidden';
            toggle.setAttribute('aria-expanded', 'true');
        }

        function close() {
            sidebar.classList.remove('open');
            backdrop.classList.remove('show');
            document.body.style.overflow = '';
            toggle.setAttribute('aria-expanded', 'false');
        }

        toggle.addEventListener('click', function () {
            if (sidebar.classList.contains('open')) {
                close();
            } else {
                open();
            }
        });

        backdrop.addEventListener('click', close);

        document.querySelectorAll('.docs-sidebar-list a, .docs-sidebar-brand').forEach(function (el) {
            el.addEventListener('click', function () {
                if (window.innerWidth < 768) close();
            });
        });

        window.addEventListener('resize', function () {
            if (window.innerWidth >= 768) close();
        });
    }

    /* =========================================
       Copy-to-clipboard for code blocks
       ========================================= */
    function initCopyButtons() {
        document.querySelectorAll('.docs-article pre').forEach(function (pre) {
            if (pre.querySelector('.docs-copy-btn')) return;

            var btn = document.createElement('button');
            btn.className = 'docs-copy-btn';
            btn.type = 'button';
            btn.textContent = 'Copy';
            btn.setAttribute('aria-label', 'Copy code to clipboard');
            pre.style.position = 'relative';
            pre.appendChild(btn);

            btn.addEventListener('click', async function () {
                var code = pre.querySelector('code');
                var text = code ? code.innerText : pre.innerText;
                try {
                    await navigator.clipboard.writeText(text);
                    btn.classList.add('copied');
                    btn.textContent = 'Copied';
                    setTimeout(function () {
                        btn.classList.remove('copied');
                        btn.textContent = 'Copy';
                    }, 1800);
                } catch (e) {
                    btn.textContent = 'Failed';
                    setTimeout(function () { btn.textContent = 'Copy'; }, 1800);
                }
            });
        });
    }

    /* =========================================
       Search (client-side suggestion dropdown)
       ========================================= */
    function initSearch() {
        var input = document.getElementById('docsSearchInput');
        var resultsBox = document.getElementById('docsSearchResults');
        if (!input || !resultsBox) return;

        var debounce;
        input.addEventListener('input', function () {
            clearTimeout(debounce);
            var q = input.value.trim();
            if (q.length < 2) {
                resultsBox.classList.remove('show');
                resultsBox.innerHTML = '';
                return;
            }
            debounce = setTimeout(function () {
                fetch('/Docs/search?q=' + encodeURIComponent(q))
                    .then(function (r) { return r.json(); })
                    .then(function (data) {
                        renderResults(data);
                    })
                    .catch(function () {
                        resultsBox.innerHTML = '<div class="p-3 text-muted small">Search unavailable.</div>';
                        resultsBox.classList.add('show');
                    });
            }, 220);
        });

        input.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                resultsBox.classList.remove('show');
                input.blur();
            }
        });

        document.addEventListener('click', function (e) {
            if (!e.target.closest('.docs-search-wrap')) {
                resultsBox.classList.remove('show');
            }
        });

        function renderResults(items) {
            if (items.length === 0) {
                resultsBox.innerHTML = '<div class="p-3 text-muted small">No results found.</div>';
                resultsBox.classList.add('show');
                return;
            }
            var html = '';
            items.forEach(function (item) {
                html += '<a class="docs-search-result-item" href="/Docs/' + encodeURIComponent(item.section) + '/' + encodeURIComponent(item.slug) + '">' +
                    '<div class="docs-search-result-title">' + escapeHtml(item.title) + '</div>' +
                    '<div class="docs-search-result-meta">' + escapeHtml(titleize(item.section)) + '</div>' +
                    '<div class="docs-search-result-snippet">' + escapeHtml(item.snippet) + '</div>' +
                '</a>';
            });
            resultsBox.innerHTML = html;
            resultsBox.classList.add('show');
        }
    }

    /* =========================================
       Right-TOC scroll-spy
       ========================================= */
    function initTocSpy() {
        var tocLinks = document.querySelectorAll('.docs-toc a');
        if (tocLinks.length === 0) return;

        var headings = [];
        tocLinks.forEach(function (a) {
            var id = a.getAttribute('href');
            if (id && id.startsWith('#')) {
                var el = document.getElementById(id.substring(1));
                if (el) headings.push({ el: el, link: a });
            }
        });

        if (headings.length === 0) return;

        // IntersectionObserver's rootMargin needs a resolved pixel value —
        // it can't parse calc()/var() like a normal CSS property can.
        var navOffset = getNavOffset();
        var observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    tocLinks.forEach(function (a) { a.classList.remove('active'); });
                    var match = headings.find(function (h) { return h.el === entry.target; });
                    if (match) match.link.classList.add('active');
                }
            });
        }, {
            rootMargin: '-' + (navOffset + 20) + 'px 0px -60% 0px',
            threshold: 0
        });

        headings.forEach(function (h) { observer.observe(h.el); });

        tocLinks.forEach(function (a) {
            a.addEventListener('click', function (e) {
                e.preventDefault();
                var target = document.getElementById(a.getAttribute('href').substring(1));
                if (target) {
                    var offset = getNavOffset() + 12;
                    window.scrollTo({ top: target.getBoundingClientRect().top + window.pageYOffset - offset, behavior: 'smooth' });
                    history.replaceState(null, '', a.getAttribute('href'));
                }
            });
        });
    }

    /* =========================================
       Heading anchors (fallback if Markdig didn't add them)
       ========================================= */
    function initHeadingAnchors() {
        document.querySelectorAll('.docs-article h2, .docs-article h3, .docs-article h4').forEach(function (h) {
            if (h.id) return;
            var text = h.textContent.trim();
            var id = text.toLowerCase().replace(/[^\w\s-]/g, '').replace(/\s+/g, '-');
            h.id = id || 'section';
        });
    }

    /* =========================================
       Helpers
       ========================================= */
    function escapeHtml(str) {
        if (!str) return '';
        return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function titleize(input) {
        if (!input) return '';
        return input.split(/[-\s_]+/).map(function (w) { return w.charAt(0).toUpperCase() + w.slice(1); }).join(' ');
    }

    /* =========================================
       Boot
       ========================================= */
    document.addEventListener('DOMContentLoaded', function () {
        initSidebar();
        initCopyButtons();
        initSearch();
        initHeadingAnchors();
        initTocSpy();
    });
})();