/* Geometry transforms for selected editable features. */
(function(root){
"use strict";
function create({blockForUnsavedTableEdits,editableSelectedRecords,showEditToolOptions,turf,status,commitGeometryOperation}){
 if(typeof editableSelectedRecords!=="function"||typeof showEditToolOptions!=="function"||!turf||typeof commitGeometryOperation!=="function")throw new TypeError("Geometry transform dependencies are incomplete.");
 async function moveSelected(){
  if(typeof blockForUnsavedTableEdits==="function"&&blockForUnsavedTableEdits("moving selected features"))return;
  const rs=editableSelectedRecords(false);if(!rs.length)return;
  const offsets=await showEditToolOptions("Move Features — offsets in meters",[{label:"East X (positive = east)",value:0},{label:"North Y (positive = north)",value:0}]);if(!offsets)return;
  const [dx,dy]=offsets;if(dx===0&&dy===0){status("No movement specified.");return;}
  const distance=Math.hypot(dx,dy),bearing=(Math.atan2(dx,dy)*180/Math.PI+360)%360,updates=[];
  try{rs.forEach(r=>{if(!r.feature.geometry)throw new Error("A selected feature has no geometry.");const moved=turf.transformTranslate(r.feature,distance,bearing,{units:"meters"});updates.push({r,geometry:moved.geometry});});}
  catch(e){status("Move cancelled: "+(e.message||"geometry transform failed"));return;}
  commitGeometryOperation(updates,"Move").then(ok=>{if(ok)status(updates.length+" feature(s) moved "+distance.toFixed(2)+" m and saved.");});
 }
 async function rotateSelected(){
  if(typeof blockForUnsavedTableEdits==="function"&&blockForUnsavedTableEdits("rotating selected features"))return;
  const rs=editableSelectedRecords(false);if(!rs.length)return;
  const values=await showEditToolOptions("Rotate Features",[{label:"Angle in degrees (positive = counterclockwise)",value:90}]);if(!values)return;
  const [deg]=values;if(deg===0){status("Rotation angle is zero.");return;}
  const updates=[];
  try{const valid=rs.filter(r=>r.feature?.geometry);if(valid.length!==rs.length)throw new Error("A selected feature has no geometry.");const bounds=turf.bbox(turf.featureCollection(valid.map(r=>r.feature))),pivot=[(bounds[0]+bounds[2])/2,(bounds[1]+bounds[3])/2];if(!pivot.every(Number.isFinite))throw new Error("Could not determine a common rotation origin.");valid.forEach(r=>{const rotated=turf.transformRotate(r.feature,deg,{pivot});updates.push({r,geometry:rotated.geometry});});}
  catch(e){status("Rotation cancelled: "+(e.message||"geometry transform failed"));return;}
  commitGeometryOperation(updates,"Rotate").then(ok=>{if(ok)status(updates.length+" feature(s) rotated by "+deg+"° and saved.");});
 }
 async function scaleSelected(){
  if(typeof blockForUnsavedTableEdits==="function"&&blockForUnsavedTableEdits("scaling selected features"))return;
  const rs=editableSelectedRecords(false);if(!rs.length)return;
  const values=await showEditToolOptions("Scale Features",[{label:"Scale factor (greater than 0; 1 = unchanged)",value:1.1}]);if(!values)return;
  const [factor]=values;if(factor<=0){status("Scale cancelled: enter a number greater than zero.");return;}if(factor===1){status("Scale factor is 1; geometry unchanged.");return;}
  const updates=[];
  try{const valid=rs.filter(r=>r.feature?.geometry);if(valid.length!==rs.length)throw new Error("A selected feature has no geometry.");const bounds=turf.bbox(turf.featureCollection(valid.map(r=>r.feature))),origin=[(bounds[0]+bounds[2])/2,(bounds[1]+bounds[3])/2];if(!origin.every(Number.isFinite))throw new Error("Could not determine a common scale origin.");valid.forEach(r=>{const scaled=turf.transformScale(r.feature,factor,{origin});updates.push({r,geometry:scaled.geometry});});}
  catch(e){status("Scale cancelled: "+(e.message||"geometry transform failed"));return;}
  commitGeometryOperation(updates,"Scale").then(ok=>{if(ok)status(updates.length+" feature(s) scaled by ×"+factor+" and saved.");});
 }
 return Object.freeze({moveSelected,rotateSelected,scaleSelected});
}
root.SabzWebGISGeometryTransforms={create};
})(window);
