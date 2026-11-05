"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const source = fs.readFileSync(
  path.join(__dirname, "../sabzroyanuraman-site/webgis-labeling-core.js"),
  "utf8"
);
const context = { window: {} };
vm.runInNewContext(source, context, { filename: "webgis-labeling-core.js" });
const core = context.window.SabzWebGISLabelingCore;

assert.deepEqual({ ...core.normalizeLabelConfig({ enabled: true, field: "name", size: 18, color: "#abcdef" }) },
  { enabled: true, field: "name", size: 18, color: "#abcdef", placement: "auto", minZoom: 0, density: "balanced" });
assert.equal(core.normalizeLabelConfig({ size: 2 }).size, 8);
assert.equal(core.normalizeLabelConfig({ size: 90 }).size, 32);
assert.equal(core.normalizeLabelConfig({ size: "invalid" }).size, 12);
assert.equal(core.normalizeLabelConfig({ color: "red" }).color, "#202020");
assert.equal(core.normalizeLabelConfig({ enabled: 1 }).enabled, false);
assert.equal(core.labelText(null), null);
assert.equal(core.labelText(undefined), null);
assert.equal(core.labelText("   "), null);
assert.equal(core.labelText(0), "0");
assert.equal(core.labelText(false), "false");
assert.equal(core.labelText({ code: 7 }), '{"code":7}');
const labelCss = core.labelClassCss("layer-label", { size: 14, color: "#123456" });
assert.match(labelCss, /font-size:14px/);
assert.match(labelCss, /text-shadow:/, "Labels should use a readable white halo like ArcMap");
assert.match(labelCss, /background:transparent/, "Labels should not render as boxed tooltips");
assert.match(labelCss, /:before/, "Leaflet tooltip arrow must be hidden");
assert.match(core.labelClassCss("bad class!", { size: 14, color: "#123456" }), /^\.badclass\{/);
console.log("WebGIS labeling core tests passed.");

assert.equal(core.normalizeLabelConfig({ placement: "right" }).placement, "right");
assert.equal(core.normalizeLabelConfig({ placement: "invalid" }).placement, "auto");
const pointPlacement = core.labelPlacement("Point");
assert.deepEqual({ direction: pointPlacement.direction, offset: Array.from(pointPlacement.offset) }, { direction: "top", offset: [0, -8] });
const polygonPlacement = core.labelPlacement("Polygon");
assert.deepEqual({ direction: polygonPlacement.direction, offset: Array.from(polygonPlacement.offset) }, { direction: "center", offset: [0, 0] });
const linePlacement = core.labelPlacement("LineString", "left");
assert.deepEqual({ direction: linePlacement.direction, offset: Array.from(linePlacement.offset) }, { direction: "left", offset: [-8, 0] });

const visibleLabels = core.selectVisibleLabels([
  {left:0,top:0,right:40,bottom:12},
  {left:10,top:2,right:50,bottom:14},
  {left:100,top:0,right:140,bottom:12}
]);
assert.deepEqual(Array.from(visibleLabels), [0, 2], "Overlapping labels should be decluttered while separated labels remain visible");
assert.deepEqual(Array.from(core.selectVisibleLabels([
  {left:0,top:0,right:40,bottom:12},
  {left:10,top:2,right:50,bottom:14}
], {density:"dense"})), [0], "Dense mode should still prevent direct label overlap");
assert.deepEqual(Array.from(core.selectVisibleLabels([
  {left:0,top:0,right:40,bottom:12,density:"dense"},
  {left:50,top:0,right:90,bottom:12,density:"sparse"}
])), [0], "Per-layer sparse padding must be respected in a shared collision pass");
assert.deepEqual(Array.from(core.selectVisibleLabels([
  {left:0,top:0,right:40,bottom:12,density:"dense"},
  {left:50,top:0,right:90,bottom:12,density:"dense"}
])), [0, 1], "Dense labels in different layers may remain visible when their boxes do not overlap");
assert.equal(core.normalizeLabelConfig({minZoom:30,density:"sparse"}).minZoom,22);
assert.equal(core.normalizeLabelConfig({minZoom:-2,density:"invalid"}).minZoom,0);
assert.equal(core.normalizeLabelConfig({density:"invalid"}).density,"balanced");


const stressCandidates = Array.from({length:10000}, (_, index) => {
  const column = index % 100;
  const row = Math.floor(index / 100);
  const left = column * 48;
  const top = row * 24;
  return {left, top, right:left + 32, bottom:top + 12};
});
const stressStart = Date.now();
const stressVisible = core.selectVisibleLabels(stressCandidates, {density:"balanced"});
const stressElapsed = Date.now() - stressStart;
assert.equal(stressVisible.length, 10000, "Separated labels in a 10,000-feature layer should remain visible");
assert.ok(stressElapsed < 5000, "10,000-label spatial-grid pass should complete within five seconds (actual "+stressElapsed+"ms)");
const crowdedCandidates = Array.from({length:1500}, () => ({left:10,top:10,right:90,bottom:30}));
assert.equal(core.selectVisibleLabels(crowdedCandidates).length, 1,
  "A dense cluster of identical label boxes should retain only one label");
console.log("Label decluttering stress: 10,000 dispersed + 1,500 coincident labels in "+stressElapsed+"ms.");
