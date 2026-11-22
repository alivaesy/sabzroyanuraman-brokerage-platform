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
    expect(errors, 'uncaught errors during WebGIS initialization').toEqual([]);
    await page.evaluate(() => {
      const bridge = window.__sabzWebGISSharedBridge;
      bridge.addGeoJSONLayer('__symbology-module-test__', {
        type: 'FeatureCollection',
        features: [
          {type:'Feature',properties:{category:'A',area:10,name:'Parcel A'},geometry:{type:'Polygon',coordinates:[[[47.75,33.62],[47.752,33.62],[47.752,33.622],[47.75,33.622],[47.75,33.62]]]}},
          {type:'Feature',properties:{category:'B',area:30,name:'Parcel B'},geometry:{type:'Polygon',coordinates:[[[47.753,33.62],[47.755,33.62],[47.755,33.622],[47.753,33.622],[47.753,33.62]]]}},
          {type:'Feature',properties:{category:'C',area:20,name:'Road'},geometry:{type:'LineString',coordinates:[[47.756,33.62],[47.758,33.622]]}},
          {type:'Feature',properties:{category:'D',area:40,name:'Control point'},geometry:{type:'Point',coordinates:[47.759,33.621]}}
        ]
      });
      const item = bridge.namedLayers.find(layer => layer.name === '__symbology-module-test__');
      bridge.openLayerProperties(item);
    });
    await page.locator('[data-lp-tab="symbology"]').click();
    await expect(page.locator('[data-lp-pane="symbology"]')).toBeVisible();
    await page.locator('#lpRenderer').selectOption('unique');
    await page.locator('#lpStyleField').selectOption('category');
    await expect(page.locator('#lpRendererLegend')).toContainText('A');
    await expect(page.locator('#lpRendererLegend')).toContainText('B');
    await expect(page.locator('#lpRendererLegend')).toContainText('C');
    await expect(page.locator('#lpRendererLegend')).toContainText('D');

    await page.locator('#lpRenderer').selectOption('graduated');
    await page.locator('#lpStyleField').selectOption('area');
    await page.locator('#lpClassificationMethod').selectOption('quantile');
    await expect(page.locator('#lpRendererLegend')).toContainText('10');
    await expect(page.locator('#lpRendererLegend')).toContainText('30');
    await page.locator('#layerPropertiesForm button[type="submit"]:not(#layerPropertiesOk)').click();

    const result = await page.evaluate(() => {
      const item = window.__sabzWebGISSharedBridge.namedLayers.find(layer => layer.name === '__symbology-module-test__');
      const features = item.layer.getLayers();
      return {
        config: item._rendererConfig,
        styled: features.length,
        styles: features.map(feature => ({color:feature.options.color,fillColor:feature.options.fillColor,geometry:feature.feature?.geometry?.type}))
      };
    });
    expect(result.config).toMatchObject({ mode: 'graduated', field: 'area', method: 'quantile' });
    expect(result.styled).toBe(4);
    expect(result.styles).toHaveLength(4);
    expect(result.styles.every(style => style.geometry==='LineString' ? !!style.color : !!style.fillColor)).toBeTruthy();
    expect(errors).toEqual([]);
  });


  test('Symbology applies renderer styles to point, line and polygon features', async ({ page }) => {
    const errors = await boot(page);
    await page.evaluate(() => {
      const bridge = window.__sabzWebGISSharedBridge;
      bridge.addGeoJSONLayer('__symbology-geometry-test__', {
        type: 'FeatureCollection', features: [
          {type:'Feature',properties:{kind:'Point',value:5},geometry:{type:'Point',coordinates:[47.75,33.62]}},
          {type:'Feature',properties:{kind:'Line',value:15},geometry:{type:'LineString',coordinates:[[47.751,33.62],[47.753,33.622]]}},
          {type:'Feature',properties:{kind:'Polygon',value:25},geometry:{type:'Polygon',coordinates:[[[47.754,33.62],[47.756,33.62],[47.756,33.622],[47.754,33.622],[47.754,33.62]]]}}
        ]
      });
      const item = bridge.namedLayers.find(layer => layer.name === '__symbology-geometry-test__');
      bridge.openLayerProperties(item);
    });
    await page.locator('[data-lp-tab="symbology"]').click();
    await page.locator('#lpRenderer').selectOption('unique');
    await page.locator('#lpStyleField').selectOption('kind');
    await expect(page.locator('#lpRendererLegend')).toContainText('Point');
    await expect(page.locator('#lpRendererLegend')).toContainText('Line');
    await expect(page.locator('#lpRendererLegend')).toContainText('Polygon');
    await page.locator('#layerPropertiesForm button[type="submit"]:not(#layerPropertiesOk)').click();
    const styles = await page.evaluate(() => {
      const item = window.__sabzWebGISSharedBridge.namedLayers.find(layer => layer.name === '__symbology-geometry-test__');
      return item.layer.getLayers().map(layer => ({
        type: layer.feature.geometry.type,
        color: layer.options.color,
        fillColor: layer.options.fillColor,
        radius: layer.options.radius
      }));
    });
    expect(styles).toHaveLength(3);
    expect(styles.every(style => style.color && style.fillColor)).toBeTruthy();
    expect(new Set(styles.map(style => style.fillColor)).size).toBeGreaterThan(1);
    expect(styles.find(style => style.type === 'Point').radius).toBeGreaterThan(0);
    expect(errors).toEqual([]);
  });

  test('TOC visibility, selection, context menu, properties and drag reorder work', async ({ page }) => {
    const errors = await boot(page);
    await page.evaluate(() => {
      const bridge = window.__sabzWebGISSharedBridge;
      bridge.addGeoJSONLayer('__toc-order-A__', {type:'FeatureCollection',features:[{type:'Feature',properties:{name:'A'},geometry:{type:'LineString',coordinates:[[47.75,33.62],[47.751,33.621]]}}]});
      bridge.addGeoJSONLayer('__toc-order-B__', {type:'FeatureCollection',features:[{type:'Feature',properties:{name:'B'},geometry:{type:'LineString',coordinates:[[47.76,33.62],[47.761,33.621]]}}]});
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
    const paintOrder = await page.evaluate(() => {
      const items = window.__sabzWebGISSharedBridge.namedLayers.filter(item => item.name.startsWith('__toc-order-'));
      return items.map(item => {
        const layer=item.layer.getLayers()[0],renderer=layer._renderer;
        if(renderer?._drawFirst){
          let node=renderer._drawFirst,index=0;
          while(node){if(node.layer===layer)return {name:item.name,index};node=node.next;index++;}
        }
        const path=layer._path;
        return {name:item.name,index:path?.parentNode?[...path.parentNode.children].indexOf(path):-1};
      });
    });
    expect(paintOrder.find(item => item.name === '__toc-order-B__').index)
      .toBeGreaterThan(paintOrder.find(item => item.name === '__toc-order-A__').index);
    expect(errors).toEqual([]);
  });

  test('Selection module computes feature distance through injected dependencies', async ({ page }) => {
    const errors = await boot(page);
    const result = await page.evaluate(() => {
      const drawn = {name:'My Drawings'};
      const vectorLayer = {name:'Parcels'};
      const child = {id:'selected-child'};
      const feature = {type:'Feature',properties:{name:'Parcel'},geometry:{type:'Point',coordinates:[47.75,33.62]}};
      const selected = new Set([child]);
      const selection = window.SabzWebGISSelection.create({
        turf: window.turf,
        nearestPointOnFeatureSafe: (target) => window.turf.point(target.geometry.coordinates),
        getDrawnLayer: () => drawn,
        getNamedLayers: () => [{name:'Parcels',layer:vectorLayer}],
        isVectorLayer: () => true,
        vectorFeatures: layer => layer===vectorLayer ? [{layer:child,feature}] : [],
        getSelectedFeatures: () => selected
      });
      const a = window.turf.point([47.75,33.62]);
      const b = window.turf.point([47.751,33.62]);
      return {
        distance: selection.featureDistanceMeters(a,b),
        layerCount: selection.allSelectableFeatureLayers().length,
        recordCount: selection.getFeatureRecords().length,
        selectedOwner: selection.selectedRecords()[0]?.owner?.name
      };
    });
    expect(result.distance).toBeGreaterThan(0);
    expect(result.distance).toBeLessThan(100);
    expect(result.layerCount).toBe(2);
    expect(result.recordCount).toBe(1);
    expect(result.selectedOwner).toBe('Parcels');
    expect(errors).toEqual([]);
  });
  test('Feature Editing module controls toolbar and standard action states', async ({ page }) => {
    const errors = await boot(page);
    const result = await page.evaluate(() => {
      const selected = new Set([{}]);
      const clipboard = [{}];
      const editing = window.SabzWebGISFeatureEditing.create({
        $: id => document.getElementById(id), document,
        getEditSessionActive: () => true,
        getSelectedFeatures: () => selected,
        getFeatureClipboard: () => clipboard,
        getActiveDrawHandler: () => ({}),
        getActiveDrawPurpose: () => 'draw'
      });
      editing.updateEditorToolbar();
      return {
        startDisabled: document.getElementById('startEditingBtn').disabled,
        saveEnabled: !document.getElementById('saveEditsBtn').disabled,
        stopEnabled: !document.getElementById('stopEditingBtn').disabled,
        copyEnabled: !document.getElementById('copyBtn').disabled,
        cutEnabled: !document.getElementById('cutBtn').disabled,
        pasteEnabled: !document.getElementById('pasteBtn').disabled,
        finishEnabled: !document.getElementById('finishSketchBtn').disabled
      };
    });
    expect(result).toEqual({startDisabled:true,saveEnabled:true,stopEnabled:true,copyEnabled:true,cutEnabled:true,pasteEnabled:true,finishEnabled:true});
    expect(errors).toEqual([]);
  });

});
