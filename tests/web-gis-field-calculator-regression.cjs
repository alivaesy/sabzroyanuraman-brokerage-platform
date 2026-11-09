'use strict';
const assert=require('node:assert/strict');
const {chromium}=require('playwright');
const baseUrl=process.env.WEBGIS_BASE_URL||'https://sabzroyanuraman.com/web-gis.html';
const layerName='__CalculateFieldRegression__';
const fixture={type:'FeatureCollection',features:[
 {type:'Feature',id:'calc-1',properties:{name:'North parcel',area_ha:12.5,parcel_id:'C-001'},geometry:{type:'Polygon',coordinates:[[[47.7575,33.6288],[47.7590,33.6288],[47.7590,33.6300],[47.7575,33.6300],[47.7575,33.6288]]]}},
 {type:'Feature',id:'calc-2',properties:{name:'South parcel',area_ha:27,parcel_id:'C-002'},geometry:{type:'Polygon',coordinates:[[[47.7592,33.6288],[47.7607,33.6288],[47.7607,33.6300],[47.7592,33.6300],[47.7592,33.6288]]]}}
]};
(async()=>{
 const browser=await chromium.launch({headless:true,args:['--no-sandbox']});
 const context=await browser.newContext({viewport:{width:1440,height:960}});
 const page=await context.newPage();
 const pageErrors=[],consoleErrors=[];
 page.on('pageerror',error=>pageErrors.push(error.message));
 page.on('console',message=>{if(message.type()!=='error')return;const value=message.text();if(/Failed to load resource|404|net::ERR/.test(value))return;consoleErrors.push(value);});
 try{
  const response=await page.goto(baseUrl,{waitUntil:'domcontentloaded',timeout:60000});
  assert.ok(response&&response.ok(),'WebGIS page must return HTTP success');
  await page.waitForFunction(()=>!!window.__sabzGISMap&&!!window.__sabzWebGISSharedBridge,null,{timeout:45000});
  await page.locator('#toolboxToggleBtn').click();
  const accordion=page.locator('.accordion-section[data-accordion="attribute"] .accordion-head');
  await accordion.waitFor({state:'visible',timeout:10000});
  await accordion.click();
  await page.evaluate(({name,data})=>{
   const bridge=window.__sabzWebGISSharedBridge;
   bridge.addGeoJSONLayer(name,data);bridge.refreshAttributeLayers?.();bridge.refreshEditorTargets?.();
   const index=bridge.namedLayers.findIndex(item=>item.name===name);
   if(index<0)throw new Error('Calculate Field fixture layer was not registered');
   for(const id of ['attributeLayer','editTargetLayer']){
    const control=document.getElementById(id);control.value=String(index);control.dispatchEvent(new Event('change',{bubbles:true}));
   }
  },{name:layerName,data:fixture});
  await page.locator('#startEditingBtn').click();
  await page.locator('#openTable').click();
  const tableWindow=page.locator('#attributeTableWindow');
  assert.ok(await tableWindow.isVisible(),'Calculate Field must run from the dockable Attribute Table');
  const menu=tableWindow.locator('.attribute-window-menu');
  async function runCalculation(expression){
   if(!(await menu.locator('summary').evaluate(node=>node.parentElement.open)))await menu.locator('summary').click();
   await menu.locator('[data-table-action="calculate"]').click();
   const input=page.locator('#arcToolDialogInput');
   await input.waitFor({state:'visible',timeout:10000});
   await input.selectOption({label:'area_ha'});
   await page.locator('#arcToolDialogOk').click();
   await input.waitFor({state:'visible',timeout:10000});
   await input.fill(expression);
   await page.locator('#arcToolDialogOk').click();
  }
  async function readValues(){
   return page.evaluate(name=>{
    const owner=window.__sabzWebGISSharedBridge.namedLayers.find(item=>item.name===name);
    return owner.layer.getLayers().map(layer=>({id:layer.feature.properties.parcel_id,value:layer.feature.properties.area_ha}));
   },layerName);
  }
  // ArcMap behavior: no checked table rows means all records in the current layer view.
  await runCalculation('!area_ha! * 2');
  await page.locator('#arcToolDialogOk').waitFor({state:'visible',timeout:10000});
  await page.locator('#arcToolDialogOk').click();
  await page.waitForFunction(name=>{
   const owner=window.__sabzWebGISSharedBridge.namedLayers.find(item=>item.name===name);
   const values=owner.layer.getLayers().map(layer=>layer.feature.properties.area_ha);
   return values[0]===25&&values[1]===54;
  },layerName,{timeout:10000});
  assert.deepEqual((await readValues()).map(x=>x.value),[25,54],
   'With no selected rows, Calculate Field must update every record in the active layer view');
  await page.locator('#undoBtn').click();
  await page.waitForFunction(name=>{
   const owner=window.__sabzWebGISSharedBridge.namedLayers.find(item=>item.name===name);
   const values=owner.layer.getLayers().map(layer=>layer.feature.properties.area_ha);
   return values[0]===12.5&&values[1]===27;
  },layerName,{timeout:10000});
  assert.deepEqual((await readValues()).map(x=>x.value),[12.5,27],
   'Calculate Field must create one undoable edit operation');
  // An explicit row selection must limit the calculation to that record.
  const rows=page.locator('#attributeTableWrap tbody tr[data-index]');
  await rows.nth(0).locator('td').nth(1).click();
  await runCalculation('!area_ha! + 5');
  await page.locator('#arcToolDialogOk').waitFor({state:'visible',timeout:10000});
  await page.locator('#arcToolDialogOk').click();
  await page.waitForFunction(name=>{
   const owner=window.__sabzWebGISSharedBridge.namedLayers.find(item=>item.name===name);
   const values=owner.layer.getLayers().map(layer=>layer.feature.properties.area_ha);
   return values[0]===17.5&&values[1]===27;
  },layerName,{timeout:10000});
  assert.deepEqual((await readValues()).map(x=>x.value),[17.5,27],
   'When rows are selected, only the selected records may be calculated');
  // Invalid expressions must stop before confirmation or mutation.
  await runCalculation('!missing_field! + 1');
  await page.waitForFunction(()=>/Unknown field: missing_field/.test(document.getElementById('status')?.textContent||''),null,{timeout:10000});
  assert.deepEqual((await readValues()).map(x=>x.value),[17.5,27],
   'An invalid expression must leave every source record unchanged');
  await page.locator('#stopEditingBtn').click();
  assert.deepEqual(pageErrors,[],'Calculate Field must not produce uncaught browser errors');
  assert.deepEqual(consoleErrors,[],'Calculate Field must not produce unexpected console errors');
  console.log('PASS — Calculate Field all-records scope, selected-row scope, expression validation, Undo and autosave.');
  console.log('Browser page errors: '+JSON.stringify(pageErrors));
  console.log('Browser console errors: '+JSON.stringify(consoleErrors));
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
