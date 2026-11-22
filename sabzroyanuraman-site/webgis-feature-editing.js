(function(root){
  "use strict";
  function create({$,document,getEditSessionActive,getSelectedFeatures,getFeatureClipboard,getActiveDrawHandler,getActiveDrawPurpose}={}){
    if(typeof $!=="function"||!document||typeof getEditSessionActive!=="function"||typeof getSelectedFeatures!=="function"||typeof getFeatureClipboard!=="function")throw new Error("Feature editing UI dependencies are incomplete.");
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
    return Object.freeze({updateEditorToolbar,updateStandardActionButtons});
  }
  root.SabzWebGISFeatureEditing={create};
})(window);
