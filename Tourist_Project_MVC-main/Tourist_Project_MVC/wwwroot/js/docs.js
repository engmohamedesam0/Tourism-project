(function () {
    'use strict';

    function getNavOffset() {
        var raw = getComputedStyle(document.documentElement).getPropertyValue('--nav-offset').trim();
        var val = parseInt(raw, 10);
        return isNaN(val) ? 80 : val;
    }

    /* =========================================
       Mobile sidebar toggle & Section Collapse
       ========================================= */
    function initSidebar() {
        var toggle = document.getElementById('docsSidebarToggle');
        var sidebar = document.getElementById('docsSidebar');
        var backdrop = document.getElementById('docsSidebarBackdrop');
        if (toggle && sidebar && backdrop) {
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

        // Section collapse state persistence via localStorage
        var STORAGE_KEY = 'egyxplore-docs-sidebar-state';
        var collapsedSections = {};
        try {
            collapsedSections = JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}');
        } catch (e) {
            collapsedSections = {};
        }

        document.querySelectorAll('.docs-sidebar-section').forEach(function (sec) {
            var secId = sec.getAttribute('data-section-id') || sec.querySelector('.docs-sidebar-section-title')?.textContent?.trim();
            if (!secId) return;

            var toggleBtn = sec.querySelector('.docs-sidebar-toggle-section');

            if (collapsedSections[secId]) {
                sec.classList.add('collapsed');
                if (toggleBtn) toggleBtn.setAttribute('aria-expanded', 'false');
            }

            if (toggleBtn) {
                toggleBtn.addEventListener('click', function (e) {
                    e.preventDefault();
                    e.stopPropagation();
                    var isCollapsed = sec.classList.toggle('collapsed');
                    toggleBtn.setAttribute('aria-expanded', isCollapsed ? 'false' : 'true');
                    collapsedSections[secId] = isCollapsed;
                    try {
                        localStorage.setItem(STORAGE_KEY, JSON.stringify(collapsedSections));
                    } catch (err) {}
                });
            }
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
       Search & Section Filter
       ========================================= */
    function initSearch() {
        var input = document.getElementById('docsSearchInput');
        var resultsBox = document.getElementById('docsSearchResults');
        var filterBtn = document.getElementById('docsFilterBtn');
        var filterMenu = document.getElementById('docsFilterMenu');
        if (!input || !resultsBox) return;

        var activeSection = '';

        // Filter Popover Handler
        if (filterBtn && filterMenu) {
            filterBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                var isExpanded = filterMenu.classList.toggle('show');
                filterBtn.setAttribute('aria-expanded', isExpanded ? 'true' : 'false');
            });

            document.addEventListener('click', function (e) {
                if (!e.target.closest('.docs-search')) {
                    filterMenu.classList.remove('show');
                    filterBtn.setAttribute('aria-expanded', 'false');
                }
            });

            filterMenu.querySelectorAll('.docs-filter-item').forEach(function (item) {
                item.addEventListener('click', function () {
                    filterMenu.querySelectorAll('.docs-filter-item').forEach(function (i) { i.classList.remove('active'); });
                    item.classList.add('active');
                    activeSection = item.getAttribute('data-section') || '';
                    filterMenu.classList.remove('show');
                    filterBtn.setAttribute('aria-expanded', 'false');

                    // Filter landing cards if search input is empty
                    filterLandingCards(activeSection);

                    // Re-run search if input has query
                    if (input.value.trim().length >= 2) {
                        performSearch(input.value.trim());
                    }
                });
            });
        }

        function filterLandingCards(sectionId) {
            document.querySelectorAll('.docs-section-block').forEach(function (block) {
                var id = block.getAttribute('data-section-id');
                if (!sectionId || (id && id.toLowerCase() === sectionId.toLowerCase())) {
                    block.style.display = '';
                } else {
                    block.style.display = 'none';
                }
            });
        }

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
                performSearch(q);
            }, 200);
        });

        function performSearch(q) {
            var url = '/Docs/search?q=' + encodeURIComponent(q);
            if (activeSection) {
                url += '&section=' + encodeURIComponent(activeSection);
            }
            fetch(url)
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    renderResults(data, q);
                })
                .catch(function () {
                    resultsBox.innerHTML = '<div class="p-3 text-muted small">Search unavailable.</div>';
                    resultsBox.classList.add('show');
                });
        }

        input.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                resultsBox.classList.remove('show');
                input.blur();
            }
        });

        document.addEventListener('click', function (e) {
            if (!e.target.closest('.docs-search')) {
                resultsBox.classList.remove('show');
            }
        });

        function renderResults(items, query) {
            if (!items || items.length === 0) {
                resultsBox.innerHTML = '<div class="p-3 text-muted small">No results found.</div>';
                resultsBox.classList.add('show');
                return;
            }

            var html = '';
            items.forEach(function (item) {
                var highlightedTitle = highlightMatch(item.title, query);
                var highlightedSnippet = highlightMatch(item.snippet, query);
                html += '<a class="docs-search-result-item" href="/Docs/' + encodeURIComponent(item.section) + '/' + encodeURIComponent(item.slug) + '">' +
                    '<div class="docs-search-result-title">' + highlightedTitle + '</div>' +
                    '<div class="docs-search-result-meta">' + escapeHtml(titleize(item.section)) + '</div>' +
                    '<div class="docs-search-result-snippet">' + highlightedSnippet + '</div>' +
                '</a>';
            });
            resultsBox.innerHTML = html;
            resultsBox.classList.add('show');
        }

        function highlightMatch(text, query) {
            if (!text) return '';
            var escapedText = escapeHtml(text);
            if (!query) return escapedText;

            var escapedQuery = escapeHtml(query);
            var regex = new Regex('(' + escapeRegExp(escapedQuery) + ')', 'gi');
            return escapedText.replace(regex, '<mark>$1</mark>');
        }

        function escapeRegExp(string) {
            return string.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
        }
    }

    /* =========================================
       PDF Download Handler
       ========================================= */
    function initPdfDownload() {
        var downloadBtn = document.getElementById('docsDownloadPdf');
        if (!downloadBtn) return;

        downloadBtn.addEventListener('click', function () {
            if (downloadBtn.classList.contains('downloading')) return;

            downloadBtn.classList.remove('success', 'error');
            downloadBtn.classList.add('downloading');

            var articleContent = document.querySelector('.docs-article');
            var heroContent = document.querySelector('.docs-hero');
            if (!articleContent) {
                downloadBtn.classList.remove('downloading');
                downloadBtn.classList.add('error');
                setTimeout(function () { downloadBtn.classList.remove('error'); }, 2000);
                return;
            }

            var element = document.createElement('div');
            element.className = 'docs-pdf-export-container';
            element.style.padding = '30px';
            element.style.fontFamily = 'sans-serif';
            element.style.color = '#1d1d1f';

            if (heroContent) {
                element.appendChild(heroContent.cloneNode(true));
            }
            element.appendChild(articleContent.cloneNode(true));

            // Clean up interactive elements inside export container
            element.querySelectorAll('.docs-copy-btn, .docs-download-pdf').forEach(function (el) { el.remove(); });

            var title = (document.querySelector('.docs-hero-title')?.textContent || 'Document').trim();
            var filename = title.toLowerCase().replace(/[^\w\s-]/g, '').replace(/\s+/g, '-') + '.pdf';

            var opt = {
                margin: 10,
                filename: filename,
                image: { type: 'jpeg', quality: 0.98 },
                html2canvas: { scale: 2, useCORS: true },
                jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' }
            };

            if (window.html2pdf) {
                window.html2pdf().set(opt).from(element).save()
                    .then(function () {
                        downloadBtn.classList.remove('downloading');
                        downloadBtn.classList.add('success');
                        setTimeout(function () { downloadBtn.classList.remove('success'); }, 1800);
                    })
                    .catch(function (err) {
                        console.error('PDF export error:', err);
                        downloadBtn.classList.remove('downloading');
                        downloadBtn.classList.add('error');
                        setTimeout(function () { downloadBtn.classList.remove('error'); }, 2000);
                    });
            } else {
                console.warn('html2pdf library not loaded');
                downloadBtn.classList.remove('downloading');
                downloadBtn.classList.add('error');
                setTimeout(function () { downloadBtn.classList.remove('error'); }, 2000);
            }
        });
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
       Heading anchors
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
        initPdfDownload();
        initHeadingAnchors();
        initTocSpy();
    });
})();