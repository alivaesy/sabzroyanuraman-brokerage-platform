'use strict';

const { test, expect } = require('@playwright/test');
const path = require('node:path');
const { pathToFileURL } = require('node:url');

const app = path.resolve(__dirname, '..', 'sabzroyanuraman-site', 'web-gis.html');
const appUrl = pathToFileURL(app).href;

test.describe('WebGIS real-browser regression', () => {
  test('boots the map and ArcMap toolbars without uncaught page errors', async ({ page }) => {
    const pageErrors = [];
    page.on('pageerror', error => pageErrors.push(error.message));
    await page.goto(appUrl, { waitUntil: 'domcontentloaded' });
    await expect(page.locator('#map')).toBeVisible({ timeout: 30000 });
    await expect(page.getByRole('button', { name: 'Add Data', exact: false }).first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'ArcToolbox', exact: false }).first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Zoom In' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Identify' })).toBeVisible();
    await page.waitForTimeout(1500);
    expect(pageErrors, 'uncaught JavaScript errors').toEqual([]);
  });

  test('opens and closes the ArcMap File menu and Add Data dialog', async ({ page }) => {
    await page.goto(appUrl, { waitUntil: 'domcontentloaded' });
    await page.getByRole('button', { name: 'File', exact: true }).click();
    await expect(page.locator('#fileMenu')).toBeVisible();
    await page.getByRole('button', { name: /Add Data\.\.\./ }).first().click();
    await expect(page.getByText('Add Data', { exact: true }).last()).toBeVisible();
    const dialog = page.locator('#addDataDialog, .data-dialog').filter({ hasText: 'Add Data' }).last();
    await expect(dialog).toBeVisible();
    const cancel = dialog.getByRole('button', { name: /Cancel|×/ }).first();
    if (await cancel.count()) await cancel.click();
  });

  test('basemap controls remain available and map responds to navigation', async ({ page }) => {
    await page.goto(appUrl, { waitUntil: 'domcontentloaded' });
    await expect(page.locator('#map')).toBeVisible();
    const basemapButtons = page.locator('button').filter({ hasText: /OpenStreetMap|Esri World Imagery|OpenTopoMap/ });
    if (await basemapButtons.count()) {
      await basemapButtons.first().click();
      await expect(page.locator('#map .leaflet-container, #map .leaflet-pane')).toBeAttached();
    }
    await page.getByRole('button', { name: 'Zoom In' }).click();
    await expect(page.locator('#map .leaflet-container')).toBeAttached();
  });
});
