'use strict';
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const test=require('node:test');
const assert=require('node:assert/strict');
const root=path.join(__dirname,'..','sabzroyanuraman-site');
const html=fs.readFileSync(path.join(root,'web-gis.html'),'utf8');
const source=fs.readFileSync(path.join(root,'webgis-attribute-field-calculator.js'),'utf8');
const sandbox={window:{}};
vm.runInNewContext(source,sandbox,{filename:'webgis-attribute-field-calculator.js'});
const api=sandbox.window.SabzAttributeFieldCalculator;
function fixtureContext({selected=[],expression='!area_ha! * 2',confirm=true}={}){
 const layer={};
 const items=[
  {layer:{id:1},feature:{properties:{name:'North',area_ha:12.5}}},
  {layer:{id:2},feature:{properties:{name:'South',area_ha:27}}}
 ];
 const state={undo:0,updates:[],messages:[],confirmation:null};
 const ctx={
  $:id=>id==='fillValue'?{value:''}:null,
  status:message=>state.messages.push(message),
  hasUnsavedTableEdits:()=>false,
  currentAttributeLayer:()=>layer,
  editSessionActive:true,
  editTarget:null,
  isLayerInEditTarget:()=>true,
  currentAttributeItems:()=>items,
  selectedTableIndexes:()=>selected,
  tableKeys:()=>['name','area_ha'],
  isComputedAttributeField:()=>false,
  arcMapDialog:{
   select:async options=>{state.selectMessage=options.message;return 'area_ha';},
   prompt:async()=>expression,
   confirm:async options=>{state.confirmation=options.message;return confirm;}
  },
  ensureFeatureProperties:layer=>items.find(item=>item.layer===layer).feature.properties,
  updateFeaturePopup:layer=>state.updates.push(layer.id),
  recordUndoState:()=>state.undo++,
  renderEditableTable:()=>{state.rendered=true;},
  flushGeodatabaseEdits:async()=>true,
  scheduleAutoSave:async()=>true
 };
 ctx.editTarget=ctx.currentAttributeLayer();
 return {ctx,items,state};
}
test('Calculate Field module loads before the legacy core and keeps delegate entry points',()=>{
 assert.doesNotThrow(()=>new vm.Script(source));
 assert.match(html,/<script src="\.\/webgis-attribute-field-calculator\.js\?v=20261109-field-calculator"><\/script>/);
 assert.match(html,/window\.SabzAttributeFieldCalculator\.create\(/);
 assert.match(html,/function evaluateAttributeFieldExpression\(expression,properties\)\{return attributeFieldCalculatorModule\.evaluateAttributeFieldExpression\(expression,properties\);\}/);
 assert.match(html,/\$\("fillSelected"\)\.onclick=\(\)=>attributeFieldCalculatorModule\.calculateField\(\);/);
});
test('Expression evaluator supports field references, arithmetic, and safe functions',()=>{
 assert.equal(api.evaluateAttributeFieldExpression('!POPULATION! / 100',{POPULATION:1250}).value,12.5);
 assert.equal(api.evaluateAttributeFieldExpression('UPPER(!name!)',{name:'North'}).value,'NORTH');
 assert.equal(api.evaluateAttributeFieldExpression('ROUND(!area!, 1)',{area:12.56}).value,12.6);
 assert.equal(api.evaluateAttributeFieldExpression("CONCAT(!first!, ' ', !last!)",{first:'Sabz',last:'Royan'}).value,'Sabz Royan');
});
test('Expression evaluator rejects unknown fields, arbitrary property access, and non-finite results',()=>{
 assert.throws(()=>api.evaluateAttributeFieldExpression('!missing! + 1',{area:1}),/Unknown field/);
 assert.throws(()=>api.evaluateAttributeFieldExpression('!name! + window.location',{name:'x'}),/Only field references/);
 assert.throws(()=>api.evaluateAttributeFieldExpression('!area! / 0',{area:1}),/invalid number/i);
});
test('No selected rows calculates every record in the current layer view and records one undo state',async()=>{
 const {ctx,items,state}=fixtureContext();
 const module=api.create(ctx);
 assert.equal(await module.calculateField(),true);
 assert.deepEqual(items.map(item=>item.feature.properties.area_ha),[25,54]);
 assert.equal(state.undo,1);
 assert.equal(state.updates.length,2);
 assert.match(state.selectMessage,/all records in the current layer view/);
 assert.match(state.confirmation,/2 record\(s\) in the current layer view/);
 assert.equal(state.rendered,true);
});
test('An explicit table selection limits calculation to selected records',async()=>{
 const {ctx,items,state}=fixtureContext({selected:[1],expression:'!area_ha! + 5'});
 await api.create(ctx).calculateField();
 assert.deepEqual(items.map(item=>item.feature.properties.area_ha),[12.5,32]);
 assert.match(state.selectMessage,/selected records/);
 assert.match(state.confirmation,/1 selected feature/);
});
test('Invalid expressions leave every record unchanged and do not create an undo entry',async()=>{
 const {ctx,items,state}=fixtureContext({expression:'!unknown_field! + 1'});
 assert.equal(await api.create(ctx).calculateField(),false);
 assert.deepEqual(items.map(item=>item.feature.properties.area_ha),[12.5,27]);
 assert.equal(state.undo,0);
 assert.match(state.messages.at(-1),/Unknown field/);
});
