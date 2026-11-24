(function(root){
"use strict";
function create({namedLayers,L,addNamedLayer,drawn,getActiveDrawingSetName,setSelectedOverlay,setEditTarget,$,refreshEditorTargets,refreshAttributeLayers,renderLayerList}){
 return function drawingLayerForType(type){
  const kind=type==="Marker"?"Point":(["Polyline","FreehandLine"].includes(type)?"Line":"Polygon");
  const suffix=kind==="Point"?"Points":kind==="Line"?"Lines":"Polygons";
  const setName=getActiveDrawingSetName()||"My Drawings";
  let item=namedLayers.find(x=>x.drawingSet===setName&&x._webgisDrawingType===kind);
  if(!item){
   const name=setName+" / "+suffix;
   const layer=new L.FeatureGroup();
   addNamedLayer(name,layer,false);
   item=namedLayers.find(x=>x.layer===layer);
   if(item){item.drawingSet=setName;item._webgisDrawingType=kind;item.displayName=suffix;}
  }
  if(!item)return drawn;
  setSelectedOverlay(item.layer);setEditTarget(item.layer);
  const index=namedLayers.findIndex(x=>x.layer===item.layer);
  if(index>=0) $("editTargetLayer").value=String(index);
  refreshEditorTargets();
  if(index>=0) $("editTargetLayer").value=String(index);
  refreshAttributeLayers();
  if($("attributeLayer"))$("attributeLayer").value=String(index);
  renderLayerList();
  return item.layer;
 };
}
root.SabzWebGISDrawingLayer={create};
})(window);
