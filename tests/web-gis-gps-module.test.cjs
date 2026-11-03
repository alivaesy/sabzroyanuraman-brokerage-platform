'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const site = path.join(__dirname, '..', 'sabzroyanuraman-site');
const html = fs.readFileSync(path.join(site, 'web-gis.html'), 'utf8');
const moduleSource = fs.readFileSync(path.join(site, 'webgis-gps-tracking.js'), 'utf8');

test('GPS tracking is loaded as a standalone classic script after the WebGIS core', () => {
  const coreIndex = html.indexOf('window.__sabzWebGISSharedBridge=');
  const moduleIndex = html.indexOf('webgis-gps-tracking.js');
  assert.ok(coreIndex >= 0, 'WebGIS shared bridge must be initialized in the core script');
  assert.ok(moduleIndex > coreIndex, 'GPS module must load after the shared bridge owner');
  assert.match(html, /window\.SabzGpsTrack/);
  assert.doesNotMatch(html, /function goXYGps(?:Start|Stop|Point|Send)\s*\(/);
});

test('Go To XY delegates GPS actions to the extracted module API', () => {
  for (const method of ['start', 'stop', 'exportKml', 'clear']) {
    assert.ok(html.includes('gpsTrack?.' + method), 'Go To XY must delegate ' + method);
    assert.ok(moduleSource.includes('function ' + method + '('), 'GPS module must implement ' + method);
  }
  assert.match(html, /gpsTrack\?\.active/);
  assert.match(moduleSource, /get active\(\)/);
  assert.match(moduleSource, /get pointCount\(\)/);
});

test('GPS module preserves explicit consent and same-origin server tracking', () => {
  assert.match(moduleSource, /window\.confirm\(/);
  assert.match(moduleSource, /navigator\.geolocation\.watchPosition/);
  assert.match(moduleSource, /credentials:\s*"same-origin"/);
  assert.match(moduleSource, /\/api\/location\/track\.php/);
  for (const action of ['start', 'point', 'stop']) assert.ok(moduleSource.includes('action:"' + action + '"'));
  assert.match(moduleSource, /Previously uploaded server records are not deleted/);
});
