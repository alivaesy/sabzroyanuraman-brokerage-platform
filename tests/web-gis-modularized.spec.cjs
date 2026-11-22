'use strict';

const { test, expect } = require('@playwright/test');
const path = require('node:path');
const { pathToFileURL } = require('node:url');

const app = path.resolve(__dirname, '..', 'sabzroyanuraman-site', 'web-gis.html');
const appUrl = pathToFileURL(app).href;

async function boot(page) {
  const pageErrors = [];
  page.on('pageerror', error => pageErrors.push(error.message));
  await page.goto(appUrl, { waitUntil: 'domcontentloaded' });
  await expect(page.locator('#map.leaflet-container')).toBeAttached({ timeout: 30000 });
  await page.waitForFunction(() => !!window.__sabzGISMap && !!window.__sabzWebGISSharedBridge);
  // The main inline application script publishes its bridge before all later declarations finish.
  await page.waitForTimeout(1800);
  return pageErrors;
}

function polygonFeature(id, category, area) {
  const x = 47.75 + id * 0.003;
  return {
    type: 'Feature', id: String(id),
    properties: { category, area, name: 'Parcel ' + id },
    geometry: { type: 'Polygon', coordinates: [[[x,33.62],[x+0.002,33.62],[x+0.002,33.622],[x,33.622],[x,33.62]]] }
  };
}

test.describe('Extracted Symbology and TOC modules', () => {
  test('Symbology tab configures real feature styles and applies renderer', async ({ page }) => {
    const errors = await boot(page);
    await page.evaluate(() => {
      const bridge = window.__sabzWebGISSharedBridge;
      bridge.addGeoJSONLayer('__symbology-module-test__', {
        type: 'FeatureCollection',
        features: [
          {type:'Feature',properties:{category:'A',area:10,name:'Parcel A'},geometry:{type:'Polygon',coordinates:[[[47.75,33.62],[47.752,33.62],[47.752,33.622],[47.75,33.622],[47.75,33.62]]]}},
          {type:'Feature',properties:{category:'B',area:30,name:'Parcel B'},geometry:{type:'Polygon',coordinates:[[[47.753,33.62],[47.755,33.62],[47.755,33.622],[47.753,33.622],[47.753,33.62]]]}}
        ]
      });
      const item = bridge.namedLayers.find(layer => layer.name === '__symbology-module-test__');
      bridge.openLayerProperties(item);
    });
    await page.locator('[data-lp-tab="symbology"]').click();
    await expect(page.locator('[data-lp-pane="symbology"]')).toBeVisible();
    await page.locator('#lpStyleField').selectOption('category');
    await page.locator('#lpRenderer').selectOption('unique');
    await expect(page.locator('#lpRendererLegend')).toContainText('A');
    await expect(page.locator('#lpRendererLegend')).toContainText('B');

    await page.locator('#lpStyleField').selectOption('area');
    await page.locator('#lpRenderer').selectOption('graduated');
    await page.locator('#lpClassificationMethod').selectOption('quantile');
    await expect(page.locator('#lpRendererLegend')).toContainText('10');
    await page.locator('#lpRendererLegend').toContainText('30');
    await page.locator('#layerPropertiesForm button[type="submit"]:not(#layerPropertiesOk)').click();

    const result = await page.evaluate(() => {
      const item = window.__sabzWebGISSharedBridge.namedLayers.find(layer => layer.name === '__symbology-module-test__');
      const features = item.layer.getLayers();
      return {
        config: item._rendererConfig,
        styled: features.length,
        fillColors: features.map(feature => feature.options.fillColor)
      };
    });
    expect(result.config).toMatchObject({ mode: 'graduated', field: 'area', method: 'quantile' });
    expect(result.styled).toBe(2);
    expect(result.fillColors.every(Boolean)).toBeTruthy();
    expect(errors).toEqual([]);
  });

  test('TOC visibility, selection, context menu, properties and drag reorder work', async ({ page }) => {
    const errors = await boot(page);
    await page.evaluate(() => {
      const bridge = window.__sabzWebGISSharedBridge;
      bridge.addGeoJSONLayer('__toc-order-A__', {type:'FeatureCollection',features:[{type:'Feature',properties:{name:'A'},geometry:{type:'Point',coordinates:[47.75,33.62]}}]});
      bridge.addGeoJSONLayer('__toc-order-B__', {type:'FeatureCollection',features:[{type:'Feature',properties:{name:'B'},geometry:{type:'Point',coordinates:[47.76,33.62]}}]});
    });
    await page.locator('[data-toc-view="drawing"]').click();
    const rowA = page.locator('#layers .layer-row[data-toc-layer-name="__toc-order-A__"]');
    const rowB = page.locator('#layers .layer-row[data-toc-layer-name="__toc-order-B__"]');
    await expect(rowA).toBeVisible();
    await expect(rowB).toBeVisible();
    await expect(rowA).toHaveAttribute('draggable', 'true');

    await rowA.locator('input[type="checkbox"]').uncheck();
    const hidden = await page.evaluate(() => {
      const item = window.__sabzWebGISSharedBridge.namedLayers.find(layer => layer.name === '__toc-order-A__');
      return !window.__sabzWebGISSharedBridge.map.hasLayer(item.layer);
    });
    expect(hidden).toBeTruthy();
    await rowA.locator('input[type="checkbox"]').check();

    await rowA.locator('.layer-name').dispatchEvent('click');
    await expect(rowA).toHaveClass(/layer-active/);
    await rowA.locator('.layer-name').dispatchEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 200, clientY: 200 });
    await expect(page.locator('#layerContextMenu')).toHaveClass(/show/);
    await page.locator('#layerContextMenu').evaluate(element => element.classList.remove('show'));

    await rowA.locator('.layer-name').dispatchEvent('dblclick', { bubbles: true, cancelable: true });
    await expect(page.locator('#layerPropertiesDialog')).toHaveClass(/show/);
    await page.locator('#layerPropertiesCancel').click();

    await rowA.dragTo(rowB);
    const order = await page.evaluate(() => window.__sabzWebGISSharedBridge.namedLayers
      .filter(item => item.name.startsWith('__toc-order-'))
      .map(item => item.name));
    expect(order).toEqual(['__toc-order-B__', '__toc-order-A__']);
    expect(errors).toEqual([]);
  });
});
