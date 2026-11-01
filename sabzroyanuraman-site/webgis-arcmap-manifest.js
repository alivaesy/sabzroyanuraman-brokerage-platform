/* ArcMap document manifest import and Repair Data Source. */
(function () {
  "use strict";
  const $ = id => document.getElementById(id);
  const bridge = () => window.__sabzWebGISSharedBridge;
  function notify(message) {
    const b=bridge(); if(b?.status)b.status(message);
    if($("arcMapImportStatus"))$("arcMapImportStatus").textContent=message;
  }
  function flattenLayers(manifest) {
    const out=[];
    function visit(items,parents,frame){(Array.isArray(items)?items:[]).forEach(item=>{
      if(!item||typeof item!=="object")return;
      if(item.isGroupLayer||Array.isArray(item.layers))visit(item.layers||[],parents.concat(item.name||"Group"),frame);
      else out.push({item,groupPath:item.groupLayer||parents.join(" / ")||null,frame});
    });}
    (manifest.dataFrames||[]).forEach(frame=>visit(frame.layers,[],frame));
    if(!manifest.dataFrames?.length)visit(manifest.layers,[]);
    return out;
  }
  function importManifest(manifest) {
    if(!manifest||!((manifest.schemaVersion===1&&Array.isArray(manifest.dataFrames))||
      (manifest.format==="sabzroyan-arcmap-export"&&Array.isArray(manifest.layers))))
      throw new Error("Unsupported ArcMap manifest schema.");
    const b=bridge(),entries=flattenLayers(manifest);
    if(!entries.length)throw new Error("Manifest contains no feature layers.");
    entries.forEach(({item,groupPath,frame})=>{
      const name=String(item.name||"Unnamed layer");
      b.addGeoJSONLayer(name,{type:"FeatureCollection",features:[]});
      const owner=b.namedLayers[b.namedLayers.length-1];
      owner.sourceName=item.dataSourcePath||"";
      owner.sourceMissing=true;
      owner.sourceMissingReason="Source path from MXD is not available in this browser.";
      owner.drawingSet=groupPath;
      owner.displayName=name;
      owner._arcMapManifest={
        dataSourcePath:item.dataSourcePath||null,dataSourceType:item.dataSourceType||"other",
        geometryType:item.geometryType||null,visible:item.visible!==false,
        minScale:item.minScale??null,maxScale:item.maxScale??null,
        definitionQuery:item.definitionQuery||null,renderer:item.renderer||null,
        labeling:item.labeling||null,
        extractionWarnings:Array.isArray(item.extractionWarnings)?item.extractionWarnings.slice():[],
        frameName:frame?.name||null,
        frameSpatialReference:typeof frame?.spatialReference==="number"?"EPSG:"+frame.spatialReference:
          (frame?.spatialReference?.latestWkid?"EPSG:"+frame.spatialReference.latestWkid:
          (frame?.spatialReference?.wkid?"EPSG:"+frame.spatialReference.wkid:frame?.spatialReference||null))
      };
      owner._rendererConfig=owner._arcMapManifest.renderer;
      if(item.visible===false)b.map.removeLayer(owner.layer);
    });
    renderRepairChoices();
    notify("Manifest imported: "+entries.length+" layer(s). Repair unresolved data sources to load geometry.");
    return entries.length;
  }
  function renderRepairChoices() {
    const select=$("arcMapRepairLayer");if(!select)return;
    const b=bridge(),old=select.value;
    select.innerHTML='<option value="">Select unresolved layer…</option>';
    b.namedLayers.filter(x=>x?.sourceMissing).forEach(x=>{
      const option=document.createElement("option");option.value=x.name;
      option.textContent=x.name+(x.sourceName?" — "+x.sourceName:"");select.appendChild(option);
    });
    if([...select.options].some(x=>x.value===old))select.value=old;
  }
  function parseKml(source) {
    const doc=new DOMParser().parseFromString(source,"application/xml");
    if(doc.querySelector("parsererror"))throw new Error("Invalid KML.");
    const all=(node,tag)=>Array.from(node.getElementsByTagNameNS("*",tag));
    const parseCoords=value=>String(value||"").trim().split(/\s+/).map(p=>p.split(",").slice(0,2).map(Number)).filter(p=>p.length===2&&p.every(Number.isFinite));
    const features=[];
    all(doc,"Placemark").forEach(pm=>{
      const props={name:all(pm,"name")[0]?.textContent?.trim()||""};
      all(pm,"Data").forEach(x=>{const key=x.getAttribute("name");if(key)props[key]=all(x,"value")[0]?.textContent?.trim()||"";});
      all(pm,"SimpleData").forEach(x=>{const key=x.getAttribute("name");if(key)props[key]=x.textContent.trim();});
      all(pm,"Point").forEach(x=>{const c=parseCoords(all(x,"coordinates")[0]?.textContent)[0];if(c)features.push({type:"Feature",properties:{...props},geometry:{type:"Point",coordinates:c}});});
      all(pm,"LineString").forEach(x=>{const c=parseCoords(all(x,"coordinates")[0]?.textContent);if(c.length>1)features.push({type:"Feature",properties:{...props},geometry:{type:"LineString",coordinates:c}});});
      all(pm,"Polygon").forEach(x=>{const rings=[];all(x,"outerBoundaryIs").concat(all(x,"innerBoundaryIs")).forEach(r=>{const c=parseCoords(all(r,"coordinates")[0]?.textContent);if(c.length>=4)rings.push(c);});if(rings.length)features.push({type:"Feature",properties:{...props},geometry:{type:"Polygon",coordinates:rings}});});
    });
    return {type:"FeatureCollection",features};
  }
  async function readSource(file) {
    const n=file.name.toLowerCase();
    if(n.endsWith(".geojson")||n.endsWith(".json"))return JSON.parse(await file.text());
    if(n.endsWith(".kml"))return parseKml(await file.text());
    if(n.endsWith(".kmz")){
      if(!window.JSZip)throw new Error("JSZip is unavailable.");
      const zip=await JSZip.loadAsync(await file.arrayBuffer());
      const entry=Object.values(zip.files).find(x=>!x.dir&&/(^|\/)doc\.kml$/i.test(x.name));
      if(!entry)throw new Error("KMZ has no doc.kml.");
      return parseKml(await entry.async("text"));
    }
    if(n.endsWith(".zip")){
      if(typeof window.shp!=="function")throw new Error("Shapefile reader is unavailable.");
      const result=await window.shp(await file.arrayBuffer()),sets=Array.isArray(result)?result:[result];
      const valid=sets.filter(x=>x?.type==="FeatureCollection");
      if(!valid.length)throw new Error("ZIP has no readable Shapefile.");
      return valid.length===1?valid[0]:{type:"FeatureCollection",features:valid.flatMap(x=>x.features||[])};
    }
    throw new Error("Supported formats: GeoJSON, KML, KMZ, Shapefile ZIP.");
  }
  async function repairSelectedLayer() {
    const b=bridge(),name=$("arcMapRepairLayer")?.value,file=$("arcMapRepairFile")?.files?.[0];
    if(!name||!file)throw new Error("Select a missing layer and a replacement file.");
    const owner=b.namedLayers.find(x=>x.name===name&&x.sourceMissing);
    if(!owner)throw new Error("Layer is no longer unresolved.");
    if(file.name.toLowerCase().endsWith(".zip")&&window.JSZip){
      const archive=await window.JSZip.loadAsync(await file.arrayBuffer());
      const containsGdb=Object.values(archive.files).some(entry=>!entry.dir&&/\.gdb\//i.test(entry.name));
      if(containsGdb){
        if(!window.SabzGdbCatalog?.importGdbZip)throw new Error("FileGDB repair module is unavailable.");
        window.__sabzGdbRepairTargetName=name;
        try{await window.SabzGdbCatalog.importGdbZip(file);}
        finally{delete window.__sabzGdbRepairTargetName;}
        if(owner.sourceMissing)throw new Error("No compatible feature class was selected from the FileGDB.");
        renderRepairChoices();
        notify("Repaired "+owner.name+" from FileGDB: "+(owner._repairReport?.featureCount||0)+" feature(s).");
        return owner._repairReport;
      }
    }
    let data=await readSource(file);
    if(data?.type==="Feature")data={type:"FeatureCollection",features:[data]};
    if(data?.type!=="FeatureCollection"||!Array.isArray(data.features))throw new Error("File is not a GeoJSON FeatureCollection.");
    data.features=data.features.filter(f=>f?.type==="Feature"&&f.geometry?.type);
    if(!data.features.length)throw new Error("No spatial features found.");
    const expected=String(owner._arcMapManifest?.geometryType||"").toLowerCase();
    const types=[...new Set(data.features.map(f=>f.geometry.type))];
    const compatible=!expected||types.some(t=>expected.includes(t.toLowerCase())||
      (expected.includes("polyline")&&t.toLowerCase().includes("line"))||
      (expected.includes("polygon")&&t.toLowerCase().includes("polygon")));
    if(!compatible)throw new Error("Geometry mismatch: expected "+expected+", got "+types.join(", "));
    const field=owner._arcMapManifest?.renderer?.field||owner._arcMapManifest?.labeling?.field;
    const fieldWarning=field&&!Object.prototype.hasOwnProperty.call(data.features[0].properties||{},field)
      ? " Field '"+field+"' is absent; renderer/labels need remapping.":"";
    owner.layer.clearLayers();owner.layer.addData(data);
    owner.layer.eachLayer(child=>{if(typeof window.attachFeatureSelection==="function")window.attachFeatureSelection(child);});
    owner.sourceMissing=false;owner.sourceMissingReason=null;owner.sourceName=file.name;
    owner._repairReport={file:file.name,featureCount:data.features.length,geometryTypes:types,fieldWarning};
    b.map.addLayer(owner.layer);
    if(owner.layer.getBounds?.().isValid())b.map.fitBounds(owner.layer.getBounds(),{padding:[24,24],maxZoom:18});
    if(b.saveLocalProject)await b.saveLocalProject();
    renderRepairChoices();
    notify("Repaired "+owner.name+": "+data.features.length+" feature(s)."+fieldWarning);
    return owner._repairReport;
  }
  function installUi() {
    if($("arcMapImportPanel"))return;
    const panel=document.createElement("section");panel.id="arcMapImportPanel";panel.className="section";
    panel.innerHTML='<h3>ArcMap Document / Data Sources</h3>'+
      '<div class="row"><button type="button" class="action secondary" id="arcMapManifestButton">Import ArcMap Manifest (.json)</button></div>'+
      '<input id="arcMapManifestInput" type="file" accept=".json" hidden>'+
      '<label class="compact-label" for="arcMapRepairLayer">Repair Data Source</label>'+
      '<select id="arcMapRepairLayer"><option value="">Select unresolved layer…</option></select>'+
      '<input id="arcMapRepairFile" type="file" accept=".geojson,.json,.kml,.kmz,.zip">'+
      '<div class="row"><button type="button" class="action" id="arcMapRepairButton">Repair Selected Layer</button><button type="button" class="action secondary" id="arcMapRepairGdbFolderButton">Repair from .gdb Folder</button></div>'+
      '<input id="arcMapRepairGdbFolder" type="file" webkitdirectory directory multiple hidden>'+
      '<div id="arcMapImportStatus" class="small" role="status">Import a converted MXD manifest, then repair missing sources.</div>';
    const target=$("layers")?.parentElement||$("panel");if(!target)return;
    target.insertBefore(panel,target.firstChild);
    $("arcMapManifestButton").onclick=()=>$("arcMapManifestInput").click();
    $("arcMapManifestInput").onchange=async e=>{const f=e.target.files?.[0];if(!f)return;try{importManifest(JSON.parse(await f.text()));}catch(err){notify("Manifest import failed: "+(err.message||err));}finally{e.target.value="";}};
    $("arcMapRepairButton").onclick=async()=>{try{await repairSelectedLayer();}catch(err){notify("Repair failed: "+(err.message||err));}};
    $("arcMapRepairGdbFolderButton").onclick=()=>$("arcMapRepairGdbFolder").click();
    $("arcMapRepairGdbFolder").onchange=async event=>{
      const files=Array.from(event.target.files||[]),name=$("arcMapRepairLayer")?.value;
      if(!files.length)return;
      const owner=bridge().namedLayers.find(x=>x.name===name&&x.sourceMissing);
      if(!owner){notify("Select an unresolved layer before choosing a GDB folder.");event.target.value="";return;}
      window.__sabzGdbRepairTargetName=name;
      try{
        if(typeof bridge().importGDBFiles!=="function")throw new Error("FileGDB importer unavailable.");
        await bridge().importGDBFiles(files);
        if(owner.sourceMissing)throw new Error("No compatible feature class was selected from the GDB folder.");
        renderRepairChoices();
        notify("Repaired "+owner.name+" from FileGDB folder: "+(owner._repairReport?.featureCount||0)+" feature(s).");
      }catch(err){notify("FileGDB repair failed: "+(err.message||err));}
      finally{delete window.__sabzGdbRepairTargetName;event.target.value="";}
    };
  }
  function start() {
    if(!bridge()?.map||!Array.isArray(bridge().namedLayers))return;
    installUi();renderRepairChoices();
    const original=bridge().addGeoJSONLayer;
    bridge().addGeoJSONLayer=function(name,data){const result=original.call(this,name,data);renderRepairChoices();return result;};
    window.SabzArcMapManifest={importManifest,repairSelectedLayer,renderRepairChoices,version:"1.0.0"};
  }
  if(document.readyState==="loading")document.addEventListener("DOMContentLoaded",start,{once:true});else start();
})();