'use strict';

const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const test = require('node:test');
const assert = require('node:assert/strict');

const htmlPath = path.join(__dirname, '..', 'sabzroyanuraman-site', 'web-gis.html');
const html = fs.readFileSync(htmlPath, 'utf8');

test('all inline JavaScript blocks parse', () => {
  const scripts = [...html.matchAll(/<script\b([^>]*)>([\s\S]*?)<\/script>/gi)]
    .filter(([, attrs, body]) => !/\bsrc\s*=/i.test(attrs)
      && !/type\s*=\s*["']application\/(?:json|ld\+json)["']/i.test(attrs)
      && body.trim());
  assert.ok(scripts.length > 0, 'expected inline scripts in web-gis.html');
  for (const [, attrs, body] of scripts) {
    const label = attrs.match(/\bid\s*=\s*["']([^"']+)["']/i)?.[1] || 'inline script';
    assert.doesNotThrow(() => new vm.Script(body, { filename: label }), label);
  }
});

test('Geoprocessing options have valid unique values and registered handlers', () => {
  const select = html.match(/<select\s+id=["']gisOperation["'][^>]*>([\s\S]*?)<\/select>/i)?.[1];
  assert.ok(select, 'gisOperation select was not found');
  const options = [...select.matchAll(/<option\s+value=["']([^"']+)["']/gi)].map(m => m[1]);
  assert.equal(options.length, 23, 'expected 23 Geoprocessing operations');
  assert.equal(new Set(options).size, options.length, 'operation values must be unique');

  const core = new Set([
    'buffer', 'clip', 'intersection', 'merge', 'union', 'erase', 'identity',
    'append', 'split', 'centroid', 'convexHull', 'boundingBox'
  ]);
  const toolEntries = [...html.matchAll(/\{\s*id:\s*["']([^"']+)["'](?:\s*,\s*op:\s*["']([^"']+)["'])?/g)];
  const registered = new Set(toolEntries.flatMap(([, id, op]) => [id, op].filter(Boolean)));
  const missing = options.filter(value => !core.has(value) && !registered.has(value));
  assert.deepEqual(missing, [], 'unregistered operation(s): ' + missing.join(', '));
});

test('ArcToolbox renderer retains search and empty-state feedback', () => {
  assert.match(html, /function\s+renderGisToolbox\s*\(/);
  assert.match(html, /No matching tools found/);
  assert.match(html, /registered tools/);
  assert.match(html, /gis-tool-group/);
});



test('View menu actions are connected to registered mapActions handlers', () => {
  const actionBlock = html.match(/const mapActions\s*=\s*\{([\s\S]*?)\n\s{4}\};/)?.[1];
  assert.ok(actionBlock, 'mapActions dispatcher was not found');
  const handlers = new Set([...actionBlock.matchAll(/^\s{6}([A-Za-z]\w*)\s*:/gm)].map(m => m[1]));

  for (const menuId of ['mapMenu', 'viewMenu']) {
    const menuStart = html.indexOf('<div class="arc-dropdown" id="' + menuId + '">');
    assert.notEqual(menuStart, -1, menuId + ' was not found');
    const nextMenu = html.indexOf('\n  <div class="arc-dropdown"', menuStart + 1);
    const menu = html.slice(menuStart, nextMenu === -1 ? html.length : nextMenu);
    const actions = [...menu.matchAll(/data-action=["']([^"']+)["']/g)].map(m => m[1]);
    assert.ok(actions.length, menuId + ' contains no actions');
    const missing = actions.filter(action => !handlers.has(action));
    assert.deepEqual(missing, [], menuId + ' actions without handlers: ' + missing.join(', '));
  }

  const tocHandler = actionBlock.match(/^\s{6}showLayers\s*:[\s\S]*?(?=^\s{6}[A-Za-z]\w*\s*:|$)/m)?.[0] || '';
  assert.match(tocHandler, /renderLayerList/);
  assert.match(tocHandler, /focus/);
});


test('Append maps source records to the target schema without changing Merge', () => {
  const start = html.indexOf('function appendFeaturesToTarget(');
  const end = html.indexOf('async function runWholeLayerGisOperation(', start);
  assert.ok(start >= 0 && end > start, 'Append schema helper was not found');
  const helper = html.slice(start, end);
  const appendFeaturesToTarget = vm.runInNewContext(helper + '\nappendFeaturesToTarget');
  const target = [
    { type: 'Feature', properties: { ParcelID: 1, Owner: 'A' }, geometry: { type: 'Point', coordinates: [1, 2] } }
  ];
  const source = [
    { type: 'Feature', properties: { parcelid: 2, Extra: 'discard', OWNER: 'B' }, geometry: { type: 'Point', coordinates: [3, 4] } }
  ];
  const output = appendFeaturesToTarget(target, source);
  assert.equal(output.length, 2);
  assert.deepEqual(JSON.parse(JSON.stringify(output[0].properties)), { ParcelID: 1, Owner: 'A' });
  assert.deepEqual(JSON.parse(JSON.stringify(output[1].properties)), { ParcelID: 2, Owner: 'B' });
  assert.equal(output[1].geometry.coordinates[0], 3);
  assert.match(html, /if\(op==='merge'\)\{/);
  assert.match(html, /output=appendFeaturesToTarget\(records,ovs\)/);
});


test('Merge preserves separate geometries instead of dissolving polygon features', () => {
  const start = html.indexOf('function mergeFeaturesPreservingGeometry(');
  const end = html.indexOf('async function runGisOperation(', start);
  assert.ok(start >= 0 && end > start, 'Merge helper was not found');
  const mergeFeaturesPreservingGeometry = vm.runInNewContext(html.slice(start, end) + '\nmergeFeaturesPreservingGeometry');
  const a = { type: 'Feature', properties: { ID: 1 }, geometry: { type: 'Polygon', coordinates: [[[0,0],[1,0],[1,1],[0,1],[0,0]]] } };
  const b = { type: 'Feature', properties: { ID: 2 }, geometry: { type: 'Polygon', coordinates: [[[1,0],[2,0],[2,1],[1,1],[1,0]]] } };
  const output = mergeFeaturesPreservingGeometry(a, b);
  assert.equal(output.length, 2);
  assert.deepEqual(JSON.parse(JSON.stringify(output[0].geometry)), a.geometry);
  assert.deepEqual(JSON.parse(JSON.stringify(output[1].geometry)), b.geometry);
  assert.equal(output[0].properties.ID, 1);
  assert.equal(output[1].properties.ID, 2);
  assert.equal(output[0].properties._merge_source, 'input');
  assert.equal(output[1].properties._merge_source, 'overlay');
  assert.match(html, /result=\{type:"FeatureCollection",features:mergeFeaturesPreservingGeometry\(f1,f2\)\}/);
});


test('Whole-layer Clip supports point and line inputs with polygon overlays', () => {
  assert.match(html, /function clipFeatureToPolygon\(feature,mask\)/);
  assert.match(html, /op==='clip'&&ovs\.some\(f=>!\['Polygon','MultiPolygon'\]/);
  assert.match(html, /clipFeatureToPolygon\(a,mask\)/);
  assert.match(html, /g\.type==="MultiPoint"/);
  assert.match(html, /g\.type!=="LineString"&&g\.type!=="MultiLineString"/);
});

test('ArcMap toolbar uses SVG editing icons and an X/Y coordinate glyph', () => {
  assert.match(html, /#editorToolbar #startEditingBtn \.arcmap-icon\{stroke:#4f8056\}/);
  assert.match(html, /#editorToolbar #saveEditsBtn \.arcmap-icon\{stroke:#286d9b\}/);
  assert.match(html, /#editorToolbar #stopEditingBtn \.arcmap-icon\{stroke:#b15a48\}/);
  assert.match(html, /<symbol id="arc-icon-goxy"[^>]*>[\s\S]*?M2 13\.5V3\.2/);
  assert.match(html, /title="Go To XY \(X\/Y coordinates\)" aria-label="Go To XY \(X\/Y coordinates\)"/);
});

test('ArcMap keyboard shortcuts invoke existing Attribute Table, Editor, and Measure actions', () => {
  assert.match(html, /__arcMapShortcutHandlerInstalled/);
  assert.match(html, /F6:document\.getElementById\("openTable"\)/);
  assert.match(html, /F2:document\.getElementById\("startEditingBtn"\)/);
  assert.match(html, /F7:document\.getElementById\("measureToolBtn"\)/);
  assert.match(html, /if\(typing\|\|event\.altKey\|\|event\.ctrlKey\|\|event\.metaKey\)return/);
});
