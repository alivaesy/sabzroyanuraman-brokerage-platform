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

const core = context.window.SabzWebGISSymbologyCore;
assert.deepEqual(Array.from(core.numericValues([null, "", " ", "2", 3, "x"])), [2, 3]);
assert.deepEqual(Array.from(core.uniqueValueClasses(["A", null, "B", "A", ""], "blue"), item => ({ value: item.value, count: item.count, color: item.color })), [
  { value: "A", count: 2, color: "#eff3ff" },
  { value: "B", count: 1, color: "#08306b" }
]);

const equal = core.graduatedClasses([null, "", "10", 20, 30, "bad"], 2, "green");
assert.deepEqual(Array.from(equal, x => [x.min, x.max]), [[10, 20], [20, 30]]);
const quantile = core.graduatedClasses([1, 2, 3, 4, 5, 6], 3, "blue", "quantile");
assert.deepEqual(Array.from(quantile, x => [x.min, x.max]), [[1, 2], [2, 4], [4, 6]]);

// Tied observations must never create duplicate quantile breaks or empty classes.
const tiedQuantile = core.graduatedClasses([1, 1, 1, 1, 2, 2], 4, "blue", "quantile");
assert.deepEqual(Array.from(tiedQuantile, x => x.max), [1, 2]);
assert.equal(tiedQuantile.length, 2);

const unevenTies = core.graduatedClasses([1, 2, 2, 2, 3, 4], 4, "blue", "quantile");
assert.deepEqual(Array.from(unevenTies, x => x.max), [1, 2, 3, 4]);
assert.equal(new Set(Array.from(unevenTies, x => x.max)).size, unevenTies.length);
for (const value of [1, 2, 3, 4]) {
  assert.notEqual(core.classifyGraduatedValue(value, unevenTies), -1);
}

const singleDistinct = core.graduatedClasses([7, 7, 7, 7], 5, "green", "quantile");
assert.equal(singleDistinct.length, 1);
assert.deepEqual(Array.from(singleDistinct, x => [x.min, x.max]), [[7, 7]]);

const emptyQuantile = core.graduatedClasses([null, "", "bad"], 5, "green", "quantile");
assert.deepEqual(Array.from(emptyQuantile), []);

assert.equal(core.classifyGraduatedValue(null, equal), -1);
assert.equal(core.classifyGraduatedValue("", equal), -1);
assert.equal(core.classifyGraduatedValue("bad", equal), -1);
assert.equal(core.classifyGraduatedValue(20, equal), 0);
assert.equal(core.classifyGraduatedValue(30, equal), 1);
