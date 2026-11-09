'use strict';
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const test=require('node:test');
const assert=require('node:assert/strict');
const root=path.join(__dirname,'..','sabzroyanuraman-site');
const html=fs.readFileSync(path.join(root,'web-gis.html'),'utf8');
const source=fs.readFileSync(path.join(root,'webgis-attribute-table-window.js'),'utf8');
test('Attribute Table window module parses and is loaded before the legacy core',()=>{
 assert.doesNotThrow(()=>new vm.Script(source));
 assert.match(html,/<script src="\.\/webgis-attribute-table-window\.js\?v=20261109-attribute-window"><\/script>/);
 assert.ok(html.indexOf('webgis-attribute-table-window.js')<html.indexOf('Main ArcMap-style dockable attribute table'));
});
test('Legacy Attribute Table window entry points delegate to the extracted module',()=>{
 assert.match(html,/window\.SabzAttributeTableWindow\.create\(/);
 assert.match(html,/function ensureAttributeTableWindow\(\)\{return attributeTableWindowModule\.ensureAttributeTableWindow\(\);\}/);
 assert.match(html,/function showAttributeTableWindow\(\)\{return attributeTableWindowModule\.showAttributeTableWindow\(\);\}/);
 assert.match(html,/function updateAttributeTableWindowInfo\(\)\{return attributeTableWindowModule\.updateAttributeTableWindowInfo\(\);\}/);
 for(const action of ['start-edit','save','add','delete-field','delete','copy','paste','calculate','refresh'])assert.match(source,new RegExp('data-table-action="' + action + '"'));
});
