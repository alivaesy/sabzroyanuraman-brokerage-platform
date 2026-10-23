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
