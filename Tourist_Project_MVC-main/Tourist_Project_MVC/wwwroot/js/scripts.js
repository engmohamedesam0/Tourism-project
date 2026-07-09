/*!
* Start Bootstrap - Grayscale v7.0.6 (https://startbootstrap.com/theme/grayscale)
* Copyright 2013-2023 Start Bootstrap
* Licensed under MIT (https://github.com/StartBootstrap/startbootstrap-grayscale/blob/master/LICENSE)
*/
//
// Scripts
// 

window.addEventListener('DOMContentLoaded', event => {

    // Keep --nav-offset in sync with the actual rendered navbar height so the
    // content offset (padding-top / 100vh - offset) matches the two-tier bar.
    // Measured at top-of-page where both the utility and primary bars are visible.
    var syncNavOffset = function () {
        const navbar = document.body.querySelector('#mainNav');
        if (!navbar) {
            return;
        }
        document.documentElement.style.setProperty('--nav-offset', navbar.offsetHeight + 'px');
    };

    // Navbar shrink / direction-aware utility bar
    var lastScrollY = 0;
    var navbarShrink = function () {
        const navbarCollapsible = document.body.querySelector('#mainNav');
        if (!navbarCollapsible) {
            return;
        }
        const currentY = window.scrollY;
        const threshold = 10;

        // Top of page: floating translucent, utility bar visible
        if (currentY === 0) {
            navbarCollapsible.classList.remove('navbar-shrink');
            navbarCollapsible.classList.remove('nav-utility-hidden');
            syncNavOffset();
            lastScrollY = currentY;
            return;
        }

        // Only react to meaningful movement to avoid jitter
        if (Math.abs(currentY - lastScrollY) >= threshold) {
            if (currentY > lastScrollY) {
                // scrolling DOWN: solidify lower bar, slide utility bar away
                navbarCollapsible.classList.add('navbar-shrink');
                navbarCollapsible.classList.add('nav-utility-hidden');
            } else {
                // scrolling UP: restore translucent bar + utility bar
                navbarCollapsible.classList.remove('navbar-shrink');
                navbarCollapsible.classList.remove('nav-utility-hidden');
            }
            lastScrollY = currentY;
        }
    };

    // Shrink the navbar
    navbarShrink();

    // Shrink the navbar when page is scrolled
    document.addEventListener('scroll', navbarShrink);

    // Sync the content offset to the real navbar height
    syncNavOffset();
    window.addEventListener('resize', syncNavOffset);

    // Activate Bootstrap scrollspy on the main nav element
    const mainNav = document.body.querySelector('#mainNav');
    if (mainNav) {
        new bootstrap.ScrollSpy(document.body, {
            target: '#mainNav',
            rootMargin: '0px 0px -40%',
        });
    };

    // Collapse responsive navbar when toggler is visible
    const navbarToggler = document.body.querySelector('.navbar-toggler');
    const responsiveNavItems = [].slice.call(
        document.querySelectorAll('#navbarResponsive .nav-link')
    );
    responsiveNavItems.map(function (responsiveNavItem) {
        responsiveNavItem.addEventListener('click', () => {
            if (window.getComputedStyle(navbarToggler).display !== 'none') {
                navbarToggler.click();
            }
        });
    });

});