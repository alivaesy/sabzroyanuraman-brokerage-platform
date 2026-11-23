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
    return Object.freeze({updateEditorToolbar,updateStandardActionButtons,splitPolygonWithCutter,splitPolygonAtTargetArea,splitPolygonByRotatingCut,vertexGeometryIsValid,restoreVertexGeometry:(layer,geometry)=>restoreVertexGeometry(root.L,layer,geometry),createEditOperationContext});
  }
  root.SabzWebGISFeatureEditing={create};
})(window);
