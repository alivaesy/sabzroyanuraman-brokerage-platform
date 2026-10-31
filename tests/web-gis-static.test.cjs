'use strict';

const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const test = require('node:test');
const assert = require('node:assert/strict');

const htmlPath = path.join(__dirname, '..', 'sabzroyanuraman-site', 'web-gis.html');
const html = fs.readFileSync(htmlPath, 'utf8');

test('all inline JavaScript blocks parse', () => {
  const scripts = [...html.matchAll(/<script\b([^>]*)>([\s\S]*?)<\/script>/gi)]
    .filter(([, attrs, body]) => !/\bsrc\s*=/i.test(attrs)
      && !/type\s*=\s*["']application\/(?:json|ld\+json)["']/i.test(attrs)
      && body.trim());
  assert.ok(scripts.length > 0, 'expected inline scripts in web-gis.html');
  for (const [index, [, attrs, body]] of scripts.entries()) {
    const label = attrs.match(/\bid\s*=\s*["']([^"']+)["']/i)?.[1] || 'inline script';
    try {
      new vm.Script(body, { filename: label + ' #' + (index + 1) });
    } catch (error) {
      assert.fail(label + ' #' + (index + 1) + ': ' + error.message + '\\n' + error.stack);
    }
  }
});

test('Geoprocessing options have valid unique values and registered handlers', () => {
  const select = html.match(/<select\s+id=["']gisOperation["'][^>]*>([\s\S]*?)<\/select>/i)?.[1];
  assert.ok(select, 'gisOperation select was not found');
  const options = [...select.matchAll(/<option\s+value=["']([^"']+)["']/gi)].map(m => m[1]);
  assert.equal(options.length, 23, 'expected 23 Geoprocessing operations');
  assert.equal(new Set(options).size, options.length, 'operation values must be unique');

  const core = new Set([
    'buffer', 'clip', 'intersection', 'merge', 'union', 'erase', 'identity',
    'append', 'split', 'centroid', 'convexHull', 'boundingBox'
  ]);
  const toolEntries = [...html.matchAll(/\{\s*id:\s*["']([^"']+)["'](?:\s*,\s*op:\s*["']([^"']+)["'])?/g)];
  const registered = new Set(toolEntries.flatMap(([, id, op]) => [id, op].filter(Boolean)));
  const missing = options.filter(value => !core.has(value) && !registered.has(value));
  assert.deepEqual(missing, [], 'unregistered operation(s): ' + missing.join(', '));
});

test('ArcToolbox renderer retains search and empty-state feedback', () => {
  assert.match(html, /function\s+renderGisToolbox\s*\(/);
  assert.match(html, /No matching tools found/);
  assert.match(html, /registered tools/);
  assert.match(html, /gis-tool-group/);
});



test('View menu actions are connected to registered mapActions handlers', () => {
  const actionBlock = html.match(/const mapActions\s*=\s*\{([\s\S]*?)\n\s{4}\};/)?.[1];
  assert.ok(actionBlock, 'mapActions dispatcher was not found');
  const handlers = new Set([...actionBlock.matchAll(/^\s{6}([A-Za-z]\w*)\s*:/gm)].map(m => m[1]));

  for (const menuId of ['mapMenu', 'viewMenu']) {
    const menuStart = html.indexOf('<div class="arc-dropdown" id="' + menuId + '">');
    assert.notEqual(menuStart, -1, menuId + ' was not found');
    const nextMenu = html.indexOf('\n  <div class="arc-dropdown"', menuStart + 1);
    const menu = html.slice(menuStart, nextMenu === -1 ? html.length : nextMenu);
    const actions = [...menu.matchAll(/data-action=["']([^"']+)["']/g)].map(m => m[1]);
    assert.ok(actions.length, menuId + ' contains no actions');
    const missing = actions.filter(action => !handlers.has(action));
    assert.deepEqual(missing, [], menuId + ' actions without handlers: ' + missing.join(', '));
  }

  const tocHandler = actionBlock.match(/^\s{6}showLayers\s*:[\s\S]*?(?=^\s{6}[A-Za-z]\w*\s*:|$)/m)?.[0] || '';
  assert.match(tocHandler, /renderLayerList/);
  assert.match(tocHandler, /focus/);
});


test('Append maps source records to the target schema without changing Merge', () => {
  const start = html.indexOf('function appendFeaturesToTarget(');
  const end = html.indexOf('async function runWholeLayerGisOperation(', start);
  assert.ok(start >= 0 && end > start, 'Append schema helper was not found');
  const helper = html.slice(start, end);
  const appendFeaturesToTarget = vm.runInNewContext(helper + '\nappendFeaturesToTarget');
  const target = [
    { type: 'Feature', properties: { ParcelID: 1, Owner: 'A' }, geometry: { type: 'Point', coordinates: [1, 2] } }
  ];
  const source = [
    { type: 'Feature', properties: { parcelid: 2, Extra: 'discard', OWNER: 'B' }, geometry: { type: 'Point', coordinates: [3, 4] } }
  ];
  const output = appendFeaturesToTarget(target, source);
  assert.equal(output.length, 2);
  assert.deepEqual(JSON.parse(JSON.stringify(output[0].properties)), { ParcelID: 1, Owner: 'A' });
  assert.deepEqual(JSON.parse(JSON.stringify(output[1].properties)), { ParcelID: 2, Owner: 'B' });
  assert.equal(output[1].geometry.coordinates[0], 3);
  assert.match(html, /if\(op==='merge'\)\{/);
  assert.match(html, /output=appendFeaturesToTarget\(records,ovs\)/);
});


test('Merge preserves separate geometries instead of dissolving polygon features', () => {
  const start = html.indexOf('function mergeFeaturesPreservingGeometry(');
  const end = html.indexOf('async function runGisOperation(', start);
  assert.ok(start >= 0 && end > start, 'Merge helper was not found');
  const mergeFeaturesPreservingGeometry = vm.runInNewContext(html.slice(start, end) + '\nmergeFeaturesPreservingGeometry');
  const a = { type: 'Feature', properties: { ID: 1 }, geometry: { type: 'Polygon', coordinates: [[[0,0],[1,0],[1,1],[0,1],[0,0]]] } };
  const b = { type: 'Feature', properties: { ID: 2 }, geometry: { type: 'Polygon', coordinates: [[[1,0],[2,0],[2,1],[1,1],[1,0]]] } };
  const output = mergeFeaturesPreservingGeometry(a, b);
  assert.equal(output.length, 2);
  assert.deepEqual(JSON.parse(JSON.stringify(output[0].geometry)), a.geometry);
  assert.deepEqual(JSON.parse(JSON.stringify(output[1].geometry)), b.geometry);
  assert.equal(output[0].properties.ID, 1);
  assert.equal(output[1].properties.ID, 2);
  assert.equal(output[0].properties._merge_source, 'input');
  assert.equal(output[1].properties._merge_source, 'overlay');
  assert.match(html, /result=\{type:"FeatureCollection",features:mergeFeaturesPreservingGeometry\(f1,f2\)\}/);
});


test('Whole-layer Clip supports point and line inputs with polygon overlays', () => {
  assert.match(html, /function clipFeatureToPolygon\(feature,mask\)/);
  assert.match(html, /op==='clip'&&ovs\.some\(f=>!\['Polygon','MultiPolygon'\]/);
  assert.match(html, /clipFeatureToPolygon\(a,mask\)/);
  assert.match(html, /g\.type==="MultiPoint"/);
  assert.match(html, /g\.type!=="LineString"&&g\.type!=="MultiLineString"/);
});

test('ArcMap toolbar uses SVG editing icons and an X/Y coordinate glyph', () => {
  assert.match(html, /#editorToolbar #startEditingBtn \.arcmap-icon\{stroke:#4f8056\}/);
  assert.match(html, /#editorToolbar #saveEditsBtn \.arcmap-icon\{stroke:#286d9b\}/);
  assert.match(html, /#editorToolbar #stopEditingBtn \.arcmap-icon\{stroke:#b15a48\}/);
  assert.match(html, /<symbol id="arc-icon-goxy"[^>]*>[\s\S]*?M2 13\.5V3\.2/);
  assert.match(html, /title="Go To XY \(X\/Y coordinates\)" aria-label="Go To XY \(X\/Y coordinates\)"/);
});

test('ArcMap keyboard shortcuts invoke existing Attribute Table, Editor, and Measure actions', () => {
  assert.match(html, /__arcMapShortcutHandlerInstalled/);
  assert.match(html, /F6:document\.getElementById\("openTable"\)/);
  assert.match(html, /F2:document\.getElementById\("startEditingBtn"\)/);
  assert.match(html, /F7:document\.getElementById\("measureToolBtn"\)/);
  assert.match(html, /if\(typing\|\|event\.altKey\|\|event\.ctrlKey\|\|event\.metaKey\)return/);
});


test("Geoprocessing rejects unsplit lines and validates result geometry", () => {
  assert.match(html, /Split produced fewer than two line parts/);
  assert.match(html, /no valid output features were produced/);
  assert.match(html, /output contains missing or unsupported geometry/);
  assert.match(html, /produced structurally invalid geometry/);
});

test('Bulk attribute editing converts blank typed boolean, object, and array values to null', () => {
  assert.match(html, /raw\.trim\(\)===""&&\["boolean","object","array"\]\.includes\(valueType\)/);
});


test('GIS account workspace uses explicit organization-scoped login, project listing, save, and restore', () => {
  assert.match(html, /async function remoteGisLogin\(\)/);
  assert.match(html, /auth\.php\?action=csrf/);
  assert.match(html, /auth\.php\?action=login/);
  assert.match(html, /async function remoteGisChooseProject\(\)/);
  assert.match(html, /projects\.php\?organization_id="\+encodeURIComponent\(remoteGisOrganization\.id\)/);
  assert.match(html, /async function remoteGisSaveProject\(project\)/);
  assert.match(html, /organization_id:Number\(remoteGisOrganization\.id\)/);
  assert.match(html, /async function openRemoteGisProjects\(\)/);
  assert.match(html, /if\(remoteGisUser&&remoteGisOrganization\)/);
  assert.doesNotMatch(html, /async function gisEnsureAccount\(\)/);
});

test('GIS project API scopes reads and feature retrieval to the selected organization', () => {
  const apiPath = path.join(__dirname, '..', 'sabzroyanuraman-site', 'api', 'gis', 'projects.php');
  const api = fs.readFileSync(apiPath, 'utf8');
  assert.match(api, /gis_auth_require_org\(\$pdo,\$user,\(int\)\$org,'viewer'\)/);
  assert.match(api, /WHERE id=:id AND organization_id=:o/);
  assert.match(api, /WHERE project_id=:p AND organization_id=:o/);
  assert.match(api, /gis_auth_require_org_subscription\(\$pdo,\$user,\(int\)\$org\)/);
  assert.match(api, /ORDER BY lower\(name\) ASC, updated_at DESC LIMIT 200/);
});


test('ArcMap import entry point accepts MXD/MXT/MPK and extracts supported MPK data', () => {
  assert.match(html, /data-action=["']importArcMap["']/);
  assert.match(html, /id=["']mxdInput["'][^>]*accept=["']\.mxd,\.mpk,\.mxt,\.zip["']/);
  assert.match(html, /async function importArcMapDocument\(file\)/);
  assert.match(html, /api\/gis\/import-mxd\.php/);
  assert.match(html, /JSZip\.loadAsync\(await file\.arrayBuffer\(\)\)/);
  assert.match(html, /parseKML\(await entry\.async\("text"\)\)/);
  assert.match(html, /MXD styling, fonts, layer-source links and layout are not reproduced/);
});

test('FileGDB import reports missing classes, preserves schema metadata, and flags unsupported rasters', () => {
  assert.match(html, /async function importGDBFiles\(fileList\)/);
  assert.match(html, /const inside=path\.startsWith\(prefix\)\?path:\(prefix\+path\)/);
  assert.match(html, /zip\.file\(inside,f/);
  assert.match(html, /const detailedLayer=/);
  assert.match(html, /fieldDefinitions:/);
  assert.match(html, /notExtracted:/);
  assert.match(html, /-preserve_fid/);
  assert.match(html, /feature-count mismatch/);
  assert.match(html, /raster dataset detected; raster conversion is not yet implemented/);
  assert.match(html, /import-report\.json/);
});


test('ModelBuilder and Layout enhancement is loaded through the renderer bridge', () => {
  const enhancementPath = path.join(__dirname, '..', 'sabzroyanuraman-site', 'webgis-rustgis-enhancements.js');
  const enhancement = fs.readFileSync(enhancementPath, 'utf8');
  assert.match(html, /__sabzInstallModelBuilderRenderer\s*=\s*factory/);
  assert.match(html, /<script\s+src=["']webgis-rustgis-enhancements\.js["']><\/script>/);
  assert.match(enhancement, /sabzMbNode/);
  assert.match(enhancement, /sabzSelectionInstalled/);
  assert.match(enhancement, /MutationObserver/);
});

test('RustGIS GeoTIFF preview refuses to assume EPSG:4326 when CRS metadata is absent', () => {
  const runtimePath = path.join(__dirname, '..', 'sabzroyanuraman-site', 'webgis-rustgis-vector-runtime.js');
  const runtime = fs.readFileSync(runtimePath, 'utf8');
  assert.match(runtime, /const epsgValue = geoKeys\.ProjectedCSTypeGeoKey \|\| geoKeys\.GeographicTypeGeoKey/);
  assert.match(runtime, /Output GeoTIFF has no valid EPSG CRS metadata/);
  assert.doesNotMatch(runtime, /GeographicTypeGeoKey \|\| 4326/);
});

test('Plain TIFF import skips absent GeoKeys and remains preview-only without a CRS', () => {
  const runtimePath = path.join(__dirname, '..', 'sabzroyanuraman-site', 'raster-runtime.js');
  const runtime = fs.readFileSync(runtimePath, 'utf8');
  assert.match(runtime, /const geoKeyDirectory = readTag\(directory, "GeoKeyDirectory"\)/);
  assert.match(runtime, /if \(geoKeyDirectory && typeof image\.getGeoKeys === "function"\)/);
  assert.match(runtime, /const georeferenced = !!transform && !!extent && !!crs\.epsg/);
  assert.match(runtime, /previewOnly: !georeferenced/);
  assert.match(html, /raster-runtime\.js\?v=20261031-plain-tiff/);
  const rasterTests = fs.readFileSync(path.join(__dirname, '..', 'sabzroyanuraman-site', 'raster-runtime-tests.html'), 'utf8');
  assert.match(rasterTests, /real baseline TIFF loads without CRS or GeoKeys/);
});

test('RustGIS browser runtime exposes both vector and raster tool catalogues', () => {
  const runtimePath = path.join(__dirname, '..', 'sabzroyanuraman-site', 'webgis-rustgis-vector-runtime.js');
  const runtime = fs.readFileSync(runtimePath, 'utf8');
  assert.match(runtime, /Array\.isArray\(raw\.tools\)[\s\S]*Array\.isArray\(raw\.rasterTools\)/);
  assert.match(runtime, /runRasterTool\(tool\.id/);
});

test('RustGIS parameter UI filters geometry-specific layers and offers schema-aware fields', () => {
  const runtimePath = path.join(__dirname, '..', 'sabzroyanuraman-site', 'webgis-rustgis-vector-runtime.js');
  const runtime = fs.readFileSync(runtimePath, 'utf8');
  assert.match(runtime, /kind === "polygon-layer"/);
  assert.match(runtime, /types\.has\("Polygon"\)/);
  assert.match(runtime, /kind === "point-layer"/);
  assert.match(runtime, /types\.has\("Point"\)/);
  assert.match(runtime, /\["field","numeric-field","coordinate-field"\]/);
  assert.match(runtime, /Number\.isFinite\(Number\(value\)\)/);
});

test('RustGIS selection replaces stale selections and synchronizes the active layer and table', () => {
  assert.match(html, /applyRustGISSelection:\(layerName,rows\)=>\{[\s\S]*?clearMultiSelection\(\);selectedOverlay=owner\.layer/);
  assert.match(html, /selectedLayer=first\?\.layer\|\|null;synchronizeFeatureSelection/);
  assert.match(html, /syncVisibleAttributeTableSelection\(\)/);
});

test('ModelBuilder executes a topological dependency graph with bounded cumulative outputs', () => {
  assert.match(html, /function modelBuilderTopologicalSteps\(modal\)/);
  assert.match(html, /circular dependency/);
  assert.match(html, /function runModelBuilder\(modal\)/);
  assert.match(html, /nodeOutputs\.get\(inputRef\)/);
  assert.match(html, /nodeOutputs\.get\(overlayNodeRef\)/);
  assert.match(html, /MAX_NODE_FEATURES=20000,MAX_MODEL_FEATURES=100000/);
  assert.match(html, /processedFeatureCount\+=current\.length/);
  assert.match(html, /MAX_MODEL_FEATURES\)/);
  assert.match(html, /nodeOutputs\.get\(validation\.outputNodeId\)/);
});

test('ModelBuilder supports cooperative cancellation between processing nodes', () => {
  assert.match(html, /async function runModelBuilder\(modal\)/);
  assert.match(html, /data-mb-cancel/);
  assert.match(html, /modal\._mbRunState\.cancelled=true/);
  assert.match(html, /lockedControls=\[\.\.\.modal\.querySelectorAll/);
  assert.match(html, /lockedControls\.forEach\(item=>\{item\.control\.disabled=true;\}\)/);
  assert.match(html, /lockedControls\.forEach\(item=>\{item\.control\.disabled=item\.disabled;\}\)/);
  assert.match(html, /await yieldToBrowser\(\)/);
  assert.match(html, /checkCancelled\(\)/);
  assert.match(html, /Model cancelled\. No output layers were added\./);
  assert.match(html, /finally\{lockedControls\.forEach\(item=>\{item\.control\.disabled=item\.disabled;\}\);runButton\.disabled=false;cancelButton\.disabled=true;modal\._mbRunState=null;\}/);
});



test('Raster asset replacement quota accounts for the previous content-addressed asset', () => {
  const apiPath = path.join(__dirname, '..', 'sabzroyanuraman-site', 'api', 'gis', 'raster-assets.php');
  const api = fs.readFileSync(apiPath, 'utf8');
  assert.match(api, /\$oldStored=is_array\(\$oldMeta\).*basename\(\$file\)/);
  assert.match(api, /\$oldPath=\$dir.*\$oldStored/);
  assert.match(api, /\$previousBytes=is_file\(\$oldPath\)&&!is_link\(\$oldPath\)\?\(int\)filesize\(\$oldPath\):0/);
  assert.match(api, /\$storedBytes-\$previousBytes\+\$size>1073741824/);
  assert.match(api, /is_file\(\$existingFile\)&&!is_link\(\$existingFile\)/);
});

test('Raster quota, replacement, download, and delete operations share project locking', () => {
  const apiPath = path.join(__dirname, '..', 'sabzroyanuraman-site', 'api', 'gis', 'raster-assets.php');
  const api = fs.readFileSync(apiPath, 'utf8');
  assert.match(api, /fopen\(\$dir\.'\/\.quota\.lock','c'\)/);
  assert.match(api, /flock\(\$quotaLock,LOCK_EX\)/);
  assert.match(api, /flock\(\$quotaLock,LOCK_SH\)/);
  assert.match(api, /fpassthru\(\$handle\)/);
  const deletion = api.slice(api.indexOf("if($action==='delete')"));
  assert.match(deletion, /flock\(\$quotaLock,LOCK_EX\)/);
  assert.match(deletion, /unlink\(\$metaFile\)/);
});

test('Raster API validates TIFF IFD, GeoTIFF georeferencing, image blocks, and EPSG metadata', () => {
  const apiPath = path.join(__dirname, '..', 'sabzroyanuraman-site', 'api', 'gis', 'raster-assets.php');
  const validatorPath = path.join(__dirname, '..', 'sabzroyanuraman-site', 'api', 'gis', 'raster-geotiff-validator.php');
  const api = fs.readFileSync(apiPath, 'utf8');
  const validator = fs.readFileSync(validatorPath, 'utf8');
  assert.match(validator, /function raster_validate_geotiff\(/);
  assert.match(validator, /invalid_geotiff_ifd/);
  assert.match(validator, /geotiff_image_block_out_of_bounds/);
  assert.match(validator, /\$hasTransform/);
  assert.match(validator, /\$hasTieScale/);
  assert.ok(validator.includes("$tiffEpsg === (int)$metadata['crs']['epsg']"));
  assert.match(api, /raster_validate_geotiff\(\$tmp,\$metadata\)/);
  assert.match(validator, /unsupported_or_invalid_tiff_variant/);
});

test('Raster replacement commits through an atomic metadata pointer to an immutable TIFF', () => {
  const apiPath = path.join(__dirname, '..', 'sabzroyanuraman-site', 'api', 'gis', 'raster-assets.php');
  const commitPath = path.join(__dirname, '..', 'sabzroyanuraman-site', 'api', 'gis', 'raster-asset-commit.php');
  const api = fs.readFileSync(apiPath, 'utf8');
  const commit = fs.readFileSync(commitPath, 'utf8');
  assert.match(commit, /\$storedName = \$assetId \. '\.' \. \$hash \. '\.tif'/);
  assert.match(commit, /single commit point/);
  assert.match(commit, /if \(!\$rename\(\$metadataTemp, \$metadataFile\)\)/);
  assert.match(api, /raster_commit_content_addressed\(/);
  assert.match(api, /\$meta\['storage_file'\]/);
  assert.match(api, /invalid_raster_storage_reference/);
});


test('RustGIS plugin manager is connected to the pinned official wasmi plugin runtime', () => {
  const cargoPath = path.join(__dirname, '..', 'sabzroyanuraman-site', 'rustgis-vector-wasm', 'Cargo.toml');
  const rustPath = path.join(__dirname, '..', 'sabzroyanuraman-site', 'rustgis-vector-wasm', 'src', 'lib.rs');
  const runtimePath = path.join(__dirname, '..', 'sabzroyanuraman-site', 'webgis-rustgis-vector-runtime.js');
  const cargo = fs.readFileSync(cargoPath, 'utf8');
  const rust = fs.readFileSync(rustPath, 'utf8');
  const runtime = fs.readFileSync(runtimePath, 'utf8');
  assert.ok(cargo.includes('rustgis-plugins = { git = "https://github.com/opengeos/RustGIS.git"'));
  assert.ok(cargo.includes('rev = "38f24336140d59092a31f964e9ac86aab18fdecd"'));
  for (const symbol of ['inspect_plugin_package', 'install_plugin_package', 'list_plugins', 'set_plugin_enabled', 'uninstall_plugin', 'run_plugin_command']) {
    assert.match(rust, new RegExp('pub fn ' + symbol + '\\b'));
    assert.match(runtime, new RegExp('engine\\.' + symbol + '\\('));
  }
  assert.match(rust, /rustgis_plugins::Package::from_zip/);
  assert.match(rust, /rustgis_plugins::prepare\(package, "web-upload"\)/);
  assert.match(rust, /rustgis_plugins::NoHost/);
  assert.match(rust, /run_command\(&command, &mut host, &params\)/);
  assert.match(rust, /rustgis_plugins::tools::register/);
  assert.match(rust, /rustgis_analysis::run\(tool_id, &inputs, &params\)/);
  assert.match(runtime, /window\.SabzRustRasterBridge = Object\.freeze/);
  assert.match(runtime, /Publish the bridge before Plugin Manager attaches/);
  assert.match(runtime, /const latest = JSON\.parse\(engine\.list_tools\(\)\)/);
  assert.ok(html.indexOf('src="./webgis-rustgis-vector-runtime.js"') < html.indexOf('src="./plugin-manager.js"'),
    'RustGIS plugin bridge must load before Plugin Manager');
  assert.match(runtime, /inspectPluginPackage: async bytes/);
  assert.match(runtime, /runPluginCommand: async/);
});

test('Plugin restore verifies persisted bytes and denies host permissions before installation', () => {
  const pluginPath = path.join(__dirname, '..', 'sabzroyanuraman-site', 'plugin-manager.js');
  const plugin = fs.readFileSync(pluginPath, 'utf8');
  const start = plugin.indexOf('async function restoreApproved()');
  const end = plugin.indexOf('section.querySelector("[data-r-plugin-install]")', start);
  assert.ok(start >= 0 && end > start, 'plugin restore routine was not found');
  const restore = plugin.slice(start, end);
  assert.ok(restore.indexOf('inspectPluginPackage(bytes)') < restore.indexOf('installPluginPackage(bytes,[])'),
    'persisted package must be inspected before it is installed');
  assert.match(restore, /inspected\.sha256!==record\.sha256/);
  assert.match(restore, /inspected\.id!==record\.id/);
  assert.match(restore, /requested\.length\|\|!Array\.isArray\(record\.approvedPermissions\)/);
  assert.match(plugin, /const BLOCKED_PERMISSIONS=new Set\(\["edit","files","network"\]\)/);
  assert.match(plugin, /Plugin manager unavailable: RustGIS WASM plugin runtime failed to load/);
  assert.match(plugin, /section\.querySelectorAll\("button,input,select,textarea"\)\.forEach\(control=>\{control\.disabled=true;\}\)/);
});
