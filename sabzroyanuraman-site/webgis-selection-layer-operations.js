/* Selected feature layer operations: create, clone and explode multipart features. */
(function(root){
"use strict";
function create(ctx){
 const {selectedRecords,status,L,bindFeature,addNamedLayer,scheduleAutoSave,editableSelectedRecords,refreshEditorTargets,refreshAttributeLayers,blockForUnsavedTableEdits,getEditSessionActive,getEditTarget,getDrawn,arcMapDialog,prepareDerivedFeature,recordUndoState,selectedFeatures,getSelectedLayer,setSelectedLayer,attachFeatureSelection,applySelectionVisual,refreshAll,updateStandardActionButtons}=ctx;
 function newLayerFromSelection(){
  const rs=selectedRecords();if(!rs.length){status("Select at least one feature first.");return;}
  const fc={type:"FeatureCollection",features:rs.map(r=>JSON.parse(JSON.stringify(r.feature)))};
  const layer=L.geoJSON(fc,{onEachFeature:(f,l)=>bindFeature(l,f)});
  const name="Selection_"+new Date().toISOString().slice(0,19).replace(/[:T]/g,"-");
  addNamedLayer(name,layer,false);scheduleAutoSave();status("Layer created from selected features.");
 }
 function cloneSelectedFeatures(){
  const rs=editableSelectedRecords(false);if(!rs.length)return;
  const clones=rs.map(r=>JSON.parse(JSON.stringify(r.feature)));
  const layer=L.geoJSON({type:"FeatureCollection",features:clones},{onEachFeature:(f,l)=>bindFeature(l,f)});
  addNamedLayer("Clone_"+new Date().toISOString().replace(/[:.]/g,"-"),layer,true);
  refreshEditorTargets();refreshAttributeLayers();scheduleAutoSave();
  status(clones.length+" feature(s) cloned into a new vector layer.");
 }
 async function explodeSelectedFeatures(){
  if(typeof blockForUnsavedTableEdits==="function"&&blockForUnsavedTableEdits("exploding multipart features"))return;
  const editSessionActive=getEditSessionActive(),editTarget=getEditTarget(),drawn=getDrawn();
  if(!editSessionActive||!editTarget){status("Start Editing the target layer before exploding multipart features.");return;}
  const records=editableSelectedRecords(false);if(!records.length)return;
  if(records.some(r=>(r.owner?.layer||drawn)!==editTarget)){status("Explode requires all selected features to belong to the active edit layer.");return;}
  const multipart=records.filter(r=>["MultiPoint","MultiLineString","MultiPolygon"].includes(r.feature?.geometry?.type));
  if(multipart.length!==records.length){status("Explode requires only multipart features in the selection; deselect single-part features and try again.");return;}
  if(!multipart.length){status("Select at least one MultiPoint, MultiLineString, or MultiPolygon feature.");return;}
  if(!await arcMapDialog.confirm({title:"Explode Multipart Features",message:"Explode "+multipart.length+" selected multipart feature(s) into individual features?",confirmText:"Explode"}))return;
  const replacements=[];
  try{multipart.forEach(r=>{const g=r.feature.geometry,types={MultiPoint:"Point",MultiLineString:"LineString",MultiPolygon:"Polygon"};const parts=g.coordinates.map(coordinates=>prepareDerivedFeature({type:"Feature",properties:{...(r.feature.properties||{})},geometry:{type:types[g.type],coordinates:JSON.parse(JSON.stringify(coordinates))}},r.owner.layer));if(!parts.length)throw new Error("A selected multipart feature has no parts.");replacements.push({r,parts});});}
  catch(err){status("Explode cancelled: "+err.message);return;}
  recordUndoState();
  replacements.forEach(({r,parts})=>{const owner=r.owner.layer,old=r.layer,wasSelected=selectedFeatures.has(old),wasActive=getSelectedLayer()===old;owner.removeLayer(old);selectedFeatures.delete(old);parts.forEach(feature=>{const group=L.geoJSON(feature,{style:old.options||{},pointToLayer:(f,ll)=>old instanceof L.CircleMarker?L.circleMarker(ll,old.options||{}):L.marker(ll,old.options||{}),onEachFeature:(f,l)=>bindFeature(l,f)});group.eachLayer(layer=>{layer.feature=feature;owner.addLayer(layer);attachFeatureSelection(layer);if(wasSelected)selectedFeatures.add(layer);if(wasActive)setSelectedLayer(layer);});});});
  setSelectedLayer(selectedRecords()[0]?.layer||null);applySelectionVisual();refreshAll();updateStandardActionButtons();scheduleAutoSave();
  status("Exploded "+replacements.length+" multipart feature(s) into "+replacements.reduce((n,x)=>n+x.parts.length,0)+" feature(s).");
 }
 return Object.freeze({newLayerFromSelection,cloneSelectedFeatures,explodeSelectedFeatures});
}
root.SabzWebGISSelectionLayerOperations={create};
})(window);
