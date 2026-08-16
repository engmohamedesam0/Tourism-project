(function () {
    'use strict';

    function getNavOffset() {
        var raw = getComputedStyle(document.documentElement).getPropertyValue('--nav-offset').trim();
        var val = parseInt(raw, 10);
        return isNaN(val) ? 80 : val;
    }

    /* Shared sidebar state (indicator pill, scroll-spy, collapse persistence) */
    var sidebarState = {
        sidebar: null,
        pill: null,
        currentLink: null,
        STORAGE_KEY: 'egyxplore-docs-sidebar-state',
        collapsedSections: {}
    };
    try {
        sidebarState.collapsedSections = JSON.parse(localStorage.getItem(sidebarState.STORAGE_KEY) || '{}');
    } catch (e) {
        sidebarState.collapsedSections = {};
    }

    function getSectionId(sec) {
        return sec.getAttribute('data-section-id') || sec.querySelector('.docs-sidebar-section-title')?.textContent?.trim();
    }

    function setCollapsed(sec, isCollapsed) {
        var secId = getSectionId(sec);
        var toggleBtn = sec.querySelector('.docs-sidebar-toggle-section');
        sec.classList.toggle('collapsed', isCollapsed);
        if (toggleBtn) toggleBtn.setAttribute('aria-expanded', isCollapsed ? 'false' : 'true');
        if (secId) {
            sidebarState.collapsedSections[secId] = isCollapsed;
            try {
                localStorage.setItem(sidebarState.STORAGE_KEY, JSON.stringify(sidebarState.collapsedSections));
            } catch (err) {}
        }
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
                // Keep the active item in view inside the drawer
                if (sidebarState.currentLink) ensureActiveVisible(sidebarState.currentLink, false);
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
        // (getSectionId / setCollapsed live at module scope — shared with setActive)
        document.querySelectorAll('.docs-sidebar-section').forEach(function (sec) {
            var secId = getSectionId(sec);
            if (!secId) return;

            var toggleBtn = sec.querySelector('.docs-sidebar-toggle-section');

            if (sidebarState.collapsedSections[secId]) {
                sec.classList.add('collapsed');
                if (toggleBtn) toggleBtn.setAttribute('aria-expanded', 'false');
            }

            if (toggleBtn) {
                toggleBtn.addEventListener('click', function (e) {
                    e.preventDefault();
                    e.stopPropagation();
                    var isCollapsed = sec.classList.toggle('collapsed');
                    setCollapsed(sec, isCollapsed);
                    // Realign the sliding pill after the collapse/expand animation
                    setTimeout(function () {
                        if (sidebarState.currentLink) {
                            positionPill(sidebarState.currentLink);
                            ensureActiveVisible(sidebarState.currentLink, true);
                        }
                    }, 360);
                });
            }
        });

        // If the current article's section was persisted collapsed, expand it so
        // the reader always sees where they are.
        var activeOnLoad = sidebar.querySelector('.docs-sidebar-list a.active');
        if (activeOnLoad) {
            var activeSec = activeOnLoad.closest('.docs-sidebar-section');
            if (activeSec && activeSec.classList.contains('collapsed')) {
                setCollapsed(activeSec, false);
            }
        }
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
            // Re-evaluate the sidebar scroll-spy against the filtered layout
            window.dispatchEvent(new Event('scroll'));
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
            var regex = new RegExp('(' + escapeRegExp(escapedQuery) + ')', 'gi');
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
        var tocLinks = document.querySelectorAll('.docs-toc a, .docs-toc-bottom a');
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
       Sidebar sliding active pill
       ========================================= */
    function initSidebarIndicator() {
        var sidebar = document.getElementById('docsSidebar');
        if (!sidebar) return;
        sidebarState.sidebar = sidebar;
        sidebarState.links = Array.prototype.slice.call(sidebar.querySelectorAll('.docs-sidebar-list a'));
        if (!sidebarState.links.length) return;

        var pill = document.createElement('span');
        pill.className = 'docs-sidebar-indicator hidden';
        pill.setAttribute('aria-hidden', 'true');
        sidebar.appendChild(pill);
        sidebarState.pill = pill;

        // Smooth scroll to matching card when clicking sidebar links on landing page
        sidebarState.links.forEach(function (a) {
            a.addEventListener('click', function (e) {
                var href = a.getAttribute('href');
                if (!href) return;

                var targetCard = document.querySelector('.doc-card[href="' + CSS.escape ? href : href.replace(/"/g, '\\"') + '"]');
                if (!targetCard) {
                    // Try matching with CSS.escape if available
                    try { targetCard = document.querySelector('.doc-card[href="' + href + '"]'); } catch (err) {}
                }
                if (targetCard && targetCard.offsetParent !== null) {
                    e.preventDefault();
                    setActive(a);
                    var offset = getNavOffset() + 20;
                    var cardTop = targetCard.getBoundingClientRect().top + window.pageYOffset - offset;
                    window.scrollTo({ top: cardTop, behavior: 'smooth' });
                    try { history.replaceState(null, '', href); } catch (err) {}
                }
            });
        });

        // Click on section titles: scroll to section block if present, or toggle collapse
        sidebar.querySelectorAll('.docs-sidebar-section-title').forEach(function (titleEl) {
            titleEl.addEventListener('click', function () {
                var sec = titleEl.closest('.docs-sidebar-section');
                if (!sec) return;
                var secId = getSectionId(sec);
                if (!secId) return;

                var targetBlock = document.querySelector('.docs-section-block[data-section-id="' + secId + '"]');
                if (targetBlock && targetBlock.offsetParent !== null) {
                    var offset = getNavOffset() + 20;
                    var blockTop = targetBlock.getBoundingClientRect().top + window.pageYOffset - offset;
                    window.scrollTo({ top: blockTop, behavior: 'smooth' });
                } else {
                    var isCollapsed = sec.classList.toggle('collapsed');
                    setCollapsed(sec, isCollapsed);
                    // Wait for collapse animation, then realign the pill
                    setTimeout(function () {
                        if (sidebarState.currentLink) {
                            positionPill(sidebarState.currentLink);
                            ensureActiveVisible(sidebarState.currentLink, true);
                        }
                    }, 360);
                }
            });
        });

        // Article pages render an active link server-side — place the pill on it
        // instantly (no glide on first paint), then fade it in.
        var active = sidebar.querySelector('.docs-sidebar-list a.active');
        if (active) {
            sidebarState.currentLink = active;
            pill.style.transition = 'none';
            positionPill(active);
            void pill.offsetHeight; // force reflow so the next frame transitions
            pill.style.transition = '';
            pill.classList.remove('hidden');
            ensureActiveVisible(active, false);
            window.addEventListener('load', function () {
                positionPill(active);
                ensureActiveVisible(active, false);
            });
        }

        // Re-align when layout settles (fonts, theme switch, resize)
        if (document.fonts && document.fonts.ready) {
            document.fonts.ready.then(function () {
                if (sidebarState.currentLink) positionPill(sidebarState.currentLink);
            });
        }
        window.addEventListener('resize', function () {
            if (sidebarState.currentLink) {
                positionPill(sidebarState.currentLink);
                ensureActiveVisible(sidebarState.currentLink, false);
            }
        });

        // Reposition pill when the sidebar itself scrolls (the pill is absolute
        // to the sidebar, but uses offsetTop which is relative to offsetParent,
        // so it stays correct — but if sidebar scroll changes between position
        // reads, we should re-check on scroll end).
        var sidebarScrollTimer = null;
        sidebar.addEventListener('scroll', function () {
            // No need to reposition since we use offsetTop (stable across scrolls).
            // But if a debounced re-check is wanted for safety:
            clearTimeout(sidebarScrollTimer);
            sidebarScrollTimer = setTimeout(function () {
                if (sidebarState.currentLink) positionPill(sidebarState.currentLink);
            }, 150);
        }, { passive: true });
    }

    /* Move the pill onto `link`. Uses offsetTop/offsetHeight which are relative
       to the offsetParent (the sidebar, since it's sticky = positioned).
       The CSS transitions handle the smooth glide. */
    function positionPill(link) {
        var pill = sidebarState.pill;
        if (!pill || !link) return;
        // offsetParent is null while the link is hidden (collapsed section)
        if (!link.offsetParent || link.offsetHeight === 0) {
            pill.classList.add('hidden');
            return;
        }
        var inset = 2;
        pill.style.height = (link.offsetHeight - inset * 2) + 'px';
        pill.style.transform = 'translateY(' + (link.offsetTop + inset) + 'px)';
        pill.classList.remove('hidden');
    }

    /* If the active item sits outside the sidebar's visible scroll region,
       scroll the sidebar to bring it into view. */
    function ensureActiveVisible(link, smooth) {
        var sidebar = sidebarState.sidebar;
        if (!sidebar || !link || !link.offsetParent) return;
        if (sidebar.scrollHeight <= sidebar.clientHeight) return;

        var linkTop = link.offsetTop;
        var linkBottom = linkTop + link.offsetHeight;
        var viewTop = sidebar.scrollTop;
        var viewBottom = viewTop + sidebar.clientHeight;
        var padding = 24;
        var target = null;

        if (linkTop < viewTop + padding) {
            target = Math.max(0, linkTop - padding);
        } else if (linkBottom > viewBottom - padding) {
            target = Math.min(
                linkBottom - sidebar.clientHeight + padding,
                sidebar.scrollHeight - sidebar.clientHeight
            );
        }
        if (target === null) return;

        var reduced = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        if (smooth && !reduced) {
            sidebar.scrollTo({ top: target, behavior: 'smooth' });
        } else {
            sidebar.scrollTop = target;
        }
    }

    /* Activate a sidebar link: classes + pill + visibility + auto-expand. */
    function setActive(link, instant) {
        if (!link) return;
        var sidebar = sidebarState.sidebar;
        sidebarState.currentLink = link;

        sidebar.querySelectorAll('.docs-sidebar-list a.active').forEach(function (a) {
            if (a !== link) a.classList.remove('active');
        });
        sidebar.querySelectorAll('.docs-sidebar-section.has-active').forEach(function (s) {
            s.classList.remove('has-active');
        });
        link.classList.add('active');

        var sec = link.closest('.docs-sidebar-section');
        if (sec) {
            sec.classList.add('has-active');
            // Reveal a section that was manually collapsed but now contains
            // the content being read, so the highlight stays visible.
            if (sec.classList.contains('collapsed')) {
                setCollapsed(sec, false);
                // Wait for expand animation before positioning
                setTimeout(function () {
                    positionPill(link);
                    ensureActiveVisible(link, !instant);
                }, 360);
                return;
            }
        }

        positionPill(link);
        ensureActiveVisible(link, !instant);
    }

    /* =========================================
       Scroll-spy — sidebar follows visible content
       ========================================= */
    function initSidebarSpy() {
        if (!sidebarState.sidebar) return;
        var links = sidebarState.links;
        if (!links || !links.length) return;

        var targets = [];
        var cards = document.querySelectorAll('.doc-card');

        if (cards.length) {
            // Docs landing page: match cards to sidebar links
            cards.forEach(function (card) {
                var href = card.getAttribute('href');
                var link = links.filter(function (l) { return l.getAttribute('href') === href; })[0];
                if (link) targets.push({ el: card, link: link });
            });
        } else {
            // Article page: headings map to the single active article, keeping
            // the highlight pinned while the reader scrolls through it.
            var activeLink = sidebarState.sidebar.querySelector('.docs-sidebar-list a.active');
            if (!activeLink) return;
            document.querySelectorAll('.docs-article h2, .docs-article h3').forEach(function (h) {
                targets.push({ el: h, link: activeLink });
            });
        }

        if (!targets.length) return;

        var ticking = false;

        function currentTarget() {
            var doc = document.documentElement;
            // Only spy on elements that are actually rendered
            var visible = [];
            for (var i = 0; i < targets.length; i++) {
                if (targets[i].el.offsetParent !== null) visible.push(targets[i]);
            }
            if (!visible.length) return null;

            var atBottom = (window.innerHeight + window.pageYOffset) >= (doc.scrollHeight - 4);
            if (atBottom) return visible[visible.length - 1];

            var spyLine = getNavOffset() + 120;
            var found = null;
            for (var j = 0; j < visible.length; j++) {
                if (visible[j].el.getBoundingClientRect().top <= spyLine) {
                    found = visible[j];
                }
            }
            return found || visible[0];
        }

        function onScroll() {
            if (ticking) return;
            ticking = true;
            window.requestAnimationFrame(function () {
                var cur = currentTarget();
                if (cur && cur.link && cur.link !== sidebarState.currentLink) {
                    setActive(cur.link);
                }
                ticking = false;
            });
        }

        window.addEventListener('scroll', onScroll, { passive: true });
        window.addEventListener('resize', onScroll);
        onScroll();
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
        initSidebarIndicator();
        initSidebarSpy();
        initCopyButtons();
        initSearch();
        initPdfDownload();
        initHeadingAnchors();
        initTocSpy();

        // Smooth-scroll to a deep link (e.g. direct #heading URL)
        if (window.location.hash) {
            var hashTarget = document.getElementById(window.location.hash.substring(1));
            if (hashTarget) {
                var reduced = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
                window.setTimeout(function () {
                    hashTarget.scrollIntoView({ behavior: reduced ? 'auto' : 'smooth', block: 'start' });
                }, 80);
            }
        }
    });
})();