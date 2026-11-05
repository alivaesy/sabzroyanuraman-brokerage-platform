'use strict';

const assert = require('node:assert/strict');
const { chromium } = require('playwright');

const baseUrl = process.env.WEBGIS_BASE_URL || 'https://sabzroyanuraman.com/web-gis.html';
const layerName = '__AttributeTableRenderRegression__';
const fixture = {
  type: 'FeatureCollection',
  features: [
    {type:'Feature',id:'attr-1',properties:{name:'Regression parcel A',area_ha:12.5,parcel_id:'R-001'},geometry:{type:'Polygon',coordinates:[[[47.7575,33.6288],[47.7590,33.6288],[47.7590,33.6300],[47.7575,33.6300],[47.7575,33.6288]]]}},
    {type:'Feature',id:'attr-2',properties:{name:'Regression parcel B',area_ha:27.0,parcel_id:'R-002'},geometry:{type:'Polygon',coordinates:[[[47.7592,33.6288],[47.7607,33.6288],[47.7607,33.6300],[47.7592,33.6300],[47.7592,33.6288]]]}}
  ]
};

(async()=>{
  const browser=await chromium.launch({headless:true,args:['--no-sandbox']});
  const context=await browser.newContext({viewport:{width:1440,height:960}});
  const page=await context.newPage();
  const pageErrors=[],consoleErrors=[];
  page.on('pageerror',error=>pageErrors.push(error.message));
  page.on('console',message=>{
    if(message.type()!=='error')return;
    const text=message.text();
    if(/Failed to load resource|404|net::ERR/.test(text))return;
    consoleErrors.push(text);
  });
  try{
    const response=await page.goto(baseUrl,{waitUntil:'domcontentloaded',timeout:60000});
    assert.ok(response&&response.ok(),'WebGIS page must return HTTP success');
    await page.waitForFunction(()=>!!window.__sabzGISMap&&!!window.__sabzWebGISSharedBridge,null,{timeout:45000});
    await page.locator('.accordion-section[data-accordion="attribute"] .accordion-head').click();
    await page.evaluate(({name,data})=>{
      const bridge=window.__sabzWebGISSharedBridge;
      bridge.addGeoJSONLayer(name,data);
      bridge.refreshAttributeLayers?.();
      bridge.refreshEditorTargets?.();
      const index=bridge.namedLayers.findIndex(item=>item.name===name);
      if(index<0)throw new Error('Attribute Table regression layer was not registered');
      const attributeLayer=document.getElementById('attributeLayer');
      attributeLayer.value=String(index);
      attributeLayer.dispatchEvent(new Event('change',{bubbles:true}));
      const editTarget=document.getElementById('editTargetLayer');
      editTarget.value=String(index);
      editTarget.dispatchEvent(new Event('change',{bubbles:true}));
    },{name:layerName,data:fixture});
    await page.locator('#startEditingBtn').click();
    await page.locator('#openTable').click();
    await page.locator('#attributeTableWrap th[data-sort-key="area_ha"]').click();
    let firstName=await page.locator('#attributeTableWrap tbody tr').first().locator('input.cell-edit[data-key="name"]').inputValue();
    assert.equal(firstName,'Regression parcel A','Attribute Table must sort numeric values ascending');
    await page.locator('#attributeTableWrap th[data-sort-key="area_ha"]').click();
    firstName=await page.locator('#attributeTableWrap tbody tr').first().locator('input.cell-edit[data-key="name"]').inputValue();
    assert.equal(firstName,'Regression parcel B','Second sort click must reverse the sort direction');
    await page.locator('#attributeTableWrap tbody tr').first().locator('input.cell-edit[data-key="name"]').fill('Regression parcel B edited');
    await page.locator('#saveTableEdits').click();
    await page.waitForFunction(name=>{
      const owner=window.__sabzWebGISSharedBridge.namedLayers.find(item=>item.name===name);
      return owner?.layer?.getLayers?.().some(layer=>layer.feature?.properties?.parcel_id==='R-002'&&layer.feature.properties.name==='Regression parcel B edited');
    },layerName,{timeout:10000});
    const result=await page.evaluate(name=>{
      const owner=window.__sabzWebGISSharedBridge.namedLayers.find(item=>item.name===name);
      const edited=owner.layer.getLayers().find(layer=>layer.feature?.properties?.parcel_id==='R-002');
      return {editedName:edited?.feature?.properties?.name,sortHeader:document.querySelector('#attributeTableWrap th[data-sort-key="area_ha"]')?.textContent};
    },layerName);
    assert.equal(result.editedName,'Regression parcel B edited','Saving a cell edit must update the feature properties');
    assert.match(result.sortHeader,/▼/,'The table must retain descending sort state after saving');
    await page.locator('#stopEditingBtn').click();
    assert.deepEqual(pageErrors,[],'Attribute Table actions must not cause uncaught browser errors');
    assert.deepEqual(consoleErrors,[],'Attribute Table actions must not cause unexpected console errors');
    console.log('PASS — Attribute Table delegate, numeric ascending/descending sort, cell edit/save, sort-state retention.');
    console.log('Browser page errors: '+JSON.stringify(pageErrors));
    console.log('Browser console errors: '+JSON.stringify(consoleErrors));
  }finally{
    await browser.close();
  }
})().catch(error=>{console.error(error);process.exitCode=1;});
