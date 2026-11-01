'use strict';
const assert=require('node:assert/strict');
const path=require('node:path');
const {chromium}=require('playwright');

(async()=>{
 const browser=await chromium.launch({headless:true,args:['--no-sandbox']});
 const page=await browser.newPage({viewport:{width:1440,height:960}});
 const errors=[];
 page.on('pageerror',e=>errors.push(e.message));
 try{
  const response=await page.goto(process.env.WEBGIS_BASE_URL||'http://127.0.0.1:4173/web-gis.html',{waitUntil:'domcontentloaded',timeout:60000});
  assert.ok(response&&response.ok(),'WebGIS should load');
  await page.waitForFunction(()=>!!window.SabzArcMapManifest&&!!document.getElementById('arcMapManifestInput'),null,{timeout:45000});
  await page.locator('#arcMapManifestInput').setInputFiles(path.join(__dirname,'fixtures','sample.manifest.json'));
  await page.waitForFunction(()=>window.__sabzWebGISSharedBridge.namedLayers.filter(x=>x.sourceMissing).length===3,null,{timeout:15000});
  const initial=await page.evaluate(()=>window.__sabzWebGISSharedBridge.namedLayers.slice(-3).map(x=>({name:x.name,missing:x.sourceMissing,group:x.drawingSet,features:x.layer.getLayers().length})));
  assert.deepEqual(initial.map(x=>x.name),['Parcels','National Lands','Roads']);
  assert.ok(initial.every(x=>x.missing&&x.features===0),'MXD layers should be placeholders until sources are repaired');
  assert.ok(initial[1].group==='Thematic'&&initial[2].group==='Thematic','Manifest group metadata should be retained');
  await page.locator('#arcMapRepairLayer').selectOption('National Lands');
  await page.locator('#arcMapRepairFile').setInputFiles(path.join(__dirname,'fixtures','sample-layer.geojson'));
  await page.locator('#arcMapRepairButton').click();
  await page.waitForFunction(()=>window.__sabzWebGISSharedBridge.namedLayers.some(x=>x.name==='National Lands'&&!x.sourceMissing&&x.layer.getLayers().length===1),null,{timeout:15000});
  const repaired=await page.evaluate(()=>{
   const b=window.__sabzWebGISSharedBridge;
   const item=b.namedLayers.find(x=>x.name==='National Lands');
   const feature=item.layer.toGeoJSON().features[0];
   return {missing:item.sourceMissing,count:item.layer.getLayers().length,parcelId:feature.properties.parcel_id,source:item.sourceName};
  });
  assert.deepEqual({missing:repaired.missing,count:repaired.count,parcelId:repaired.parcelId},{missing:false,count:1,parcelId:'P-001'});
  assert.equal(repaired.source,'sample-layer.geojson');
  const table=await page.evaluate(()=>{
   const b=window.__sabzWebGISSharedBridge;
   const index=b.namedLayers.findIndex(x=>x.name==='National Lands');
   const select=document.getElementById('attributeLayer');
   if(!select)return {available:false};
   select.value=String(index);select.dispatchEvent(new Event('change',{bubbles:true}));
   document.getElementById('openTable')?.click();
   return {available:true,index};
  });
  assert.ok(table.available,'Attribute table selector must be available');
  await page.waitForTimeout(400);
  const tableText=await page.locator('#attributeTableWrap').innerText().catch(()=> '');
  assert.match(tableText,/P-001/,'Repaired geometry attributes should be available in the attribute table');
  // Import the real FileGDB fixture, select one feature class, and verify the Feature Dataset group.
  await page.locator('#gdbArchiveInput').setInputFiles(path.join(__dirname,'fixtures','sample-gdb.zip'));
  await page.waitForFunction(()=>document.getElementById('sabzGdbPicker')?.classList.contains('show'),null,{timeout:60000});
  const catalogNames=await page.locator('#sabzGdbLayerList .sabz-gdb-entry').allInnerTexts();
  assert.ok(catalogNames.some(x=>x.includes('ssdsd')),'Real GDB catalog should expose feature class ssdsd');
  await page.locator('#sabzGdbSelectNone').click();
  const selectedEntry=page.locator('#sabzGdbLayerList .sabz-gdb-entry').filter({hasText:'ssdsd'}).first();
  await selectedEntry.locator('input[type="checkbox"]').check();
  await page.locator('#sabzGdbImport').click();
  await page.waitForFunction(()=>window.__sabzWebGISSharedBridge.namedLayers.some(x=>x.name.includes('ssdsd')&&x.layer.getLayers().length>0),null,{timeout:90000});
  const gdbResult=await page.evaluate(()=>{
   const b=window.__sabzWebGISSharedBridge;
   const item=b.namedLayers.find(x=>x.name.includes('ssdsd'));
   return {name:item?.name,group:item?.drawingSet,displayName:item?.displayName,count:item?.layer?.getLayers?.().length,report:window.__sabzLastGdbImportReport||null};
  });
  assert.ok(gdbResult.count>0,'Selected GDB feature class should render real geometries');
  assert.equal(gdbResult.group,'zone38','Feature Dataset zone38 should be restored as a TOC group');
  assert.equal(gdbResult.displayName,'ssdsd','Grouped layer should use its short feature-class name');

  assert.deepEqual(errors,[],'No uncaught JavaScript errors');
  console.log('PASS — ArcMap manifest creates 3 ordered source placeholders; Repair Data Source loads polygon geometry and attributes; attribute table reads repaired row.');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
