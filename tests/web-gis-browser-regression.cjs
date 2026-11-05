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
  const context = await browser.newContext({viewport:{width:1440,height:960},acceptDownloads:true,geolocation:{latitude:33.63,longitude:47.75},permissions:['geolocation']});
  const page = await context.newPage();
  const gpsActions = [];
  await page.route('**/api/location/track.php', async route => {
    const payload = route.request().postDataJSON();
    gpsActions.push(payload.action);
    await route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({ok:true})});
  });
  const pageErrors = [];
  const consoleErrors = [];
  page.on('pageerror', error => pageErrors.push(error.message));
  page.on('console', message => {
    if (message.type() !== 'error') return;
    const text = message.text();
    if (/Failed to load resource|404|net::ERR/.test(text)) return;
    consoleErrors.push(text);
  });
  try {
    const response = await page.goto(baseUrl,{waitUntil:'domcontentloaded',timeout:60000});
    assert.ok(response && response.ok(), 'WebGIS page must return HTTP success');
    await page.waitForFunction(() => !!window.__sabzGISMap && !!window.__sabzWebGISSharedBridge, null, {timeout:45000});
    await page.waitForTimeout(1800);
    assert.equal(await page.evaluate(() => typeof window.__sabzWebGISSharedBridge.currentCRS), 'string',
      'the active projected CRS must be exposed to the processing adapter');

    // Basemap switching is exercised through the actual UI control.
    // ArcToolbox owns the Display accordion; open it before using the basemap control.
    await page.locator('#toolboxToggleBtn').click();
    await page.locator('.accordion-section[data-accordion="display"] .accordion-head').click();
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
    const visibleMenus = page.locator('.arc-menu-btn[data-menu]:visible');
    const menuCount = await visibleMenus.count();
    assert.ok(menuCount >= 8, 'Expected the ArcMap-style top-level menus to be present');
    for (let index=0; index<menuCount; index++) {
      const button=visibleMenus.nth(index);
      const menuId=await button.getAttribute('data-menu');
      const panel=page.locator('#'+menuId);
      assert.ok(await panel.count(), 'Menu panel must exist for '+menuId);
      await button.click();
      await page.waitForFunction(id => document.getElementById(id)?.classList.contains('show'),menuId);
      assert.equal(await button.getAttribute('aria-expanded'),'true','Menu should expose expanded state: '+menuId);
      await page.keyboard.press('Escape');
    }

    // Exercise the extracted GPS module with browser geolocation and a mocked tracking API.
    assert.equal(await page.evaluate(() => typeof window.SabzGpsTrack?.start), 'function',
      'GPS tracking module must be loaded and expose its public API');
    await page.locator('#goToXYBtn').click();
    assert.ok(await page.locator('#goToXYDialog').isVisible(), 'Go To XY dialog must open');
    page.once('dialog', dialog => dialog.accept());
    await page.locator('#goToXYTrackStart').click();
    await page.waitForFunction(() => window.SabzGpsTrack?.pointCount > 0, null, {timeout:15000});
    assert.equal(await page.evaluate(() => window.SabzGpsTrack.active), true,
      'GPS tracker should remain active after receiving a browser position');
    await page.locator('#goToXYTrackStop').click();
    await page.waitForFunction(() => window.SabzGpsTrack?.active === false, null, {timeout:5000});
    await page.waitForTimeout(250);
    assert.ok(gpsActions.includes('start') && gpsActions.includes('point') && gpsActions.includes('stop'),
      'GPS module should send start, point and stop actions to the tracking API: '+JSON.stringify(gpsActions));
    await page.locator('#goToXYTrackClear').click();
    await page.locator('#goToXYClose').click();

    // Inject a deterministic test layer through the same bridge used by RustGIS outputs.
    const setup = await page.evaluate(data => {
      const bridge = window.__sabzWebGISSharedBridge;
      bridge.addGeoJSONLayer('__SabzBrowserRegressionFixture__',data);
      const selected = bridge.applyRustGISSelection('__SabzBrowserRegressionFixture__',[0]);
      const before = bridge.map.getZoom();
      bridge.zoomToSelection();
      return {selected,before,after:bridge.map.getZoom(),hasProperties:typeof bridge.openLayerProperties==='function'};
    },fixture);
    assert.equal(setup.selected,1,'RustGIS selection bridge should select one feature');
    assert.ok(setup.after >= setup.before,'Zoom To Selection should not zoom out');
    assert.ok(setup.hasProperties,'Layer Properties command must be callable');

    // Run a real RustGIS Clip through the WebAssembly engine. The input coordinates
    // are WGS84; the adapter must project to EPSG:32638 and return the output to WGS84.
    const clipInputName='__SabzProjectedClipInput__';
    const clipMaskName='__SabzProjectedClipMask__';
    await page.evaluate(({inputName,maskName}) => {
      const bridge=window.__sabzWebGISSharedBridge;
      bridge.addGeoJSONLayer(inputName,{type:'FeatureCollection',features:[
        {type:'Feature',properties:{parcel:'A'},geometry:{type:'Polygon',coordinates:[[[47.0,33.0],[47.02,33.0],[47.02,33.02],[47.0,33.02],[47.0,33.0]]]}}
      ]});
      bridge.addGeoJSONLayer(maskName,{type:'FeatureCollection',features:[
        {type:'Feature',properties:{zone:'Z1'},geometry:{type:'Polygon',coordinates:[[[47.01,33.0],[47.03,33.0],[47.03,33.02],[47.01,33.02],[47.01,33.0]]]}}
      ]});
    },{inputName:clipInputName,maskName:clipMaskName});
    await page.locator('#rgEngineButton').click();
    await page.waitForFunction(() => !!window.SabzRoyanRustGIS?.listTools, null, {timeout:60000});
    const clipTool = await page.evaluate(() => window.SabzRoyanRustGIS.listTools().find(tool=>tool.id==='clip'));
    assert.ok(clipTool,'RustGIS Clip tool must be available in the loaded WASM catalogue');
    await page.locator('#rgEngineTool').selectOption('clip');
    await page.locator('[data-param="input"]').selectOption(clipInputName);
    await page.locator('[data-param="clip"]').selectOption(clipMaskName);
    await page.locator('[data-param="output"]').fill('UTM Clip Regression Output');
    await page.locator('#rgEngineRun').click();
    await page.waitForFunction(() => {
      const status=document.getElementById('rgEngineStatus')?.textContent||'';
      return status.includes('پایان یافت') || status.includes('خطا در اجرای پردازش');
    }, null, {timeout:60000});
    const projectedClip = await page.evaluate(() => {
      const bridge=window.__sabzWebGISSharedBridge;
      const item=bridge.namedLayers.find(layer=>layer.name.includes('UTM Clip Regression Output'));
      const message=document.getElementById('rgEngineMessages')?.textContent||'';
      if(!item)return {found:false,message};
      const geo=item.layer.toGeoJSON();
      const positions=[];
      const walk=value=>{if(Array.isArray(value)){if(value.length>=2&&typeof value[0]==='number'&&typeof value[1]==='number')positions.push(value);else value.forEach(walk)}};
      const walkGeometry=geometry=>{if(geometry?.coordinates)walk(geometry.coordinates);(geometry?.geometries||[]).forEach(walkGeometry)};
      (geo.features||[]).forEach(feature=>walkGeometry(feature.geometry));
      return {found:true,count:geo.features?.length||0,minX:Math.min(...positions.map(p=>p[0])),maxX:Math.max(...positions.map(p=>p[0])),minY:Math.min(...positions.map(p=>p[1])),maxY:Math.max(...positions.map(p=>p[1])),message};
    });
    assert.ok(projectedClip.found && projectedClip.count>0,'RustGIS Clip should add a non-empty result layer: '+JSON.stringify(projectedClip));
    assert.match(projectedClip.message,/EPSG:32638/,'Processing report should identify the projected processing CRS: '+JSON.stringify(projectedClip));
    assert.match(projectedClip.message,/geo boolean operations in projected coordinates/,'Projected Clip should use the geo engine in the active CRS: '+JSON.stringify(projectedClip));
    assert.ok(projectedClip.minX>46 && projectedClip.maxX<49 && projectedClip.minY>32 && projectedClip.maxY<35,
      'Clip output must be reprojected to WGS84 before adding it to Leaflet: '+JSON.stringify(projectedClip));
    await page.locator('#rgEngineDialog .rg-actions button').nth(1).click();

    // Exercise Graduated Colors and permanent labels, then verify their live layer state.
    const styleResult = await page.evaluate(name => {
      const bridge = window.__sabzWebGISSharedBridge;
      const item = bridge.namedLayers.find(layer=>layer.name===name);
      if (!item) throw new Error('Regression fixture layer was not registered');
      bridge.openLayerProperties(item);
      const set = (id,value) => {
        const control=document.getElementById(id);
        if(!control) throw new Error('Missing Layer Properties control: '+id);
        control.value=String(value);
        control.dispatchEvent(new Event('change',{bubbles:true}));
      };
      const apply = (mode,field,method) => {
        bridge.openLayerProperties(item);
        set('lpRenderer',mode);
        if(field)set('lpStyleField',field);
        set('lpColorRamp','green');
        set('lpClassCount','5');
        if(method)set('lpClassificationMethod',method);
        document.getElementById('layerPropertiesForm').dispatchEvent(new Event('submit',{bubbles:true,cancelable:true}));
      };
      apply('unique','parcel_id');
      const uniqueColors=item.layer.getLayers().map(feature=>feature.options?.fillColor).filter(Boolean);
      apply('graduated','area_ha','quantile');
      const labels=document.getElementById('lpLabelsEnabled');
      if(!labels) throw new Error('Layer label toggle is missing');
      labels.checked=true;
      set('lpLabelField','name');
      set('lpLabelSize','16');
      set('lpLabelColor','#123456');
      set('lpLabelPlacement','top');
      set('lpLabelMinZoom','0');
      set('lpLabelDensity','sparse');
      document.getElementById('layerPropertiesForm').dispatchEvent(new Event('submit',{bubbles:true,cancelable:true}));
      const first=item.layer.getLayers()[0];
      const tooltip=first?.getTooltip();
      return {uniqueMode:'unique',uniqueColors,renderer:item._rendererConfig?.mode,field:item._rendererConfig?.field,method:item._rendererConfig?.method,labels:item._labelsEnabled,labelField:item._labelField,tooltip:!!tooltip,tooltipContent:tooltip?.getContent(),tooltipPermanent:tooltip?.options?.permanent,tooltipClassName:tooltip?.options?.className,labelSize:item._labelSize,labelColor:item._labelColor,labelPlacement:item._labelPlacement,labelMinZoom:item._labelMinZoom,labelDensity:item._labelDensity,tooltipDirection:tooltip?.options?.direction,labelCss:document.getElementById(item._labelClass+'-style')?.textContent||''};
    },fixtureName);
    assert.equal(styleResult.uniqueMode,'unique');
    assert.ok(new Set(styleResult.uniqueColors).size>=2,'Unique Values renderer should assign distinct category colors');
    assert.equal(styleResult.renderer,'graduated');
    assert.equal(styleResult.field,'area_ha');
    assert.equal(styleResult.method,'quantile','Graduated Colors should apply the selected Quantile method');
    assert.equal(styleResult.labels,true);
    assert.equal(styleResult.labelField,'name');
    assert.equal(styleResult.tooltip,true,'Labels should be bound to the rendered feature');
    assert.equal(styleResult.tooltipContent,'Regression parcel A','Label renderer should display the selected attribute value');
    assert.equal(styleResult.tooltipPermanent,true,'ArcMap-style labels should remain visible without hover');
    assert.match(styleResult.tooltipClassName,/^gis-feature-label-/,'Layer labels should use their own generated CSS class');
    assert.equal(styleResult.labelSize,16,'Layer Properties should apply the requested label size');
    assert.equal(styleResult.labelColor,'#123456','Layer Properties should apply the requested label color');
    assert.equal(styleResult.labelPlacement,'top','Layer Properties should apply the requested label placement');
    assert.equal(styleResult.labelMinZoom,0,'Layer Properties should persist the minimum label zoom');
    assert.equal(styleResult.labelDensity,'sparse','Layer Properties should apply sparse label density');
    assert.equal(styleResult.tooltipDirection,'top','Labels should use the requested placement');
    assert.match(styleResult.labelCss,/text-shadow:/,'Rendered labels should use a white halo rather than a tooltip box');
    assert.match(styleResult.labelCss,/background:transparent/,'Rendered labels should not have a tooltip background');

    // Dense-layer E2E: exercise real Leaflet tooltip elements at several zoom levels.
    const denseLabelResult = await page.evaluate(async () => {
      const bridge=window.__sabzWebGISSharedBridge;
      const name='__SabzDenseLabelRegression__';
      const features=[];
      for(let row=0;row<15;row++)for(let column=0;column<20;column++){
        const lon=47.759+(column-10)*0.000018;
        const lat=33.629+(row-7)*0.000018;
        features.push({type:'Feature',id:'dense-'+row+'-'+column,properties:{name:'Dense '+row+'-'+column},
          geometry:{type:'Point',coordinates:[lon,lat]}});
      }
      bridge.addGeoJSONLayer(name,{type:'FeatureCollection',features});
      const item=bridge.namedLayers.find(layer=>layer.name===name);
      if(!item)throw new Error('Dense test layer was not registered');
      bridge.map.setView([33.629,47.759],15);
      bridge.openLayerProperties(item);
      const set=(id,value)=>{const el=document.getElementById(id);if(!el)throw new Error('Missing '+id);el.value=String(value);el.dispatchEvent(new Event('change',{bubbles:true}));};
      const toggle=document.getElementById('lpLabelsEnabled');
      toggle.checked=true;
      set('lpLabelField','name');
      set('lpLabelMinZoom','12');
      set('lpLabelDensity','balanced');
      document.getElementById('layerPropertiesForm').dispatchEvent(new Event('submit',{bubbles:true,cancelable:true}));
      const settleMap=async()=>{await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));};
      const count=()=>Array.from(document.querySelectorAll('.'+item._labelClass)).filter(el=>el.getBoundingClientRect().width>0).length;
      const visible=()=>Array.from(document.querySelectorAll('.'+item._labelClass)).filter(el=>el.getBoundingClientRect().width>0&&getComputedStyle(el).visibility!=='hidden').length;
      await settleMap();
      bridge.map.setZoom(11,{animate:false});
      await settleMap();
      window.refreshLabelDecluttering?.();
      const belowMinimum=visible();
      bridge.map.setZoom(15,{animate:false});
      await settleMap();
      window.refreshLabelDecluttering?.();
      const mediumVisible=visible();
      bridge.map.setZoom(18,{animate:false});
      await settleMap();
      window.refreshLabelDecluttering?.();
      const closeVisible=visible();
      return {featureCount:features.length,domLabels:count(),belowMinimum,mediumVisible,closeVisible};
    });
    assert.equal(denseLabelResult.featureCount,300,'Dense regression fixture should contain 300 point features');
    assert.equal(denseLabelResult.belowMinimum,0,'Labels must be hidden below the configured minimum zoom');
    assert.ok(denseLabelResult.mediumVisible>0,'Some labels should be visible at the medium zoom');
    assert.ok(denseLabelResult.closeVisible>=denseLabelResult.mediumVisible,
      'Zooming in should not reduce the number of decluttered labels');

    // Persist locally to IndexedDB, reload the page, and verify geometry + symbology + labels.
    const saved = await page.evaluate(async () => {
      document.getElementById('projectName').value='WebGIS Browser Regression Project';
      const bridge=window.__sabzWebGISSharedBridge;
      const ok = await bridge.saveLocalProject();
      const snapshot = bridge.serializeProject();
      return {ok,features:snapshot.integrity?.featureCount,crs:snapshot.crs,name:snapshot.name};
    });
    assert.equal(saved.ok,true,'IndexedDB project autosave must complete');
    assert.ok(saved.features >= 2,'Project snapshot should include fixture geometries');
    await page.reload({waitUntil:'domcontentloaded',timeout:60000});
    await page.waitForFunction(() => !!window.__sabzGISMap && !!window.__sabzWebGISSharedBridge, null, {timeout:45000});
    const restored = await page.evaluate(async name => {
      const bridge=window.__sabzWebGISSharedBridge;
      const projects=await bridge.listLocalProjects();
      const project=projects.find(item=>item.name==='WebGIS Browser Regression Project');
      if(!project) throw new Error('Saved browser regression project was not found in IndexedDB after reload');
      if(!bridge.restoreProject(project)) throw new Error('Saved browser regression project could not be restored');
      const item=window.__sabzWebGISSharedBridge.namedLayers.find(layer=>layer.name===name);
      const featureLayers=item?.layer?.getLayers?.()||[];
      const tooltip=featureLayers[0]?.getTooltip?.();
      return {renderer:item?._rendererConfig?.mode,field:item?._rendererConfig?.field,method:item?._rendererConfig?.method,labels:item?._labelsEnabled,labelField:item?._labelField,labelSize:item?._labelSize,labelColor:item?._labelColor,labelPlacement:item?._labelPlacement,labelMinZoom:item?._labelMinZoom,labelDensity:item?._labelDensity,count:featureLayers.length,tooltipContent:tooltip?.getContent(),tooltipPermanent:tooltip?.options?.permanent};
    },fixtureName);
    assert.deepEqual(restored,{renderer:'graduated',field:'area_ha',method:'quantile',labels:true,labelField:'name',labelSize:16,labelColor:'#123456',labelPlacement:'top',labelMinZoom:0,labelDensity:'sparse',count:2,tooltipContent:'Regression parcel A',tooltipPermanent:true},
      'Saved project must restore features, renderer and visible label text after a browser reload');

    // The login entry point must be present. Actual account sign-in/out is intentionally
    // not automated without dedicated non-production credentials supplied as CI secrets.
    const onlineDataEntry = page.locator('#addOnlineDataBtn');
    assert.ok(await onlineDataEntry.count() && await onlineDataEntry.isVisible(),
      'Add Online Data entry point for GIS account projects should be available');
    await onlineDataEntry.click();
    assert.ok(await page.locator('option[value="account"]').count(),
      'Online Data dialog should offer the GIS account project source');
    await page.keyboard.press('Escape');

    assert.deepEqual(pageErrors,[],'No uncaught JavaScript errors should occur during the browser regression');
    assert.deepEqual(consoleErrors,[],'No unexpected JavaScript console errors should occur during the browser regression');
    console.log('PASS — checked-out WebGIS load, CRS bridge, basemap switch, menu presence, selection/zoom, symbology, labels, IndexedDB save/reload.');
    console.log('SKIP — authenticated GIS sign-in/sign-out: requires dedicated test credentials; no production credentials are embedded in CI.');
  } finally {
    await browser.close();
  }
})().catch(error=>{console.error(error);process.exitCode=1;});
