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
  { enabled: true, field: "name", size: 18, color: "#abcdef" });
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
assert.match(core.labelClassCss("layer-label", { size: 14, color: "#123456" }), /font-size:14px/);
assert.match(core.labelClassCss("bad class!", { size: 14, color: "#123456" }), /^\.badclass\{/);
console.log("WebGIS labeling core tests passed.");
