// @ts-check
import { test, expect } from '@playwright/test';

test.describe('Explore page', () => {
  test('loads with correct title', async ({ page }) => {
    await page.goto('/Explore');
    await expect(page).toHaveTitle(/Explore - EGYXPLORE/);
  });

  test('lists destination cards with working detail links', async ({ page }) => {
    await page.goto('/Explore');

    const cards = page.locator('.explore-card');
    const count = await cards.count();

    if (count === 0) {
      // No seeded destinations: page should still render its empty state, not crash.
      await expect(page.getByText(/no destinations/i)).toBeVisible();
      return;
    }

    const firstCard = cards.first();
    const detailsLink = firstCard.getByRole('link', { name: /details/i });
    await detailsLink.click();

    await expect(page).toHaveURL(/\/Destination\/Details/);
  });

  test('search box filters the list by query string', async ({ page }) => {
    await page.goto('/Explore');

    const searchBox = page.locator('#exploreSearch');
    await searchBox.fill('zzz-no-such-destination-zzz');
    await searchBox.press('Enter');

    await expect(page).toHaveURL(/search=zzz-no-such-destination-zzz/);
    await expect(page.locator('.explore-card')).toHaveCount(0);
  });
});