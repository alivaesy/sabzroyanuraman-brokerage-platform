/* ArcMap map-navigation and drawing toolbar event bindings. */
(function(root){
 "use strict";
 function init(ctx){
  const d=document;
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
  Object.entries(directBindings).forEach(([id,handler])=>{const el=d.getElementById(id);if(el&&typeof handler==="function")el.onclick=handler;});
  d.querySelectorAll("[data-editor-draw]").forEach(button=>button.onclick=()=>ctx.startDraw(button.dataset.editorDraw,button));
  d.getElementById("finishSketchBtn").onclick=()=>{
   const active=ctx.getActiveDrawHandler();
   if(active&&ctx.getActiveDrawPurpose()==="draw"&&typeof active.completeShape==="function")active.completeShape();
   else ctx.stopActiveDraw(true);
  };
  d.getElementById("zoomInBtn")?.addEventListener("click",()=>ctx.map.zoomIn());
  d.getElementById("zoomOutBtn")?.addEventListener("click",()=>ctx.map.zoomOut());
  d.getElementById("panMapBtn")?.addEventListener("click",()=>{
   ctx.stopActiveDraw(true);ctx.setActiveMapTool("pan");
   d.querySelectorAll("#classicToolbars button.icon-tool").forEach(x=>x.classList.remove("active"));
   d.getElementById("panMapBtn")?.classList.add("active");
   ctx.map.dragging.enable();ctx.status("Pan tool is active.");
  });
  d.getElementById("selectRectangleBtn")?.addEventListener("click",ctx.selectByRectangle);
  d.getElementById("selectPolygonBtn")?.addEventListener("click",ctx.selectByPolygon);
  d.getElementById("selectFeaturesBtn").onclick=()=>{
   ctx.stopActiveDraw(true);ctx.setActiveMapTool("select");ctx.map.dragging.enable();ctx.map.getContainer().style.cursor="";
   d.querySelectorAll("#classicToolbars button").forEach(x=>x.classList.remove("active"));
   d.getElementById("selectFeaturesBtn").classList.add("active");
   ctx.status("Select Features is active. Click an existing feature to select it; Ctrl-click or Shift-click adds/removes features.");
  };
  d.getElementById("identifyFeatureBtn").onclick=()=>{
   ctx.setActiveMapTool("identify");d.querySelectorAll("#classicToolbars button").forEach(x=>x.classList.remove("active"));
   d.getElementById("identifyFeatureBtn").classList.add("active");ctx.status("Click a feature to identify it.");
  };
  d.getElementById("fullExtentBtn").onclick=()=>{
   const layers=[ctx.drawn,...ctx.namedLayers.map(x=>x.layer)].filter(layer=>layer.getBounds&&layer.getBounds().isValid());
   if(!layers.length)return;
   const bounds=ctx.L.latLngBounds(layers.map(layer=>layer.getBounds()));ctx.map.fitBounds(bounds,{padding:[20,20]});
  };
 }
 root.SabzWebGISMapToolbars=Object.freeze({init});
})(typeof window!=="undefined"?window:globalThis);
