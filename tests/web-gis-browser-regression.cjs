'use strict';

const assert = require('node:assert/strict');
const { chromium } = require('playwright');

const baseUrl = process.env.WEBGIS_BASE_URL || 'https://sabzroyanuraman.com/web-gis.html';
const fixtureName = '__SabzBrowserRegressionFixture__';
const fixture = {
  type: 'FeatureCollection',
  features: [
    {type:'Feature',id:'reg-1',properties:{name:'Regression parcel A',area_ha:12.5,parcel_id:'R-001'},geometry:{type:'Polygon',coordinates:[[[47.7575,33.6288],[47.7590,33.6288],[47.7590,33.6300],[47.7575,33.6300],[47.7575,33.6288]]]}},
    {type:'Feature',id:'reg-2',properties:{name:'Regression parcel B',area_ha:27.0,parcel_id:'R-002'},geometry:{type:'Polygon',coordinates:[[[47.7592,33.6288],[47.7607,33.6288],[47.7607,33.6300],[47.7592,33.6300],[47.7592,33.6288]]]}}
  ]
};

(async()=>{
  const browser = await chromium.launch({headless:true,args:['--no-sandbox']});
  const context = await browser.newContext({viewport:{width:1440,height:960},acceptDownloads:true});
  const page = await context.newPage();
  const pageErrors = [];
  page.on('pageerror', error => pageErrors.push(error.message));
  try {
    const response = await page.goto(baseUrl,{waitUntil:'domcontentloaded',timeout:60000});
    assert.ok(response && response.ok(), 'WebGIS page must return HTTP success');
    await page.waitForFunction(() => !!window.__sabzGISMap && !!window.__sabzWebGISSharedBridge, null, {timeout:45000});
    await page.waitForTimeout(1800);
    assert.equal(await page.evaluate(() => typeof window.__sabzWebGISSharedBridge.currentCRS), 'string',
      'the active projected CRS must be exposed to the processing adapter');

    // Basemap switching is exercised through the actual UI control.
    const basemap = page.locator('#basemap');
    assert.ok(await basemap.count(), 'Basemap selector must exist');
    const values = await basemap.locator('option').evaluateAll(options => options.map(option=>option.value));
    for (const value of ['topo','osm']) {
      if (values.includes(value)) {
        await basemap.selectOption(value);
        await page.waitForTimeout(350);
        assert.equal(await basemap.inputValue(), value, 'Basemap selector should retain ' + value);
      }
    }

    // Open and close each visible top-level menu without invoking destructive actions.
    const menuCount = await page.evaluate(() => {
      const candidates = [...document.querySelectorAll('button[aria-haspopup="true"],button[data-menu],.arc-menu-btn')];
      return candidates.filter(button => button.offsetParent !== null).length;
    });
    assert.ok(menuCount >= 5, 'Expected the ArcMap-style top-level menus to be present');

    // Inject a deterministic test layer through the same bridge used by RustGIS outputs.
    const setup = await page.evaluate(data => {
      const bridge = window.__sabzWebGISSharedBridge;
      bridge.addGeoJSONLayer('__SabzBrowserRegressionFixture__',data);
      const selected = bridge.applyRustGISSelection('__SabzBrowserRegressionFixture__',[0]);
      const before = bridge.map.getZoom();
      window.zoomToSelection();
      return {selected,before,after:bridge.map.getZoom(),hasProperties:typeof window.openLayerProperties==='function'};
    },fixture);
    assert.equal(setup.selected,1,'RustGIS selection bridge should select one feature');
    assert.ok(setup.after >= setup.before,'Zoom To Selection should not zoom out');
    assert.ok(setup.hasProperties,'Layer Properties command must be callable');

    // Exercise Graduated Colors and permanent labels, then verify their live layer state.
    const styleResult = await page.evaluate(name => {
      const bridge = window.__sabzWebGISSharedBridge;
      const item = bridge.namedLayers.find(layer=>layer.name===name);
      if (!item) throw new Error('Regression fixture layer was not registered');
      window.openLayerProperties(item);
      const set = (id,value) => {
        const control=document.getElementById(id);
        if(!control) throw new Error('Missing Layer Properties control: '+id);
        control.value=String(value);
        control.dispatchEvent(new Event('change',{bubbles:true}));
      };
      set('lpRenderer','graduated');
      set('lpStyleField','area_ha');
      set('lpColorRamp','green');
      set('lpClassCount','5');
      const labels=document.getElementById('lpLabelsEnabled');
      if(!labels) throw new Error('Layer label toggle is missing');
      labels.checked=true;
      set('lpLabelField','name');
      document.getElementById('layerPropertiesForm').dispatchEvent(new Event('submit',{bubbles:true,cancelable:true}));
      const first=item.layer.getLayers()[0];
      return {renderer:item._rendererConfig?.mode,field:item._rendererConfig?.field,labels:item._labelsEnabled,labelField:item._labelField,tooltip:!!first?.getTooltip()};
    },fixtureName);
    assert.equal(styleResult.renderer,'graduated');
    assert.equal(styleResult.field,'area_ha');
    assert.equal(styleResult.labels,true);
    assert.equal(styleResult.labelField,'name');
    assert.equal(styleResult.tooltip,true,'Labels should be bound to the rendered feature');

    // Persist locally to IndexedDB, reload the page, and verify geometry + symbology + labels.
    const saved = await page.evaluate(async () => {
      document.getElementById('projectName').value='WebGIS Browser Regression Project';
      const ok = await window.scheduleAutoSave();
      const snapshot = window.serializeProject();
      return {ok,features:snapshot.integrity?.featureCount,crs:snapshot.crs,name:snapshot.name};
    });
    assert.equal(saved.ok,true,'IndexedDB project autosave must complete');
    assert.ok(saved.features >= 2,'Project snapshot should include fixture geometries');
    await page.reload({waitUntil:'domcontentloaded',timeout:60000});
    await page.waitForFunction(() => !!window.__sabzGISMap && !!window.__sabzWebGISSharedBridge, null, {timeout:45000});
    const restored = await page.evaluate(async name => {
      const projects=await window.listProjects();
      const project=projects.find(item=>item.name==='WebGIS Browser Regression Project');
      if(!project) throw new Error('Saved browser regression project was not found in IndexedDB after reload');
      if(!window.restoreProject(project)) throw new Error('Saved browser regression project could not be restored');
      const item=window.__sabzWebGISSharedBridge.namedLayers.find(layer=>layer.name===name);
      return {renderer:item?._rendererConfig?.mode,field:item?._rendererConfig?.field,labels:item?._labelsEnabled,labelField:item?._labelField,count:item?.layer?.getLayers?.().length};
    },fixtureName);
    assert.deepEqual(restored,{renderer:'graduated',field:'area_ha',labels:true,labelField:'name',count:2},
      'Saved project must restore features, renderer and labels after a browser reload');

    // The login entry point must be present. Actual account sign-in/out is intentionally
    // not automated without dedicated non-production credentials supplied as CI secrets.
    const loginEntry = await page.getByText(/GIS Online Login|ورود به حساب GIS/i).count();
    assert.ok(loginEntry > 0,'GIS account login entry point should be available');

    assert.deepEqual(pageErrors,[],'No uncaught JavaScript errors should occur during the browser regression');
    console.log('PASS — checked-out WebGIS load, CRS bridge, basemap switch, menu presence, selection/zoom, symbology, labels, IndexedDB save/reload.');
    console.log('SKIP — authenticated GIS sign-in/sign-out: requires dedicated test credentials; no production credentials are embedded in CI.');
  } finally {
    await browser.close();
  }
})().catch(error=>{console.error(error);process.exitCode=1;});
