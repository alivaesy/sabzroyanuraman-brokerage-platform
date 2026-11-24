/* Drawing activation controller. Keeps geometry construction in the native Leaflet drawing engine. */
(function(root){
"use strict";
function create(ctx){
 const {
  blockForUnsavedTableEdits, getActiveDrawHandler, getActiveDrawPurpose,
  arcMapDialog, stopActiveDraw, stopReshapeDraw, stopVertexEditing,
  window, namedLayers, getEditTarget, setActiveDrawingSetName,
  getActiveDrawingSetName, drawingLayerForType, getEditSessionActive,
  $, isVectorLayer, selectedOverlay, startEditing, status, startNativeDraw
 }=ctx;
 return async function startDraw(type,button){

  if(typeof blockForUnsavedTableEdits==="function"&&blockForUnsavedTableEdits("starting a drawing"))return;
  // Never silently throw away an unfinished sketch when switching tools.
  if(activeDrawHandler&&activeDrawPurpose==="draw"&&typeof activeDrawHandler.hasSketchVertices==="function"&&activeDrawHandler.hasSketchVertices()){if(!await arcMapDialog.confirm({title:"Drawing in Progress",message:"The current sketch is unfinished. Discard it and switch tools?",confirmText:"Discard Sketch",danger:true}))return;}
  stopActiveDraw();
  // A new sketch must not overlap an active reshape or vertex-edit handler.
  if(window._reshapeDrawHandler)stopReshapeDraw();
  if(window._vertexEditControl)stopVertexEditing();
  // Drawing tools always route to the matching geometry child of the active set.
  const currentDrawingItem=namedLayers.find(item=>item.layer===editTarget);
  const isDrawingSetTarget=!!currentDrawingItem&&!!currentDrawingItem.drawingSet;
  if(isDrawingSetTarget)activeDrawingSetName=currentDrawingItem.drawingSet;
  const useAutomaticDrawingLayer=editTarget===drawn||isDrawingSetTarget;
  if(useAutomaticDrawingLayer){
    drawingLayerForType(type);
  }else if(!editSessionActive){
    // Respect an explicitly selected imported vector layer, like ArcMap.
    let preferredIndex=-1;
    if(selectedOverlay&&selectedOverlay!==drawn){
      preferredIndex=namedLayers.findIndex(item=>item.layer===selectedOverlay&&isVectorLayer(item.layer));
    }
    const attributeChoice=$("attributeLayer")?.value;
    if(preferredIndex<0&&attributeChoice&&attributeChoice!=="drawn"){
      const idx=Number(attributeChoice);
      if(Number.isInteger(idx)&&namedLayers[idx]&&isVectorLayer(namedLayers[idx].layer))preferredIndex=idx;
    }
    if(preferredIndex>=0){
      $("editTargetLayer").value=String(preferredIndex);
      editTarget=namedLayers[preferredIndex].layer;
    }else if(attributeChoice==="drawn"){
      $("editTargetLayer").value="drawn";
      editTarget=drawn;
      drawingLayerForType(type);
    }
  }
  if(!editSessionActive)startEditing();
  // startEditing can refuse to start when table edits are pending; never let
  // drawing continue outside a valid editor session.
  if(!editSessionActive){status("Save or discard pending table edits before drawing.");return}
  if(!editTarget||typeof editTarget.addLayer!=="function"){status("Choose a vector layer in the Editor toolbar.");return}
  startNativeDraw(type,button);

 };
}
root.SabzWebGISDrawActivation={create};
})(window);
