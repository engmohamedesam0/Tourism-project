(function () {
    'use strict';

    let burstSvgMarkup = null;

    async function getBurstSvgMarkup() {
        if (burstSvgMarkup) return burstSvgMarkup;
        const res = await fetch('/img/icons/heart-burst.svg');
        burstSvgMarkup = await res.text();
        return burstSvgMarkup;
    }

    async function playBurst(button) {
        const markup = await getBurstSvgMarkup();
        const wrapper = document.createElement('span');
        wrapper.className = 'favorite-btn-burst';
        wrapper.innerHTML = markup;
        button.appendChild(wrapper);
        setTimeout(() => wrapper.remove(), 1100);
    }

    async function toggleFavorite(button) {
        const itemType = button.dataset.itemType;
        const itemTypeValue = {
            Destination: 0,
            Reward: 1,
            Branch: 2
        }[itemType];
        const itemId = parseInt(button.dataset.itemId, 10);
        const wasFavorited = button.classList.contains('is-favorited');

        if (itemTypeValue === undefined || Number.isNaN(itemId)) {
            console.error('Invalid favorite button data.', { itemType, itemId });
            return;
        }

        button.disabled = true;
        try {
            const res = await fetch('/Favorites/Toggle', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('#antiforgeryForm input[name="__RequestVerificationToken"]')?.value || ''
                },
                body: JSON.stringify({ itemType: itemTypeValue, itemId })
            });
            if (!res.ok) {
                let details = '';
                try {
                    const error = await res.json();
                    details = error.error ? `: ${error.error}` : '';
                } catch (_) {
                    // Keep the generic message when the server did not return JSON.
                }
                throw new Error(`Toggle failed (${res.status})${details}`);
            }
            const data = await res.json();

            document.querySelectorAll(
                `.favorite-btn[data-item-type="${itemType}"][data-item-id="${itemId}"]`
            ).forEach(btn => {
                btn.classList.toggle('is-favorited', data.isFavorited);
                btn.querySelector('.favorite-btn-icon').className =
                    'bi favorite-btn-icon ' + (data.isFavorited ? 'bi-heart-fill' : 'bi-heart');
                btn.setAttribute('aria-pressed', String(data.isFavorited));
                btn.setAttribute('aria-label', data.isFavorited ? 'Remove from favorites' : 'Add to favorites');
            });

            if (!wasFavorited && data.isFavorited) {
                playBurst(button);
            }
        } catch (e) {
            console.error(e);
        } finally {
            button.disabled = false;
        }
    }

    document.addEventListener('click', function (e) {
        const btn = e.target.closest('.favorite-btn');
        if (btn) toggleFavorite(btn);
    });
})();