'use strict';
const assert=require('node:assert/strict');
const path=require('node:path');
const {chromium}=require('playwright');

(async()=>{
 const browser=await chromium.launch({headless:true,args:['--no-sandbox']});
 const page=await browser.newPage({viewport:{width:1440,height:960}});
 const errors=[];
 const consoleErrors=[];
 page.on('pageerror',e=>errors.push(e.message));
 page.on('console',message=>{if(message.type()==='error')consoleErrors.push(message.text());});
 try{
  const response=await page.goto(process.env.WEBGIS_BASE_URL||'http://127.0.0.1:4173/web-gis.html',{waitUntil:'domcontentloaded',timeout:60000});
  assert.ok(response&&response.ok(),'WebGIS should load');
  await page.waitForFunction(()=>!!window.SabzArcMapManifest&&!!document.getElementById('arcMapManifestInput'),null,{timeout:45000});
  const catalogLayout=await page.evaluate(()=>{
   const toc=document.querySelector('#panel .layer-section');
   return {
    tocTitle:toc?.querySelector(':scope > h3')?.textContent?.trim()||null,
    tocSectionCount:document.querySelectorAll('#panel > .layer-section').length,
    injectedPanels:document.querySelectorAll('#panel #arcMapImportPanel,#panel #gdbImportControl,#panel #gdbTablesCatalog').length,
    manifestInCatalog:!!document.querySelector('[data-catalog-section="documents"] #arcMapManifestButton'),
    repairInCatalog:!!document.querySelector('[data-catalog-section="documents"] #arcMapRepairButton'),
    gdbZipInCatalog:!!document.querySelector('[data-catalog-section="geodatabase"] #gdbCatalogImportZipButton'),
    tablesInCatalog:!!document.querySelector('[data-catalog-section="geodatabase"] #gdbTablesCatalog')
   };
  });
  assert.equal(catalogLayout.tocTitle,'Table Of Contents','TOC should contain only its own title');
  assert.equal(catalogLayout.tocSectionCount,1,'TOC should have one single layer section');
  assert.equal(catalogLayout.injectedPanels,0,'Catalog modules must not inject panels into the TOC');
  assert.ok(catalogLayout.manifestInCatalog&&catalogLayout.repairInCatalog&&catalogLayout.gdbZipInCatalog&&catalogLayout.tablesInCatalog,
    'ArcMap and FileGDB controls must live in the Catalog dock');
  await page.locator('[data-catalog-section="documents"] .accordion-head').click();
  await page.locator('#arcMapManifestInput').setInputFiles(path.join(__dirname,'fixtures','sample.manifest.json'));
  await page.waitForFunction(()=>window.__sabzWebGISSharedBridge.namedLayers.filter(x=>x.sourceMissing).length===3,null,{timeout:15000});
  const initial=await page.evaluate(()=>window.__sabzWebGISSharedBridge.namedLayers.slice(-3).map(x=>({name:x.name,missing:x.sourceMissing,group:x.drawingSet,features:x.layer.getLayers().length,frameCrs:x._arcMapManifest.frameSpatialReference})));
  assert.deepEqual(initial.map(x=>x.name),['Parcels','National Lands','Roads']);
  assert.ok(initial.every(x=>x.missing&&x.features===0),'MXD layers should be placeholders until sources are repaired');
  assert.equal(initial[0].frameCrs,'EPSG:32638','MXD data frame projected CRS should be preserved in layer metadata');
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
  const tableValues=await page.locator('#attributeTableWrap input.cell-edit').evaluateAll(inputs=>inputs.map(input=>input.value));
  assert.ok(tableValues.includes('P-001'),'Repaired geometry attributes should be available as editable table cell values');
  // Repair a second unresolved MXD layer directly from a FileGDB feature class.
  await page.locator('#arcMapRepairLayer').selectOption('Roads');
  await page.locator('#arcMapRepairFile').setInputFiles(path.join(__dirname,'fixtures','sample-gdb.zip'));
  await page.locator('#arcMapRepairButton').click();
  await page.waitForFunction(()=>document.getElementById('sabzGdbPicker')?.classList.contains('show'),null,{timeout:60000});
  const repairEntry=page.locator('#sabzGdbLayerList .sabz-gdb-entry').filter({hasText:'faramarz2'}).first();
  await repairEntry.locator('input[type="checkbox"]').check();
  await page.locator('#sabzGdbImport').click();
  await page.waitForFunction(()=>window.__sabzWebGISSharedBridge.namedLayers.some(x=>x.name==='Roads'&&!x.sourceMissing&&x.layer.getLayers().length>0),null,{timeout:90000});
  const repairedFromGdb=await page.evaluate(()=>{
   const item=window.__sabzWebGISSharedBridge.namedLayers.find(x=>x.name==='Roads');
   return {missing:item.sourceMissing,group:item.drawingSet,sourceType:item._repairReport?.sourceType,count:item.layer.getLayers().length};
  });
  assert.deepEqual(repairedFromGdb,{missing:false,group:'Thematic',sourceType:'FileGDB',count:repairedFromGdb.count});
  assert.ok(repairedFromGdb.count>0,'FileGDB repair should load real line geometry into the original MXD layer');

  // Import the real FileGDB fixture, select one feature class, and verify the Feature Dataset group.
  await page.locator('#gdbArchiveInput').setInputFiles(path.join(__dirname,'fixtures','sample-gdb.zip'));
  await page.waitForFunction(()=>document.getElementById('sabzGdbPicker')?.classList.contains('show'),null,{timeout:60000});
  const catalogNames=await page.locator('#sabzGdbLayerList .sabz-gdb-entry').allInnerTexts();
  assert.ok(catalogNames.some(x=>x.includes('ssdsd')),'Real GDB catalog should expose feature class ssdsd');
  assert.ok(catalogNames[0].includes('hamidazizi')&&catalogNames[1].includes('sa')&&catalogNames[2].includes('ssdsd'),'GDB catalog should follow root and Feature Dataset order from the original geodatabase');
  await page.locator('#sabzGdbSelectNone').click();
  const selectedEntry=page.locator('#sabzGdbLayerList .sabz-gdb-entry').filter({hasText:'ssdsd'}).first();
  await selectedEntry.locator('input[type="checkbox"]').check();
  await page.locator('#sabzGdbImport').click();
  await page.waitForFunction(()=>window.__sabzWebGISSharedBridge.namedLayers.some(x=>x.name.includes('ssdsd')&&x.layer.getLayers().length>0),null,{timeout:90000});
  const gdbResult=await page.evaluate(()=>{
   const b=window.__sabzWebGISSharedBridge;
   const item=b.namedLayers.find(x=>x.name.includes('ssdsd'));
   return {name:item?.name,group:item?.drawingSet,displayName:item?.displayName,count:item?.layer?.getLayers?.().length,sourceCRS:item?.layer?._gdbMetadata?.sourceCRS,report:window.__sabzLastGdbImportReport||null};
  });
  assert.ok(gdbResult.count>0,'Selected GDB feature class should render real geometries');
  assert.equal(gdbResult.group,'zone38','Feature Dataset zone38 should be restored as a TOC group');
  assert.equal(gdbResult.displayName,'ssdsd','Grouped layer should use its short feature-class name');
  assert.equal(gdbResult.sourceCRS,'EPSG:32638','Original projected GDB CRS should be retained as layer metadata');
  assert.equal(gdbResult.report?.sourceFeatureClasses,17,'GDAL full catalog should include all 17 user feature classes, including nested Feature Dataset members');
  assert.equal(gdbResult.report?.selectedFeatureClasses,1,'Only the checked feature class should be imported');
  assert.equal(gdbResult.report?.importedLayers,1,'One selected feature class should produce one map layer');
  assert.ok(!(gdbResult.report?.tables||[]).some(table=>/^a[0-9a-f]+$/i.test(table.name)),'Internal GDB tables must not appear in the Tables catalog');


  assert.deepEqual(errors,[],'No uncaught JavaScript errors');
  console.log('Catalog/TOC DOM:',JSON.stringify(catalogLayout));
  console.log('Browser console errors:',JSON.stringify(consoleErrors));
  console.log('PASS — TOC stays clean; Catalog owns ArcMap and FileGDB controls; manifest import, source repair, FileGDB repair and table import complete in Chromium.');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
