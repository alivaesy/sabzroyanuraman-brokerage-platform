(function(root){
  "use strict";
  function create({$,document,turf,getEditSessionActive,getSelectedFeatures,getFeatureClipboard,getActiveDrawHandler,getActiveDrawPurpose}={}){
    if(typeof $!=="function"||!document||typeof getEditSessionActive!=="function"||typeof getSelectedFeatures!=="function"||typeof getFeatureClipboard!=="function")throw new Error("Feature editing UI dependencies are incomplete.");
function splitPolygonWithCutter(feature,cutter){
  const g=feature?.geometry;
  if(g?.type!=="Polygon"||!Array.isArray(g.coordinates)||!g.coordinates.length)throw new Error("Split requires a valid Polygon with an outer ring.");
  const ring=g.coordinates[0];
  if(!Array.isArray(ring)||ring.length<4)throw new Error("The polygon boundary is invalid.");
  if(typeof turf.lineIntersect!=="function"||typeof turf.lineSlice!=="function"||typeof turf.nearestPointOnLine!=="function"||typeof turf.intersect!=="function")throw new Error("Required Turf line and polygon intersection operations are unavailable.");
  const boundary=turf.lineString(ring),raw=turf.lineIntersect(boundary,cutter).features||[],hits=[];
  raw.forEach(f=>{const c=f.geometry?.coordinates;if(c&&!hits.some(p=>turf.distance(turf.point(p),turf.point(c),{units:"kilometers"})<1e-7))hits.push(c.slice(0,2));});
  if(hits.length!==2)throw new Error("The cutting line must cross the OUTER boundary at exactly two distinct points.");
  const expanded=[];
  for(let i=0;i<ring.length-1;i++){
    const a=ring[i],b=ring[i+1];expanded.push(a.slice());
    const seg=turf.lineString([a,b]),insert=[];
    hits.forEach(c=>{try{const near=turf.nearestPointOnLine(seg,turf.point(c),{units:"kilometers"}),loc=near.properties?.location;if(near.properties?.dist<=0.002&&Number.isFinite(loc)){const len=turf.length(seg,{units:"kilometers"});if(loc>1e-9&&loc<len-1e-9)insert.push({c,loc});}}catch(_){}});
    insert.sort((x,y)=>x.loc-y.loc).forEach(x=>{if(!expanded.some(p=>turf.distance(turf.point(p),turf.point(x.c),{units:"kilometers"})<1e-7))expanded.push(x.c.slice());});
  }
  const indices=hits.map(c=>expanded.findIndex(p=>turf.distance(turf.point(p),turf.point(c),{units:"kilometers"})<1e-7));
  if(indices.some(i=>i<0)||indices[0]===indices[1])throw new Error("Could not locate both intersections on the polygon boundary.");
  const arc=(from,to)=>{const out=[expanded[from].slice()];let i=from,guard=0;while(i!==to&&guard++<=expanded.length){i=(i+1)%expanded.length;out.push(expanded[i].slice());}if(i!==to)throw new Error("Could not trace polygon boundary.");return out;};
  const first=arc(indices[0],indices[1]),second=arc(indices[1],indices[0]);
  const cut=turf.lineSlice(turf.point(hits[0]),turf.point(hits[1]),cutter).geometry.coordinates;
  if(!cut||cut.length<2)throw new Error("Could not extract the cutting segment inside the polygon.");
  const close=coords=>{coords[coords.length-1]=coords[0].slice();return coords;};
  const rings=[close(first.concat(cut.slice(1,-1).reverse(),[first[0].slice()])),close(second.concat(cut.slice(1,-1),[second[0].slice()]))];
  const candidates=rings.map(coords=>({type:"Feature",properties:{...(feature.properties||{})},geometry:{type:"Polygon",coordinates:[coords]}})),parts=[];
  // Overlay each half with the original polygon to retain and clip all interior rings.
  candidates.forEach(candidate=>{
    let result;
    try{result=turf.intersect(turf.featureCollection([candidate,feature]));}
    catch(err){throw new Error("Could not preserve polygon holes during split: "+(err.message||"polygon intersection failed"));}
    if(!result?.geometry)return;
    if(!["Polygon","MultiPolygon"].includes(result.geometry.type))throw new Error("Polygon intersection returned an unsupported geometry.");
    result.properties={...(feature.properties||{})};
    if(!Number.isFinite(turf.area(result))||turf.area(result)<=0)throw new Error("The cut produced an empty polygon.");
    if(typeof turf.kinks==="function"&&(turf.kinks(result).features||[]).length)throw new Error("The cut produced a self-intersecting polygon; no changes were applied.");
    parts.push(result);
  });
  if(parts.length!==2)throw new Error("The cutter must divide the polygon into exactly two non-empty parts.");
  const sourceArea=turf.area(feature),partsArea=parts.reduce((sum,part)=>sum+turf.area(part),0);
  if(!Number.isFinite(sourceArea)||sourceArea<=0||Math.abs(partsArea-sourceArea)>Math.max(0.1,sourceArea*1e-6))throw new Error("Split validation failed: output areas do not match the original polygon.");
  return parts;
}

function splitPolygonAtTargetArea(feature,referenceLine,targetAreaM2,side){
  const coords=referenceLine?.geometry?.coordinates||[];
  if(coords.length<2)throw new Error("Draw a reference line with at least two vertices.");
  const bbox=turf.bbox(feature),diag=Math.hypot(bbox[2]-bbox[0],bbox[3]-bbox[1]);
  const totalArea=turf.area(feature);
  if(!Number.isFinite(targetAreaM2)||targetAreaM2<=0||targetAreaM2>=totalArea)throw new Error("Target area must be greater than zero and smaller than the polygon area.");
  const a=coords[0],b=coords[coords.length-1],dx=b[0]-a[0],dy=b[1]-a[1],len=Math.hypot(dx,dy);
  if(len<1e-10)throw new Error("The reference line is too short.");
  const nx=-dy/len,ny=dx/len,mid=[(a[0]+b[0])/2,(a[1]+b[1])/2];
  const span=Math.max(diag*2,0.0001),samples=100,candidates=[];
  for(let i=0;i<=samples;i++){
    const offset=-span+2*span*i/samples;
    const center=[mid[0]+nx*offset,mid[1]+ny*offset],half=span*3;
    const cutter=turf.lineString([[center[0]-dx/len*half,center[1]-dy/len*half],[center[0]+dx/len*half,center[1]+dy/len*half]]);
    try{
      const parts=splitPolygonWithCutter(feature,cutter);
      const scored=parts.map(part=>{
        const p=turf.centroid(part).geometry.coordinates;
        const cross=dx*(p[1]-center[1])-dy*(p[0]-center[0]);
        return {part,area:turf.area(part),isLeft:cross>=0};
      });
      const chosen=scored.find(x=>x.isLeft===(side!=="right"));
      if(chosen)candidates.push({parts,area:chosen.area,diff:Math.abs(chosen.area-targetAreaM2)});
    }catch(_){}
  }
  if(!candidates.length)throw new Error("No valid parallel cut found. Draw a reference line crossing the polygon and try again.");
  candidates.sort((x,y)=>x.diff-y.diff);
  const best=candidates[0];
  if(best.diff>Math.max(1,totalArea*0.002))throw new Error("Could not reach the requested area accurately; try a different reference line.");
  return {parts:best.parts,actualArea:best.area,targetArea:targetAreaM2};
}

function splitPolygonByRotatingCut(feature,referenceLine,targetAreaM2,side,direction){
  if(feature?.geometry?.type!=="Polygon")throw new Error("Rotating target-area cut currently requires a single Polygon.");
  const coords=referenceLine?.geometry?.coordinates||[];
  if(coords.length<2)throw new Error("Draw a starting line from the pivot point.");
  const pivot=coords[0],end=coords[coords.length-1],dx=end[0]-pivot[0],dy=end[1]-pivot[1],startAngle=Math.atan2(dy,dx);
  const total=turf.area(feature);
  if(!Number.isFinite(targetAreaM2)||targetAreaM2<=0||targetAreaM2>=total)throw new Error("Target area must be greater than zero and smaller than the polygon area.");
  if(!turf.booleanPointInPolygon(turf.point(pivot),feature))throw new Error("The first point of the drawn line must be inside the polygon.");
  const bbox=turf.bbox(feature),span=Math.max(Math.hypot(bbox[2]-bbox[0],bbox[3]-bbox[1])*3,0.001),steps=180,candidates=[];
  for(let i=0;i<=steps;i++){
    const angle=startAngle+(direction==="ccw"?1:-1)*Math.PI*2*i/steps;
    const cutter=turf.lineString([[pivot[0]-Math.cos(angle)*span,pivot[1]-Math.sin(angle)*span],[pivot[0]+Math.cos(angle)*span,pivot[1]+Math.sin(angle)*span]]);
    try{
      const parts=splitPolygonWithCutter(feature,cutter);
      const scored=parts.map(part=>{
        const p=turf.centroid(part).geometry.coordinates;
        const cross=Math.cos(angle)*(p[1]-pivot[1])-Math.sin(angle)*(p[0]-pivot[0]);
        return {area:turf.area(part),isLeft:cross>=0};
      });
      const chosen=scored.find(x=>x.isLeft===(side!=="right"));
      if(chosen)candidates.push({parts,area:chosen.area,diff:Math.abs(chosen.area-targetAreaM2)});
    }catch(_){}
  }
  if(!candidates.length)throw new Error("No valid rotating cut found; ensure the pivot point is inside the polygon.");
  candidates.sort((a,b)=>a.diff-b.diff);
  const best=candidates[0];
  if(best.diff>Math.max(1,total*0.003))throw new Error("Could not reach the target area accurately; try another pivot point.");
  return {parts:best.parts,actualArea:best.area,targetArea:targetAreaM2};
}

function vertexGeometryIsValid(g){
  const same=(a,b)=>Array.isArray(a)&&Array.isArray(b)&&a[0]===b[0]&&a[1]===b[1];
  const pos=p=>Array.isArray(p)&&p.length>=2&&Number.isFinite(Number(p[0]))&&Number.isFinite(Number(p[1]))&&Math.abs(Number(p[0]))<=180&&Math.abs(Number(p[1]))<=90;
  const noConsecutiveDuplicates=a=>a.every((p,i)=>i===0||!same(p,a[i-1]));
  const line=a=>Array.isArray(a)&&a.length>=2&&a.every(pos)&&noConsecutiveDuplicates(a)&&new Set(a.map(p=>p[0]+","+p[1])).size>=2;
  const ring=a=>{
    if(!Array.isArray(a)||a.length<4||!a.every(pos)||!same(a[0],a[a.length-1])||!noConsecutiveDuplicates(a))return false;
    const unique=new Set(a.slice(0,-1).map(p=>p[0]+","+p[1]));
    if(unique.size<3)return false;
    let twiceArea=0;
    for(let i=0,j=a.length-2;i<a.length-1;j=i++)twiceArea+=Number(a[j][0])*Number(a[i][1])-Number(a[i][0])*Number(a[j][1]);
    return Number.isFinite(twiceArea)&&Math.abs(twiceArea)>1e-12;
  };
  if(!g||!g.type)return false;
  let valid=false;
  switch(g.type){
    case "Point":valid=pos(g.coordinates);break;
    case "LineString":valid=line(g.coordinates);break;
    case "MultiLineString":valid=Array.isArray(g.coordinates)&&g.coordinates.length>0&&g.coordinates.every(line);break;
    case "Polygon":valid=Array.isArray(g.coordinates)&&g.coordinates.length>0&&g.coordinates.every(ring);break;
    case "MultiPolygon":valid=Array.isArray(g.coordinates)&&g.coordinates.length>0&&g.coordinates.every(p=>Array.isArray(p)&&p.length>0&&p.every(ring));break;
    default:return false;
  }
  if(!valid)return false;
  if(turf&&typeof turf.kinks==="function"&&["Polygon","MultiPolygon"].includes(g.type)){
    try{if(turf.kinks({type:"Feature",properties:{},geometry:g}).features.length)return false}catch(_){return false}
  }
  if(turf&&typeof turf.booleanValid==="function"){
    try{if(!turf.booleanValid({type:"Feature",properties:{},geometry:g}))return false}catch(_){}
  }
  return true;
}

function restoreVertexGeometry(L,layer,geometry){
  if(!layer||!geometry)return false;
  try{
    const g=JSON.parse(JSON.stringify(geometry));
    if(g.type==="Point"&&layer.setLatLng){layer.setLatLng([g.coordinates[1],g.coordinates[0]])}
    else if(layer.setLatLngs&&Array.isArray(g.coordinates)){
      const levels={LineString:0,MultiLineString:1,Polygon:1,MultiPolygon:2}[g.type];
      if(levels===undefined)return false;
      layer.setLatLngs(L.GeoJSON.coordsToLatLngs(g.coordinates,levels));
      if(layer.redraw)layer.redraw();
    }else return false;
    const old=layer.feature||{type:"Feature",properties:{}};
    layer.feature={...old,type:"Feature",geometry:g,properties:{...(old.properties||{})}};
    return true;
  }catch(err){console.warn("Could not restore last valid vertex geometry",err);return false}
}
    function updateStandardActionButtons(){
      const hasSelection=getSelectedFeatures().size>0,editing=!!getEditSessionActive();
      $("copyBtn").disabled=!hasSelection;
      $("cutBtn").disabled=!hasSelection||!editing;
      $("deleteBtn").disabled=!hasSelection||!editing;
      $("pasteBtn").disabled=!getFeatureClipboard().length||!editing;
    }
    function updateEditorToolbar(){
      const editing=!!getEditSessionActive();
      $("editorToolbar")?.classList.toggle("editing-active",editing);
      $("editMenu")?.classList.toggle("editing-active",editing);
      $("startEditingBtn").disabled=editing;
      $("saveEditsBtn").disabled=!editing;
      ["saveTableEdits","addTableField","deleteTableField","deleteSelectedRows","fillSelected"].forEach(id=>{const b=$(id);if(b)b.disabled=!editing});
      $("stopEditingBtn").disabled=!editing;
      $("editTargetLayer").disabled=editing;
      document.querySelectorAll("[data-editor-draw]").forEach(b=>{b.disabled=false;b.title=editing?"Create "+(b.getAttribute("aria-label")||"feature"):"Create "+(b.getAttribute("aria-label")||"feature")+" (starts editing automatically)";});
      const finishButton=$("finishSketchBtn");if(finishButton)finishButton.disabled=!editing||!(getActiveDrawHandler()&&getActiveDrawPurpose()==="draw");
      const state=$("editorStateLabel");if(state){state.textContent=editing?"Editing "+($("editTargetLayer").selectedOptions[0]?.textContent||"layer"):"Start Editing to enable drawing";state.classList.toggle("active",editing)}
      updateStandardActionButtons();
    }

    // Shared contract for stateful geometry-edit operations. All mutable state is
    // accessed through getters/setters so extracted commands never capture stale values.
    function createEditOperationContext(deps={}){
      const required=["getMap","getLeaflet","getTurf","getEditTarget","getSelectedFeatures","getSelectedLayer","setSelectedLayer","getEditSessionActive","getEditTargetLayer","getEditTargetSource","getUndoState","recordUndoState","getEditableSelectedRecords","getSelectedRecords","removeFeatureLayer","addFeatureLayer","attachFeatureSelection","applySelectionVisual","refreshAll","flushGeodatabaseEdits","scheduleAutoSave","prepareDerivedFeature","validateGeometry","status","blockForUnsavedTableEdits","stopActiveDraw","bindFeature","getDialog","getActiveDrawHandler","setActiveDrawHandler","getActiveDrawPurpose","setActiveDrawPurpose"];
      const missing=required.filter(name=>typeof deps[name]!=="function");
      if(missing.length)throw new Error("Geometry edit operation context is missing dependencies: "+missing.join(", "));
      const read=name=>deps[name]();
      return Object.freeze({
        get map(){return read("getMap")},get L(){return read("getLeaflet")},get turf(){return read("getTurf")},
        get editTarget(){return read("getEditTarget")},get editTargetLayer(){return read("getEditTargetLayer")},
        get editTargetSource(){return read("getEditTargetSource")},get selectedFeatures(){return read("getSelectedFeatures")},
        get selectedLayer(){return read("getSelectedLayer")},setSelectedLayer:value=>deps.setSelectedLayer(value),
        get editSessionActive(){return !!read("getEditSessionActive")},get undoState(){return read("getUndoState")},
        getEditableSelectedRecords:()=>read("getEditableSelectedRecords"),getSelectedRecords:()=>read("getSelectedRecords"),
        recordUndoState:()=>deps.recordUndoState(),removeFeatureLayer:(owner,layer)=>deps.removeFeatureLayer(owner,layer),
        addFeatureLayer:(owner,layer)=>deps.addFeatureLayer(owner,layer),attachFeatureSelection:layer=>deps.attachFeatureSelection(layer),
        applySelectionVisual:()=>deps.applySelectionVisual(),refreshAll:()=>deps.refreshAll(),
        flushGeodatabaseEdits:()=>deps.flushGeodatabaseEdits(),scheduleAutoSave:()=>deps.scheduleAutoSave(),
        prepareDerivedFeature:(feature,owner)=>deps.prepareDerivedFeature(feature,owner),
        validateGeometry:geometry=>deps.validateGeometry(geometry),status:message=>deps.status(message),
        blockForUnsavedTableEdits:reason=>deps.blockForUnsavedTableEdits(reason),
        stopActiveDraw:force=>deps.stopActiveDraw(force),bindFeature:(feature,layer)=>deps.bindFeature(feature,layer),
        get dialog(){return read("getDialog")},
        get activeDrawHandler(){return read("getActiveDrawHandler")},setActiveDrawHandler:value=>deps.setActiveDrawHandler(value),
        get activeDrawPurpose(){return read("getActiveDrawPurpose")},setActiveDrawPurpose:value=>deps.setActiveDrawPurpose(value)
      });
    }

    function createGeometryEditOperations(deps={}){
      const required=["getMap","getLeaflet","getTurf","getEditSessionActive","getEditTarget","getSelectedFeatures","getSelectedLayer","setSelectedLayer","setSelectedOverlay","getDrawn","getWindow","getUndoStack","getRedoStack","blockForUnsavedTableEdits","status","editableSelectedRecords","selectedRecords","stopSplitDraw","stopReshapeDraw","stopActiveDraw","prepareDerivedFeature","vertexGeometryIsValid","recordUndoState","attachFeatureSelection","applySelectionVisual","refreshAll","updateStandardActionButtons","flushGeodatabaseEdits","scheduleAutoSave","arcMapDialog","rebuildVectorLayerGeometry","validateAndSyncEditTarget","serializeProject","pushUndoSnapshot","bindFeature","getFeatureRecords","restoreVertexGeometry"];
      const missing=required.filter(name=>!(name in deps)||((name.startsWith("get")||name.startsWith("set")||["blockForUnsavedTableEdits","status","editableSelectedRecords","selectedRecords","stopSplitDraw","stopReshapeDraw","stopActiveDraw","prepareDerivedFeature","vertexGeometryIsValid","recordUndoState","attachFeatureSelection","applySelectionVisual","refreshAll","updateStandardActionButtons","flushGeodatabaseEdits","scheduleAutoSave","arcMapDialog","rebuildVectorLayerGeometry","validateAndSyncEditTarget","serializeProject","pushUndoSnapshot","historyRestoring","bindFeature","getFeatureRecords"].includes(name))&&typeof deps[name]!=="function"));
      if(missing.length)throw new Error("Geometry edit operations are missing dependencies: "+missing.join(", "));
      const run=()=>{
        const map=deps.getMap(),L=deps.getLeaflet(),turf=deps.getTurf();
        const editSessionActive=!!deps.getEditSessionActive(),editTarget=deps.getEditTarget();
        const selectedFeatures=deps.getSelectedFeatures(),selectedLayer=deps.getSelectedLayer(),drawn=deps.getDrawn(),window=deps.getWindow();
        const undoStack=deps.getUndoStack(),redoStack=deps.getRedoStack();
        const setSelectedLayer=value=>deps.setSelectedLayer(value),setSelectedOverlay=value=>deps.setSelectedOverlay(value);
        const {blockForUnsavedTableEdits,status,editableSelectedRecords,selectedRecords,stopSplitDraw,stopReshapeDraw,stopActiveDraw,prepareDerivedFeature,vertexGeometryIsValid,recordUndoState,attachFeatureSelection,applySelectionVisual,refreshAll,updateStandardActionButtons,flushGeodatabaseEdits,scheduleAutoSave,arcMapDialog,rebuildVectorLayerGeometry,validateAndSyncEditTarget,serializeProject,pushUndoSnapshot,historyRestoring,bindFeature,getFeatureRecords,restoreVertexGeometry}=deps;
        async function mergeSelectedFeatures(){
  if(typeof blockForUnsavedTableEdits==="function"&&blockForUnsavedTableEdits("merging features"))return;
  if(!editSessionActive||!editTarget){status("Start Editing before merging features.");return;}
  const records=editableSelectedRecords(false);
  if(records.length<2){status("Select at least two features in the active edit layer to merge.");return;}
  const type=records[0].feature?.geometry?.type;
  if(!records.every(r=>r.owner.layer===editTarget&&r.feature?.geometry?.type===type)){status("Merge requires selected features of the same geometry type in one edit layer.");return;}
  if(!["Polygon","MultiPolygon","LineString","MultiLineString"].includes(type)){status("Merge supports line and polygon features; point merging is not supported yet.");return;}
  const props={...records[0].feature.properties};
  if(!await arcMapDialog.confirm({title:"Merge Features",message:"Merge "+records.length+" selected features? Attributes will be taken from the first selected feature.",confirmText:"Merge"}))return;
  let merged;
  try{
    if(type==="Polygon"||type==="MultiPolygon"){
      const collection={type:"FeatureCollection",features:records.map(r=>r.feature)};
      merged=turf.union(collection);
      if(!merged)throw new Error("The polygons do not produce a merged geometry.");
    }else{
      // Stitch connected line parts by matching endpoints, reversing parts when
      // necessary. Disconnected chains remain a valid MultiLineString.
      const remaining=[];
      records.forEach(r=>{const g=r.feature.geometry;if(g.type==="LineString")remaining.push(JSON.parse(JSON.stringify(g.coordinates)));else g.coordinates.forEach(line=>remaining.push(JSON.parse(JSON.stringify(line))));});
      if(remaining.some(line=>!Array.isArray(line)||line.length<2))throw new Error("A selected line has fewer than two vertices.");
      const samePoint=(a,b)=>Array.isArray(a)&&Array.isArray(b)&&Math.abs(a[0]-b[0])<1e-10&&Math.abs(a[1]-b[1])<1e-10;
      const chains=[];
      while(remaining.length){
        const chain=remaining.shift();
        let changed=true;
        while(changed){
          changed=false;
          for(let i=0;i<remaining.length;i++){
            const next=remaining[i],start=chain[0],end=chain[chain.length-1];
            if(samePoint(end,next[0]))chain.push(...next.slice(1));
            else if(samePoint(end,next[next.length-1]))chain.push(...next.slice(0,-1).reverse());
            else if(samePoint(start,next[next.length-1]))chain.unshift(...next.slice(0,-1));
            else if(samePoint(start,next[0]))chain.unshift(...next.slice(1).reverse());
            else continue;
            remaining.splice(i,1);changed=true;break;
          }
        }
        chains.push(chain);
      }
      merged={type:"Feature",properties:props,geometry:chains.length===1?{type:"LineString",coordinates:chains[0]}:{type:"MultiLineString",coordinates:chains}};
    }
    merged.properties={...props};
    prepareDerivedFeature(merged,editTarget);
    if(!merged.geometry||!vertexGeometryIsValid(merged.geometry))throw new Error("The merged geometry failed validity checks.");
  }catch(err){status("Merge cancelled: "+(err.message||"geometry operation failed"));return;}
  recordUndoState();
  const owner=editTarget,selected=records.map(r=>r.layer);
  selected.forEach(layer=>{owner.removeLayer(layer);selectedFeatures.delete(layer);});
  const temp=L.geoJSON(merged,{style:records[0].layer.options||{},pointToLayer:(f,ll)=>L.circleMarker(ll,{radius:6})});
  temp.eachLayer(layer=>{layer.feature=merged;owner.addLayer(layer);attachFeatureSelection(layer);selectedFeatures.add(layer);setSelectedLayer(layer);});
  setSelectedOverlay(null);applySelectionVisual();refreshAll();updateStandardActionButtons();
  Promise.all([flushGeodatabaseEdits(),scheduleAutoSave()]).then(([synced,saved])=>status("Merged "+records.length+" features. Attributes were retained from the first selected feature."+(synced&&saved?"":" Saving failed; retry Save Edits."))).catch(err=>status("Merge applied in memory but saving failed: "+err.message));
}

async function splitSelectedLines(){
  if(typeof blockForUnsavedTableEdits==="function"&&blockForUnsavedTableEdits("splitting features"))return;
  if(!editSessionActive||!editTarget){status("Start Editing before splitting features.");return;}
  stopSplitDraw();
  const records=editableSelectedRecords(false);if(!records.length)return;
  if(records.some(r=>(r.owner?.layer||drawn)!==editTarget)){status("Split requires selected features from the active edit layer.");return;}
  const lineMode=records.every(r=>["LineString","MultiLineString"].includes(r.feature?.geometry?.type));
  const polygonMode=records.length===1&&["Polygon","MultiPolygon"].includes(records[0].feature?.geometry?.type);
  if(!lineMode&&!polygonMode){status("Split supports selected lines, or one Polygon/MultiPolygon including interior rings, in the active edit layer.");return;}
  if(typeof turf.lineSplit!=="function"){status("Split unavailable: Turf.js lineSplit is not loaded.");return;}
  stopActiveDraw(true);
  status(polygonMode?"Draw a cutting line across the polygon; it must cross the outer boundary twice.":"Draw a cutting line across selected line feature(s); double-click to finish.");
  const handler=new L.Draw.Polyline(map,{shapeOptions:{color:"#c0392b",weight:2,dashArray:"5,4"}});window._splitDrawHandler=handler;
  const created=async event=>{
    map.off(L.Draw.Event.CREATED,created);window._splitCreatedHandler=null;window._splitDrawHandler=null;
    try{
      const cutter=event.layer.toGeoJSON();
      if(cutter.geometry?.type!=="LineString"||cutter.geometry.coordinates.length<2)throw new Error("Draw a line with at least two vertices.");
      const results=[];
      if(polygonMode){
        const source=records[0].feature,g=source.geometry;
        let parts;
        const splitMode=await arcMapDialog.select({title:"Split Polygon",message:"Choose the cutting method:",label:"Cut method",value:"1",options:[{value:"1",label:"Cut along the drawn line"},{value:"2",label:"Parallel cut by target area"},{value:"3",label:"Rotate cut to target area"}]});
        if(splitMode===null)return;
        if(splitMode.trim()==="2"||splitMode.trim()==="3"){
          if(g.type!=="Polygon")throw new Error("Target-area cuts currently require a single Polygon.");
          const rawArea=await arcMapDialog.prompt({title:"Split Polygon",message:"Target area in square meters (m²):",label:"Target area",value:String(Math.round(turf.area(source)/2))});
          if(rawArea===null)return;
          const targetArea=Number(rawArea);
          const sideAnswer=await arcMapDialog.select({title:"Split Polygon",message:"Keep the target area on which side of the cut?",label:"Side",value:"left",options:[{value:"left",label:"Left"},{value:"right",label:"Right"}]});
          if(sideAnswer===null)return;
          const side=sideAnswer.trim().toLowerCase();
          if(!["left","right"].includes(side))throw new Error("Side must be left or right.");
          let result;
          if(splitMode.trim()==="2")result=splitPolygonAtTargetArea(source,cutter,targetArea,side);
          else{
            const directionAnswer=await arcMapDialog.select({title:"Split Polygon",message:"Choose the rotation direction:",label:"Direction",value:"clockwise",options:[{value:"clockwise",label:"Clockwise"},{value:"counterclockwise",label:"Counterclockwise"}]});
            if(directionAnswer===null)return;
            const direction=directionAnswer.trim().toLowerCase();
            if(!["clockwise","counterclockwise"].includes(direction))throw new Error("Direction must be clockwise or counterclockwise.");
            result=splitPolygonByRotatingCut(source,cutter,targetArea,side,direction==="counterclockwise"?"ccw":"cw");
          }
          parts=result.parts;
          status("Target-area cut: "+Math.round(result.actualArea).toLocaleString()+" m² (target "+Math.round(targetArea).toLocaleString()+" m²).");
        }else if(splitMode.trim()!=="1")throw new Error("Choose 1 for drawn-line cut or 2 for target-area parallel cut.");
        else if(g.type==="Polygon")parts=splitPolygonWithCutter(source,cutter);
        else{
          if(!Array.isArray(g.coordinates)||g.coordinates.some(poly=>!Array.isArray(poly)||!poly.length||!Array.isArray(poly[0])||poly[0].length<4))
            throw new Error("MultiPolygon contains an invalid component or outer ring.");
          const output=[];let splitCount=0;
          g.coordinates.forEach(poly=>{
            const component={type:"Feature",properties:{...(source.properties||{})},geometry:{type:"Polygon",coordinates:poly}};
            try{
              const pieces=splitPolygonWithCutter(component,cutter);
              output.push(...pieces);splitCount++;
            }catch(err){
              if(/must cross the polygon boundary at exactly two distinct points/.test(err.message))
                output.push(component);
              else throw err;
            }
          });
          if(!splitCount)throw new Error("The cutting line did not split any MultiPolygon component.");
          parts=output;
        }
        parts.forEach(part=>prepareDerivedFeature(part,records[0].owner.layer));
        results.push({r:records[0],parts});
      }else records.forEach(r=>{
        const geometry=r.feature.geometry,sourceLines=geometry.type==="LineString"?[geometry.coordinates]:geometry.coordinates,allParts=[];let wasSplit=false;
        sourceLines.forEach(coordinates=>{
          if(!Array.isArray(coordinates)||coordinates.length<2)return;
          const source=turf.lineString(coordinates,{...(r.feature.properties||{})}),split=turf.lineSplit(source,cutter);
          const parts=(split.features||[]).filter(f=>f.geometry?.type==="LineString"&&f.geometry.coordinates.length>=2);
          if(parts.length>1)wasSplit=true;
          if(parts.length)allParts.push(...parts);else allParts.push(source);
        });
        if(wasSplit&&allParts.length>1){allParts.forEach(part=>prepareDerivedFeature(part,r.owner.layer));results.push({r,parts:allParts});}
      });
      if(!results.length){status("No selected feature was split. The cutting line must cross the feature away from its endpoints.");return;}
      if(results.some(item=>!item.parts.length||item.parts.some(part=>!part.geometry||!vertexGeometryIsValid(part.geometry)))){
        status("Split cancelled: one or more output parts have invalid geometry.");return;
      }
      // Build replacement layers before removing source features so construction errors cannot erase the originals.
      results.forEach(({r,parts})=>{parts.forEach(part=>{
        part.properties={...(part.properties||r.feature.properties||{})};
        prepareDerivedFeature(part,r.owner.layer);
        const group=L.geoJSON(part,{style:r.layer.options||{},pointToLayer:(f,ll)=>r.layer instanceof L.CircleMarker?L.circleMarker(ll,r.layer.options||{}):L.marker(ll,r.layer.options||{}),onEachFeature:(f,l)=>bindFeature(l,f)});
        part._preparedLayers=group.getLayers();
        if(!part._preparedLayers.length)throw new Error("Could not build a split output feature.");
      });});
      recordUndoState();
      results.forEach(({r,parts})=>{
        const owner=r.owner.layer,old=r.layer,wasSelected=selectedFeatures.has(old);owner.removeLayer(old);selectedFeatures.delete(old);
        parts.forEach(part=>{
          (part._preparedLayers||[]).forEach(layer=>{layer.feature={type:"Feature",properties:{...(part.properties||{})},geometry:part.geometry};owner.addLayer(layer);attachFeatureSelection(layer);if(wasSelected)selectedFeatures.add(layer);});
          delete part._preparedLayers;
        });
      });
      setSelectedLayer(selectedRecords()[0]?.layer||null);setSelectedOverlay(null);applySelectionVisual();refreshAll();updateStandardActionButtons();
      Promise.all([flushGeodatabaseEdits(),scheduleAutoSave()]).then(([synced,saved])=>status((polygonMode?"Split polygon while preserving holes and interior rings.":"Split "+results.length+" selected line feature(s) into "+results.reduce((n,x)=>n+x.parts.length,0)+" line parts.")+(synced&&saved?"":" Saving failed; retry Save Edits."))).catch(err=>status("Split applied in memory but saving failed: "+err.message));
    }catch(err){status("Split failed: "+(err.message||"invalid cutting geometry"));}
    finally{if(event.layer&&map.hasLayer(event.layer))map.removeLayer(event.layer);}
  };
  window._splitCreatedHandler=created;map.once(L.Draw.Event.CREATED,created);handler.enable();
}

function reshapeSelected(){
  if(typeof blockForUnsavedTableEdits==="function"&&blockForUnsavedTableEdits("reshaping a feature"))return;
  const r=selectedRecords()[0];
  if(!r){status("Select one polygon feature first.");return;}
  const type=r.feature?.geometry?.type;
  if(!["Polygon","MultiPolygon"].includes(type)){status("Reshape currently supports Polygon and MultiPolygon features only.");return;}
  if(!editSessionActive||!editTarget){status("Start Editing before reshaping features.");return;}
  if(r.owner?.layer!==editTarget){status("The selected feature is not in the active edit layer.");return;}
  if(typeof turf.booleanIntersects!=="function"||typeof turf.booleanWithin!=="function"||typeof turf.difference!=="function"||typeof turf.union!=="function"){
    status("Reshape unavailable: required Turf geometry operations are not loaded.");return;
  }
  stopActiveDraw(true);stopReshapeDraw();
  if(window._vertexEditControl){try{map.removeControl(window._vertexEditControl)}catch(_){}window._vertexEditControl=null}
  if(window._vertexEditHandler){map.off(L.Draw.Event.EDITED,window._vertexEditHandler);window._vertexEditHandler=null}
  const original=JSON.parse(JSON.stringify(r.feature));
  status("Draw a polygon overlapping the selected polygon to reshape it. Drawing inside removes that area; drawing across/alongside merges the areas. Stop Editing to cancel.");
  const h=new L.Draw.Polygon(map,{shapeOptions:{color:"#e67e22",weight:2,dashArray:"4,4"}});
  window._reshapeDrawHandler=h;
  const once=async e=>{
    map.off(L.Draw.Event.CREATED,once);
    window._reshapeCreatedHandler=null;
    window._reshapeDrawHandler=null;
    try{
      if(!editSessionActive||editTarget!==(r.owner?.layer||drawn)){status("Reshape cancelled because the active edit layer changed.");return;}
      map.removeLayer(e.layer);
      const sketch=e.layer.toGeoJSON();
      if(!sketch.geometry||sketch.geometry.type!=="Polygon"){status("Reshape cancelled: draw a valid polygon.");return;}
      if(!turf.booleanIntersects(original,sketch)){status("Reshape cancelled: the drawn polygon must overlap or touch the selected polygon.");return;}
      const cut=turf.booleanWithin(sketch,original);
      const fc={type:"FeatureCollection",features:[original,sketch]};
      const result=cut?turf.difference(fc):turf.union(fc);
      if(!result?.geometry||!["Polygon","MultiPolygon"].includes(result.geometry.type)){status("Reshape failed: no valid polygon result was produced.");return;}
      if(!vertexGeometryIsValid(result.geometry)){status("Reshape rejected: the resulting polygon is invalid or self-intersecting; the original feature was preserved.");return;}
      if(JSON.stringify(original.geometry)===JSON.stringify(result.geometry)){status("Reshape made no geometry changes.");return;}
      recordUndoState();
      try{
        r.feature.geometry=JSON.parse(JSON.stringify(result.geometry));
        rebuildVectorLayerGeometry([r]);
        const validation=validateAndSyncEditTarget();
        if(!validation.ok)throw new Error(validation.message||"edit-target validation failed");
        refreshAll();updateStandardActionButtons();
        const [synced,saved]=await Promise.all([flushGeodatabaseEdits(),scheduleAutoSave()]);
        status((cut?"Selected polygon reshaped by removing the drawn interior area.":"Selected polygon reshaped by merging the drawn area.")+(synced&&saved?" Changes saved.":" Changes are visible, but saving/synchronization failed; retry Save Edits."));
      }catch(commitError){
        r.feature.geometry=JSON.parse(JSON.stringify(original.geometry));
        try{rebuildVectorLayerGeometry([r]);refreshAll();}catch(rollbackError){console.error("Reshape rollback failed",rollbackError);}
        status("Reshape cancelled; original geometry restored where possible: "+(commitError.message||"validation or save preparation failed"));
      }
    }catch(err){console.error("Reshape failed",err);status("Reshape failed: "+(err.message||"geometry operation error"));}
  };
  window._reshapeCreatedHandler=once;
  map.on(L.Draw.Event.CREATED,once);
  h.enable();
}

function stopVertexEditing(){
  const owner=window._vertexEditOwner,layers=Array.isArray(window._vertexEditLayers)?window._vertexEditLayers:[],baseline=window._vertexEditBaseline,lastValid=window._vertexEditLastValid;
  let geometryChanged=false,invalidRestored=0;
  // Validate live coordinates even if the user closes the toolbar without clicking its Save icon.
  if(owner&&layers.length&&editSessionActive&&editTarget===owner){
    layers.forEach(layer=>{
      if(!layer||!owner.hasLayer(layer)||typeof layer.toGeoJSON!=="function")return;
      try{
        const updated=layer.toGeoJSON(),oldFeature=layer.feature||{};
        if(!vertexGeometryIsValid(updated.geometry)){
          if(lastValid&&restoreVertexGeometry(layer,lastValid.get(layer)))invalidRestored++;
          else status("Invalid geometry detected; use the Editor Save button to validate vertices before stopping.");
          return;
        }
        if(JSON.stringify(oldFeature.geometry)!==JSON.stringify(updated.geometry))geometryChanged=true;
        updated.properties={...(oldFeature.properties||{}),...(updated.properties||{})};
        layer.feature=updated;
        if(lastValid)lastValid.set(layer,JSON.parse(JSON.stringify(updated.geometry)));
      }catch(err){console.warn("Could not finalize vertex geometry",err)}
    });
    if(invalidRestored){refreshAll();status("Rejected "+invalidRestored+" invalid vertex edit(s); last valid geometry restored.");}
    if(geometryChanged&&baseline&&!historyRestoring){
      pushUndoSnapshot(baseline);
      refreshAll();window.__webGisGdbBridge?.save();scheduleAutoSave();
      status("Vertex edits finalized and added to Undo history.");
    }
  }
  if(window._vertexEditControl){try{map.removeControl(window._vertexEditControl)}catch(err){console.warn("Could not remove vertex editing toolbar",err)}window._vertexEditControl=null;}
  if(window._vertexEditHandler){try{map.off(L.Draw.Event.EDITED,window._vertexEditHandler)}catch(err){console.warn("Could not detach vertex edit listener",err)}window._vertexEditHandler=null;}
  window._vertexEditOwner=null;window._vertexEditLayers=null;window._vertexEditBaseline=null;window._vertexEditLastValid=null;
}

function editVertices(){
  if(typeof blockForUnsavedTableEdits==="function"&&blockForUnsavedTableEdits("editing feature vertices"))return;
  if(!editSessionActive||!editTarget){status("Start Editing the target layer before editing vertices.");return}
  const selected=editableSelectedRecords(false);
  if(!selected.length){status("Select one or more features in the active edit layer first.");return}
  const owner=selected[0].owner?.layer||drawn;
  if(owner!==editTarget){status("Vertex editing is limited to the active edit layer. Select features in that layer.");return}
  if(!owner||!owner.getLayers){status("These features cannot be vertex edited.");return}
  if(selected.some(r=>(r.owner?.layer||drawn)!==owner)){status("Vertex editing requires selected features from one layer at a time.");return}
  const selectedLayers=selected.map(r=>r.layer).filter(layer=>layer&&owner.hasLayer(layer)&&typeof layer.toGeoJSON==="function");
  if(!selectedLayers.length){status("The selected features are no longer available in the active layer.");return}
  const invalidStart=selectedLayers.filter(layer=>!vertexGeometryIsValid(layer.toGeoJSON()?.geometry));
  if(invalidStart.length){
    status("Edit Vertices cannot start: "+invalidStart.length+" selected feature(s) already have invalid geometry. Repair or remove those features first.");
    return;
  }
  try{
    stopVertexEditing();
    const editableGroup=L.featureGroup(selectedLayers);
    let baseline=JSON.parse(JSON.stringify(serializeProject()));
    const lastValid=new Map();
    selectedLayers.forEach(layer=>{const g=layer.toGeoJSON()?.geometry;if(g)lastValid.set(layer,JSON.parse(JSON.stringify(g)))});
    window._vertexEditOwner=owner;window._vertexEditLayers=selectedLayers;window._vertexEditBaseline=baseline;window._vertexEditLastValid=lastValid;
    window._vertexEditHandler=async function(event){
      if(!editSessionActive||editTarget!==owner){status("Vertex edits ignored because the active edit layer changed.");return}
      const edited=[],previousValid=new Map();
      event.layers.eachLayer(layer=>{
        if(!layer||typeof layer.toGeoJSON!=="function"||!selectedLayers.includes(layer)||!owner.hasLayer(layer))return;
        const updated=layer.toGeoJSON(),oldFeature=layer.feature||{};
        previousValid.set(layer,lastValid.get(layer)?JSON.parse(JSON.stringify(lastValid.get(layer))):null);
        updated.properties={...(oldFeature.properties||{}),...(updated.properties||{})};
        if(!vertexGeometryIsValid(updated.geometry)){
          if(restoreVertexGeometry(layer,lastValid.get(layer)))status("Invalid geometry rejected. A line needs at least 2 vertices; polygon rings need at least 3 distinct vertices and a closed boundary.");
          else status("Invalid geometry detected. Cancel the edit and restore the last saved feature.");
          return;
        }
        layer.feature=updated;lastValid.set(layer,JSON.parse(JSON.stringify(updated.geometry)));edited.push(layer);
      });
      if(!edited.length){refreshAll();return}
      const validation=validateAndSyncEditTarget();
      if(!validation.ok){
        edited.forEach(layer=>{
          const previous=previousValid.get(layer);
          if(previous){restoreVertexGeometry(layer,previous);lastValid.set(layer,previous);}
        });
        refreshAll();status("Vertex edit rejected: "+validation.message);return;
      }
      try{
        const current=JSON.stringify(serializeProject()),previous=JSON.stringify(baseline);
        if(current!==previous){pushUndoSnapshot(baseline);baseline=JSON.parse(current);window._vertexEditBaseline=baseline}
      }catch(err){console.warn("Could not preserve vertex-edit history",err)}
      refreshAll();
      const synced=await flushGeodatabaseEdits();
      const saved=await scheduleAutoSave();
      status(synced&&saved?"Vertex edits saved for "+edited.length+" selected feature(s).":"Vertex edits are visible but could not be fully saved; retry Save Edits.");
    };
    map.on(L.Draw.Event.EDITED,window._vertexEditHandler);
    window._vertexEditControl=new L.Control.Draw({draw:false,edit:{featureGroup:editableGroup,selectedPathOptions:{maintainColor:true,opacity:0.9}}});
    map.addControl(window._vertexEditControl);
    status("Vertex editor ready for "+selectedLayers.length+" selected feature(s): drag vertices, click a segment to add a vertex, or click a vertex to remove it; save with the pencil toolbar.");
  }catch(e){
    if(window._vertexEditHandler)map.off(L.Draw.Event.EDITED,window._vertexEditHandler);
    window._vertexEditHandler=null;
    if(window._vertexEditControl){try{map.removeControl(window._vertexEditControl)}catch(_){}window._vertexEditControl=null}
    window._vertexEditOwner=null;window._vertexEditLayers=null;window._vertexEditBaseline=null;window._vertexEditLastValid=null;
    status("Vertex editing failed: "+e.message);
  }
}
        return {mergeSelectedFeatures,splitSelectedLines,reshapeSelected,editVertices,stopVertexEditing};
      };
      const invoke=name=>(...args)=>run()[name](...args);
      return Object.freeze({mergeSelectedFeatures:invoke("mergeSelectedFeatures"),splitSelectedLines:invoke("splitSelectedLines"),reshapeSelected:invoke("reshapeSelected"),editVertices:invoke("editVertices"),stopVertexEditing:invoke("stopVertexEditing")});
    }

    return Object.freeze({createGeometryEditOperations,updateEditorToolbar,updateStandardActionButtons,splitPolygonWithCutter,splitPolygonAtTargetArea,splitPolygonByRotatingCut,vertexGeometryIsValid,restoreVertexGeometry:(layer,geometry)=>restoreVertexGeometry(root.L,layer,geometry),createEditOperationContext});
  }
  root.SabzWebGISFeatureEditing={create};
})(window);
