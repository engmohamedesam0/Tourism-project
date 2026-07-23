// @ts-check
import { test, expect } from '@playwright/test';

test.describe('Home page', () => {
  test('loads with correct title', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveTitle(/Home - EGYXPLORE/);
  });

  test('shows hero heading and explore CTA', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'EGYXPLORE' })).toBeVisible();

    const exploreCta = page.getByRole('link', { name: /explore/i }).first();
    await expect(exploreCta).toBeVisible();
    await exploreCta.click();

    await expect(page).toHaveURL(/\/Explore/);
  });

  test('main nav links point to the right pages', async ({ page }) => {
    await page.goto('/');

    await page.getByRole('link', { name: 'About', exact: true }).click();
    await expect(page).toHaveURL(/\/About/);
    await expect(page).toHaveTitle(/About - EGYXPLORE/);
  });
});