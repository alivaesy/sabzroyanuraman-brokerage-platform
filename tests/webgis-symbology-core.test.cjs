"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const vm = require("node:vm");

const source = fs.readFileSync(
  require("node:path").join(__dirname, "../sabzroyanuraman-site/webgis-symbology-core.js"),
  "utf8"
);
const context = { window: {} };
vm.runInNewContext(source, context, { filename: "webgis-symbology-core.js" });
const ramp = context.window.SabzWebGISSymbologyCore.layerStyleRamp;

assert.deepEqual(Array.from(ramp("green", 1)), ["#31a354"]);
assert.deepEqual(Array.from(ramp("unknown", 1)), ["#31a354"]);
assert.equal(ramp("blue", 5).length, 5);
assert.equal(ramp("red", 7)[0], "#fee5d9");
assert.equal(ramp("red", 7)[6], "#67000d");
assert.equal(ramp("spectral", 3)[1], "#fdae61");
assert.equal(ramp("purple", 0).length, 1);
assert.equal(ramp("green", 4.9).length, 4);
console.log("WebGIS symbology core tests passed.");
