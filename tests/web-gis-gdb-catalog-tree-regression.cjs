'use strict';
const assert=require('node:assert/strict');
const {chromium}=require('playwright');
(async()=>{
 const browser=await chromium.launch({headless:true,args:['--no-sandbox']});
 const page=await browser.newPage({viewport:{width:1440,height:960}});
 const pageErrors=[],consoleErrors=[];
 page.on('pageerror',e=>pageErrors.push(e.message));
 page.on('console',m=>{if(m.type()==='error')consoleErrors.push(m.text())});
 try{
  const response=await page.goto(process.env.WEBGIS_BASE_URL||'http://127.0.0.1:4173/web-gis.html',{waitUntil:'domcontentloaded',timeout:60000});
  assert.ok(response?.ok(),'WebGIS should load');
  await page.waitForFunction(()=>!!window.__sabzGdbCatalogState&&!!window.SabzGdbCatalogTree&&!!window.__sabzWebGISSharedBridge?.map,null,{timeout:60000});
  await page.evaluate(()=>{document.querySelector('[data-catalog-section="geodatabase"]')?.classList.add('open');document.getElementById('catalogPanel')?.classList.add('open');window.__sabzGdbCatalogState.render()});
  const create=async(workspace,dataset,featureClass)=>{
   await page.locator('#gdbWorkspaceName').fill(workspace);
   await page.locator('#gdbDatasetName').fill(dataset);
   await page.locator('#gdbCrs').selectOption('EPSG:4326');
   await page.locator('#gdbCreateDataset').click();
   await page.locator('#gdbFeatureClassName').fill(featureClass);
   await page.locator('#gdbGeometryType').selectOption('Polygon');
   await page.locator('#gdbCreateFeatureClass').click();
   await page.waitForFunction(({w,d,c})=>{const x=window.__sabzGdbCatalogState.workspaces.get(w)?.datasets.get(d)?.classes.some(k=>k.name===c);return !!x},{w:workspace,d:dataset,c:featureClass},{timeout:15000});
  };
  for(const w of ['CatalogRegression_A','CatalogRegression_B']){
   for(const d of ['Parcels','Roads']){
    for(const c of ['FC_One','FC_Two'])await create(w,d,c);
   }
  }
  await page.evaluate(()=>window.__sabzGdbCatalogState.render());
  const before=await page.evaluate(()=>({roots:[...document.querySelectorAll('#geodatabaseManagerList > div > .sabz-gdb-tree-node')].map(x=>x.querySelector('.sabz-gdb-tree-name')?.textContent),workspaces:window.__sabzGdbCatalogState.toJSON().workspaces.map(w=>({name:w.name,datasets:w.datasets.map(d=>({name:d.name,classes:d.classes.map(c=>c.name)}))})),details:!!document.getElementById('gdbTreeDetails')}));
  assert.deepEqual(before.workspaces.filter(w=>w.name.startsWith('CatalogRegression_')).map(w=>[w.name,w.datasets.length,w.datasets.reduce((n,d)=>n+d.classes.length,0)]),[['CatalogRegression_A',2,4],['CatalogRegression_B',2,4]]);
  const firstRoot=page.locator('#geodatabaseManagerList .sabz-gdb-tree-node[data-node-key="w:CatalogRegression_A"]');
  await firstRoot.locator('.sabz-gdb-tree-toggle').click();
  const firstDataset=page.locator('#geodatabaseManagerList .sabz-gdb-tree-node[data-node-key="w:CatalogRegression_A/d:Parcels"]');
  await firstDataset.locator('.sabz-gdb-tree-toggle').click();
  const classNode=page.locator('#geodatabaseManagerList .sabz-gdb-tree-node[data-node-key="w:CatalogRegression_A/d:Parcels/c:FC_One"]');
  await classNode.click();
  assert.ok((await page.locator('#gdbTreeDetails').innerText()).includes('FC_One'),'Feature class details should be displayed');
  await classNode.dblclick();
  const save=await page.evaluate(async()=>{document.getElementById('projectName').value='ArcCatalog Tree Regression';const ok=await window.__sabzWebGISSharedBridge.saveLocalProject();return {ok,catalog:window.__sabzWebGISSharedBridge.serializeProject().gdbCatalog}});
  assert.ok(save.catalog?.workspaces?.some(w=>w.name==='CatalogRegression_A'),'Project serialization should contain the geodatabase tree');
  await page.reload({waitUntil:'domcontentloaded',timeout:60000});
  await page.waitForFunction(()=>!!window.SabzGdbCatalogTree&&!!window.__sabzWebGISSharedBridge?.map,null,{timeout:60000});
  const restored=await page.evaluate(async()=>{const b=window.__sabzWebGISSharedBridge,projects=await b.listLocalProjects(),p=projects.find(x=>x.name==='ArcCatalog Tree Regression');if(!p)throw new Error('Saved geodatabase regression project was not found after reload');if(!b.restoreProject(p))throw new Error('Saved geodatabase regression project could not be restored');const snap=b.serializeProject();return {workspaces:snap.gdbCatalog?.workspaces||[],treeRoots:[...document.querySelectorAll('#geodatabaseManagerList .sabz-gdb-tree-node[data-node-key^="w:"] .sabz-gdb-tree-name')].map(x=>x.textContent),treeReady:!!document.getElementById('gdbTreeDetails')}});
  assert.ok(restored.workspaces.some(w=>w.name==='CatalogRegression_A'&&w.datasets.length===2),'Saved project should restore geodatabase/dataset structure after reload');
  assert.ok(restored.workspaces.some(w=>w.name==='CatalogRegression_B'&&w.datasets.length===2),'Second geodatabase should survive reload');
  assert.ok(restored.treeReady,'Catalog tree should render after reload');
  assert.deepEqual(pageErrors,[],'No uncaught JavaScript errors');
  assert.deepEqual(consoleErrors,[],'No browser console errors');
  console.log('GDB catalog tree before reload:',JSON.stringify(before.workspaces.filter(w=>w.name.startsWith('CatalogRegression_'))));
  console.log('GDB catalog tree after reload:',JSON.stringify(restored.workspaces.filter(w=>w.name.startsWith('CatalogRegression_'))));
  console.log('Browser console errors:',JSON.stringify(consoleErrors));
  console.log('PASS — two workspaces, four datasets, eight feature classes, selection/details, expand/collapse and project reload.');
 }finally{await browser.close()}
})().catch(error=>{console.error(error);process.exitCode=1});
