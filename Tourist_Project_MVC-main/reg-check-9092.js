
(function () {
    'use strict';

    /* =========================================================
       DESTINATION CAROUSEL DATA & LOGIC
       ========================================================= */

    const slides = [
        {
            file: 'amr_bn_elas.png',
            label: 'Amr Ibn Al-Aas Mosque',
            description: "Discover one of Cairo's oldest landmarks and experience a remarkable part of Egypt's Islamic heritage."
        },
        {
            file: 'Apo_ELhole.png',
            label: 'Abu El Hol',
            description: "Meet the legendary guardian of the Giza Plateau and experience one of Egypt's most iconic wonders."
        },
        {
            file: 'pyramids.png',
            label: 'Giza Pyramids',
            description: "Stand before the timeless pyramids and explore one of the world's greatest ancient civilizations."
        },
        {
            file: 'temple.png',
            label: 'Ancient Temple',
            description: 'Walk through monumental temples where ancient Egyptian stories still live in stone.'
        },
        {
            file: 'the_geant_musem.png',
            label: 'Grand Egyptian Museum',
            description: "Discover Egypt's treasures in a spectacular journey through thousands of years of history."
        },
        {
            file: 'wadi_elkbash.png',
            label: 'Wadi El Kabash',
            description: "Follow the ancient path and uncover the atmosphere of Egypt's remarkable archaeological heritage."
        }
    ];

    const IMG_DIR = "/x/";
    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    let current = 2; // Default to Pyramids
    let activeLayerIsA = true;
    let autoplayTimer = null;

    const heroPanel = document.getElementById('heroPanel');
    const heroBgA = document.getElementById('heroBgA');
    const heroBgB = document.getElementById('heroBgB');
    const heroDescription = document.getElementById('heroDescription');
    const thumbStrip = document.getElementById('thumbStrip');
    const dotsEl = document.getElementById('dots');
    const liveRegion = document.getElementById('carouselLiveRegion');

    function slideUrl(index) {
        return `${IMG_DIR}${encodeURIComponent(slides[index].file)}`;
    }

    /* Preload images */
    slides.forEach((slide, index) => {
        const img = new Image();
        img.src = slideUrl(index);
    });

    function renderThumbnails() {
        thumbStrip.innerHTML = '';
        dotsEl.innerHTML = '';

        slides.forEach((slide, index) => {
            const thumb = document.createElement('button');
            thumb.type = 'button';
            thumb.className = 'thumb-item' + (index === current ? ' active' : '');
            thumb.style.backgroundImage = `url("${slideUrl(index)}")`;
            thumb.title = slide.label;
            thumb.setAttribute('role', 'tab');
            thumb.setAttribute('tabindex', '0');
            thumb.setAttribute('aria-label', slide.label);
            thumb.setAttribute('aria-selected', index === current ? 'true' : 'false');

            thumb.addEventListener('click', () => goToSlide(index, true));
            thumb.addEventListener('keydown', (event) => {
                if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    goToSlide(index, true);
                }
            });

            thumbStrip.appendChild(thumb);

            const dot = document.createElement('button');
            dot.type = 'button';
            dot.className = 'dot-pill' + (index === current ? ' active' : '');
            dot.setAttribute('aria-label', `Go to slide ${index + 1}`);

            dot.addEventListener('click', () => goToSlide(index, true));
            dotsEl.appendChild(dot);
        });
    }

    function updateActiveUI() {
        [...thumbStrip.children].forEach((element, index) => {
            element.classList.toggle('active', index === current);
            element.setAttribute('aria-selected', index === current ? 'true' : 'false');
        });

        [...dotsEl.children].forEach((element, index) => {
            element.classList.toggle('active', index === current);
        });

        if (liveRegion) {
            liveRegion.textContent = `Showing ${slides[current].label}, image ${current + 1} of ${slides.length}`;
        }
    }

    function updateDescription() {
        const slide = slides[current];
        heroDescription.classList.add('changing');
        setTimeout(() => {
            heroDescription.textContent = slide.description;
            heroDescription.classList.remove('changing');
        }, 150);
    }

    function goToSlide(index, isUserAction) {
        current = (index + slides.length) % slides.length;
        const incoming = activeLayerIsA ? heroBgB : heroBgA;
        const outgoing = activeLayerIsA ? heroBgA : heroBgB;
        incoming.style.backgroundImage = `url("${slideUrl(current)}")`;
        incoming.classList.add('is-active');
        outgoing.classList.remove('is-active');
        activeLayerIsA = !activeLayerIsA;
        updateActiveUI();
        updateDescription();
        if (isUserAction) {
            restartAutoplay();
        }
    }

    function startAutoplay() {
        if (prefersReducedMotion) return;
        stopAutoplay();
        autoplayTimer = setInterval(() => {
            goToSlide(current + 1, false);
        }, 7000);
    }

    function stopAutoplay() {
        if (autoplayTimer) {
            clearInterval(autoplayTimer);
            autoplayTimer = null;
        }
    }

    function restartAutoplay() {
        stopAutoplay();
        startAutoplay();
    }

    const prevBtn = document.getElementById('prevBtn');
    const nextBtn = document.getElementById('nextBtn');

    if (prevBtn) prevBtn.addEventListener('click', () => goToSlide(current - 1, true));
    if (nextBtn) nextBtn.addEventListener('click', () => goToSlide(current + 1, true));

    if (heroPanel) {
        heroPanel.addEventListener('mouseenter', stopAutoplay);
        heroPanel.addEventListener('mouseleave', startAutoplay);
        heroPanel.addEventListener('focusin', stopAutoplay);
        heroPanel.addEventListener('focusout', startAutoplay);

        heroPanel.addEventListener('keydown', (event) => {
            if (event.key === 'ArrowLeft') {
                event.preventDefault();
                goToSlide(current - 1, true);
            }
            if (event.key === 'ArrowRight') {
                event.preventDefault();
                goToSlide(current + 1, true);
            }
        });
    }

    renderThumbnails();
    updateActiveUI();

    if (heroDescription) {
        heroDescription.textContent = slides[current].description;
    }

    startAutoplay();


    /* =========================================================
       PASSWORD VISIBILITY TOGGLE
       ========================================================= */

    const eyeOpenPath = '<path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7-10-7-10-7z"/><circle cx="12" cy="12" r="3"/>';
    const eyeClosedPath = '<path d="M3 3l18 18" stroke-linecap="round"/><path d="M10.6 5.1A10.8 10.8 0 0 1 12 5c6.4 0 10 7 10 7a17.7 17.7 0 0 1-3.2 4.1M6.6 6.6C4 8.3 2 12 2 12s3.6 7 10 7c1.4 0 2.6-.3 3.7-.8M9.5 9.5a3 3 0 0 0 4.2 4.2" stroke-linecap="round" stroke-linejoin="round"/>';

    function initToggle(pwInputId, btnId, iconId) {
        const pwInput = document.getElementById(pwInputId);
        const toggleBtn = document.getElementById(btnId);
        const eyeIcon = document.getElementById(iconId);

        if (pwInput && toggleBtn && eyeIcon) {
            toggleBtn.addEventListener('click', () => {
                const isShown = pwInput.type === 'text';
                pwInput.type = isShown ? 'password' : 'text';
                eyeIcon.innerHTML = isShown ? eyeOpenPath : eyeClosedPath;
                toggleBtn.setAttribute('aria-label', isShown ? 'Show password' : 'Hide password');
                toggleBtn.setAttribute('aria-pressed', String(!isShown));
            });
        }
    }

    initToggle('passwordInput', 'togglePwBtn', 'eyeIcon');
    initToggle('confirmPasswordInput', 'toggleConfirmPwBtn', 'confirmEyeIcon');


    /* =========================================================
       CAPS LOCK DETECTION
       ========================================================= */

    const capsHint = document.getElementById('capslockHint');
    const pwInput = document.getElementById('passwordInput');

    if (pwInput && capsHint) {
        pwInput.addEventListener('keyup', (event) => {
            const isCaps = typeof event.getModifierState === 'function' && event.getModifierState('CapsLock');
            capsHint.classList.toggle('visible', !!isCaps);
        });

        pwInput.addEventListener('blur', () => {
            capsHint.classList.remove('visible');
        });
    }


    /* =========================================================
       PREVENT DOUBLE SUBMISSION & HANDLE VALIDATION
       ========================================================= */

    const form = document.getElementById('registerForm');
    const submitBtn = document.getElementById('registerSubmitBtn');

    if (form && submitBtn) {
        form.addEventListener('submit', () => {
            if (
                form.checkValidity &&
                window.jQuery &&
                jQuery.validator &&
                !jQuery(form).valid()
            ) {
                return;
            }

            submitBtn.classList.add('is-loading');
            submitBtn.disabled = true;
        });
    }
})();

