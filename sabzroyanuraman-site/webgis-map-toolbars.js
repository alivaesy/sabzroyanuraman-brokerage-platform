/* ArcMap-style map navigation and toolbar bindings. */
(function(root){
 "use strict";

 function init(ctx){
  if(!ctx||!ctx.map||!ctx.L||!root.document)throw new TypeError("Map toolbar requires map, Leaflet and document.");
  const d=root.document;
  if(d.__sabzMapToolbarsInitialized)return false;

  const byId=id=>d.getElementById(id);
  const bind=(id,handler)=>{
   const el=byId(id);
   if(el&&typeof handler==="function")el.addEventListener("click",handler);
   return el;
  };
  const setActive=id=>{
   d.querySelectorAll("#classicToolbars button.icon-tool").forEach(button=>button.classList.remove("active"));
   const target=byId(id);
   if(target)target.classList.add("active");
  };
  const report=message=>{if(typeof ctx.status==="function")ctx.status(message);};
  const setTool=tool=>{if(typeof ctx.setActiveMapTool==="function")ctx.setActiveMapTool(tool);};
  const enableDragging=()=>ctx.map.dragging?.enable?.();

  const directBindings={
   startEditingBtn:ctx.startEditing,newMapBtn:ctx.clearWorkspace,printMapBtn:ctx.printMap,
   copyBtn:ctx.copyFeatures,cutBtn:ctx.cutFeatures,pasteBtn:ctx.pasteFeatures,deleteBtn:ctx.deleteFeatures,
   undoBtn:ctx.undoMapEdit,redoBtn:ctx.redoMapEdit,addOnlineDataBtn:ctx.openOnlineData,
   commandLineBtn:ctx.runMapCommandLine,backExtentBtn:ctx.backExtent,nextExtentBtn:ctx.nextExtent,
   selectElementsBtn:ctx.selectElements,hyperlinkBtn:ctx.startHyperlink,htmlPopupBtn:ctx.startHtmlPopup,
   measureToolBtn:ctx.startMeasure,findRouteBtn:ctx.findRoute,timeSliderBtn:ctx.startTimeSlider,
   createViewerBtn:ctx.createMapViewer,saveEditsBtn:ctx.saveEdits,stopEditingBtn:ctx.stopEditing,
   goToXYBtn:ctx.goToXY,clearSelectionBtn:ctx.clearSelection
  };
  Object.entries(directBindings).forEach(([id,handler])=>bind(id,handler));

  d.querySelectorAll("[data-editor-draw]").forEach(button=>{
   button.addEventListener("click",()=>ctx.startDraw?.(button.dataset.editorDraw,button));
  });

  bind("finishSketchBtn",()=>{
   const active=ctx.getActiveDrawHandler?.();
   if(active&&ctx.getActiveDrawPurpose?.()==="draw"&&typeof active.completeShape==="function")active.completeShape();
   else ctx.stopActiveDraw?.(true);
  });

  bind("zoomInBtn",()=>ctx.map.zoomIn());
  bind("zoomOutBtn",()=>ctx.map.zoomOut());

  bind("panMapBtn",()=>{
   ctx.stopActiveDraw?.(true);
   setTool("pan");setActive("panMapBtn");enableDragging();
   report("Pan tool is active.");
  });

  bind("selectRectangleBtn",()=>ctx.selectByRectangle?.());
  bind("selectPolygonBtn",()=>ctx.selectByPolygon?.());

  bind("selectFeaturesBtn",()=>{
   ctx.stopActiveDraw?.(true);
   setTool("select");enableDragging();
   if(ctx.map.getContainer)ctx.map.getContainer().style.cursor="";
   setActive("selectFeaturesBtn");
   report("Select Features is active. Click an existing feature to select it; Ctrl-click or Shift-click adds/removes features.");
  });

  bind("identifyFeatureBtn",()=>{
   setTool("identify");setActive("identifyFeatureBtn");
   report("Click a feature to identify it.");
  });

  bind("fullExtentBtn",()=>{
   const candidates=[ctx.drawn,...(Array.isArray(ctx.namedLayers)?ctx.namedLayers.map(item=>item?.layer):[])].filter(Boolean);
   const bounds=[];
   candidates.forEach(layer=>{
    if(typeof layer.getBounds!=="function")return;
    try{const value=layer.getBounds();if(value?.isValid?.())bounds.push(value);}catch(_){}
   });
   if(!bounds.length){report("No visible feature layers are available for Full Extent.");return;}
   ctx.map.fitBounds(ctx.L.latLngBounds(bounds),{padding:[20,20]});
  });

  d.__sabzMapToolbarsInitialized=true;
  return true;
 }

 root.SabzWebGISMapToolbars=Object.freeze({init});
})(typeof window!=="undefined"?window:globalThis);
