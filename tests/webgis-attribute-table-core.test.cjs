"use strict";
const assert=require("node:assert/strict");
const fs=require("node:fs");
const path=require("node:path");
const vm=require("node:vm");
const source=fs.readFileSync(path.join(__dirname,"../sabzroyanuraman-site/webgis-attribute-table-core.js"),"utf8");
const context={window:{}};
vm.runInNewContext(source,context,{filename:"webgis-attribute-table-core.js"});
const core=context.window.SabzWebGISAttributeTableCore;
assert.equal(core.isReservedField("_internal"),true);
assert.equal(core.isReservedField("geometry"),true);
assert.equal(core.isReservedField("Name"),false);
assert.equal(core.isComputedField("AREA_M2"),true);
assert.equal(core.isComputedField("Length_m"),true);
assert.equal(core.isComputedField("owner"),false);
assert.equal(core.isProtectedField("x"),true);
assert.deepEqual(Array.from(core.collectFields(
 ["OBJECTID",{name:"Name"},{name:"geometry"},{name:"_cache"}],
 [{Name:"A",owner:"B",geometry:{type:"Point"},_cache:1},{owner:"C",area_m2:10}]
)),["OBJECTID","Name","owner","area_m2"]);
assert.equal(core.compareValues(3,8),-5);
assert.equal(core.compareValues(false,true),-1);
assert.ok(core.compareValues("Parcel 2","Parcel 10")<0);
assert.ok(core.compareValues(null,"A")>0,"Empty values sort after populated values");
assert.equal(core.compareValues(null,""),0);
console.log("WebGIS attribute table core tests passed.");
