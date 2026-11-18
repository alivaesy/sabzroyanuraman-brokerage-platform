/* ArcMap-style main menu and toolbar command wiring extracted from web-gis.html. */
(function(root){
function init(scope){
  with(scope){
(function(){
  const menus=[...document.querySelectorAll(".arc-menu-btn")];
  const drops=[...document.querySelectorAll(".arc-dropdown")];
  /* Menu commands must never submit an enclosing form or reload the GIS page. */
  document.querySelectorAll(".arc-dropdown button").forEach(button=>{button.type="button";});
  function closeMenus(){drops.forEach(x=>x.classList.remove("show"));menus.forEach(x=>x.classList.remove("active"))}
  document.querySelectorAll(".arc-dropdown button[data-tool], .editor-toolbar button[data-tool]").forEach(b=>b.onclick=()=>{
    const t=(typeof GIS_TOOLS!=="undefined")?GIS_TOOLS.find(x=>x.id===b.dataset.tool&&(!b.dataset.op||x.op===b.dataset.op)):null;
    if(t)runGisTool(t); closeMenus();
  });
  document.querySelectorAll(".arc-dropdown button[data-base]").forEach(b=>b.onclick=()=>{
    const key=b.dataset.base; selectBasemap(key); closeMenus();
  });
  /* Keep Share Project in the main GIS script scope so it can access the authenticated session state. */
window.__shareCurrentProject=async function(){
  try{
    if(!remoteGisUser||!remoteGisOrganization){
      status("ابتدا وارد حساب GIS شوید و سازمان را انتخاب کنید؛ سپس Share Project را بزنید.");return;
    }
    if(typeof hasUnsavedTableEdits==="function"&&hasUnsavedTableEdits()){
      status("ابتدا Save Table Edits را اجرا کنید؛ اشتراک‌گذاری انجام نشد.");return;
    }
    // Share the current map state, not a stale server snapshot from an earlier save.
    const project=serializeProject();
    project.id=projectId();project.updatedAt=new Date().toISOString();
    const saved=await remoteGisSaveProject(project);
    if(!saved||!saved.project_id)throw new Error("project_save_before_share_failed");
    currentProjectId=String(saved.project_id);
    $("projectStatus").textContent="Server saved before sharing: "+new Date().toLocaleString("en-US");
    await remoteGisGetCsrf();
    const response=await fetch("/api/gis/share.php",{method:"POST",credentials:"same-origin",headers:{"Content-Type":"application/json","Accept":"application/json","X-CSRF-Token":remoteGisCsrf},body:JSON.stringify({action:"create",id:Number(remoteGisProjectId),organization_id:Number(remoteGisOrganization.id)})});
    let result={};try{result=await response.json()}catch(_){}
    if(!response.ok||result.ok===false)throw new Error(result.error||("HTTP "+response.status));
    if(!result.url)throw new Error("The sharing API did not return a link.");
    if(navigator.clipboard&&navigator.clipboard.writeText){await navigator.clipboard.writeText(result.url);status("لینک اشتراک‌گذاری ساخته و کپی شد: "+result.url);}
    else if(typeof arcMapDialog!=="undefined"&&arcMapDialog.prompt){await arcMapDialog.prompt({title:"Shared map link",message:"Send this link to let others view the map.",label:"Link",value:result.url});}
    else{window.prompt("Shared map link",result.url);}
  }catch(error){console.error("Share Project failed:",error);status("Share Project failed: "+(error&&error.message?error.message:"unknown error"));}
};
document.querySelectorAll(".arc-dropdown button[data-action], .classic-toolbar button[data-action], #catalogPanel button[data-action]").forEach(b=>b.onclick=()=>{
    const a=b.dataset.action;
    const mapActions={
      shareProject:()=>{if(typeof window.__shareCurrentProject==="function")window.__shareCurrentProject();else status("Share Project is not ready. Reload the page and try again.");},
      layoutView:()=>{if(typeof openLayoutView==="function")openLayoutView();else status("Layout View is not available.");},
      publishLayoutExport:()=>{if(typeof openLayoutView!=="function"){status("Layout View is not available.");return;}if(!document.getElementById("webgisLayoutView"))openLayoutView();document.querySelector('#webgisLayoutView [data-layout-action="save"]')?.click();},
      publishLayoutImport:()=>{if(typeof openLayoutView!=="function"){status("Layout View is not available.");return;}if(!document.getElementById("webgisLayoutView"))openLayoutView();document.querySelector('#webgisLayoutView [data-layout-action="load"]')?.click();},
      publishLayoutPrint:()=>{if(typeof openLayoutView!=="function"){status("Layout View is not available.");return;}if(!document.getElementById("webgisLayoutView"))openLayoutView();document.querySelector('#webgisLayoutView [data-layout-action="print"]')?.click();},
      publishServices:()=>{openGisDock("display");document.getElementById("wmsUrl")?.focus();status("Service connection settings opened. This connects to an existing WMS service; publishing new WMS/WFS layers requires a configured GeoServer/API endpoint.");},
      publishHelp:()=>status("Publish is linked to Layout: export/import the layout definition as JSON and print the composed map sheet to PDF. Project/map sharing and GIS-data export are separate. Direct GeoServer/PostGIS service publishing still needs a secured server API endpoint."),

      startEditing:()=>startEditing(),
      newMap:()=>clearWorkspace(),
      printMap:()=>window.print(),
      cut:()=>$("cutBtn")?.click(),
      copy:()=>$("copyBtn")?.click(),
      paste:()=>$("pasteBtn")?.click(),
      deleteSelected:()=>$("deleteBtn")?.click(),
      undo:()=>undoMapEdit(),
      redo:()=>redoMapEdit(),
      addOnlineData:()=>openOnlineData(),
      selectElements:()=>$("selectElementsBtn")?.click(),
      hyperlink:()=>$("hyperlinkBtn")?.click(),
      htmlPopup:()=>$("htmlPopupBtn")?.click(),
      measureTool:()=>$("measureToolBtn")?.click(),
      findFeature:()=>searchFeature(),
      findRoute:()=>findRoute(),
      timeSlider:()=>startTimeSlider(),
      createViewer:()=>createMapViewer(),
      saveEdits:()=>saveEdits(),
      stopEditing:()=>stopEditing(),
      openAttributeTable:()=>{
        const candidates=[selectedOverlay,selectedLayer,currentAttributeLayer(),drawn,...namedLayers.map(x=>x.layer)];
        const layer=candidates.find(x=>x&&isVectorLayer(x));
        if(!layer){status("Select a vector feature layer before opening its attribute table.");return}
        openLayerAttributeTable({layer},"vertical");
      },
      showToolbox:()=>{openGisDock("analysis");document.getElementById("gisToolSearch")?.focus()},
      showCatalog:()=>openCatalogDock(),
      editorOptions:()=>openGisDock("edit"),
      topologyOptions:()=>openTopologyDialog("options"),
      topologyValidate:()=>{let target=topologyLayerValue(editTarget),boundary=topologyLayerValue(selectedLayer||selectedOverlay||drawn);try{target=localStorage.getItem("sabzRoyWebGisTopologyTarget")||target;boundary=localStorage.getItem("sabzRoyWebGisTopologyBoundary")||boundary}catch(_){}checkEditTopology(true,topologyLayerFromValue(target),topologyLayerFromValue(boundary))},
      topologyInspector:()=>openTopologyDialog("inspector"),
      toggleEditorToolbar:()=>document.getElementById("editorToolbar")?.classList.toggle("show"),
      openToolSearch:()=>{const d=document.getElementById("searchDock");d?.classList.add("open");document.getElementById("topToolSearch")?.focus()},
      zoomIn:()=>map.zoomIn(),
      goToXY:()=>goToXY(),
      identify:()=>document.getElementById("identifyFeatureBtn")?.click(),
      zoomOut:()=>map.zoomOut(),
      pan:()=>document.getElementById("panMapBtn")?.click(),
      about:()=>gisNotify("ArcMap-style Web GIS workspace"),
      shortcuts:()=>gisNotify("Drawing: Enter or double-click finishes a line or polygon; Backspace/Delete removes the last sketch vertex; Escape cancels the active tool. Selection: Ctrl-click or Shift-click adds/removes features. Editing: use Undo/Redo in the Edit menu to reverse/restore recorded map edits."),
      importData:()=>openDataFormatDialog("import"),
      importArcMap:()=>document.getElementById("mxdInput")?.click(),
      importGdbZip:()=>document.getElementById("gdbZipInput")?.click(),
      importGdbFolder:()=>document.getElementById("gdbInput")?.click(),
      exportData:()=>openDataFormatDialog("export"),
      geodatabaseManager:()=>openGeodatabaseManager(),
      openProjectManager:()=>openProjectManager(),
      saveProject:()=>document.getElementById("saveProject")?.click(),
      loadProject:()=>document.getElementById("loadProject")?.click(),
      downloadProject:()=>document.getElementById("downloadProject")?.click(),
      fitDraw:()=>document.getElementById("fitDraw")?.click(),
      clearDraw:()=>document.getElementById("clearDraw")?.click(),
      showLayers:()=>{const panel=document.getElementById("panel"),layers=document.getElementById("layers");if(panel)panel.scrollIntoView({block:"nearest"});if(layers){layers.tabIndex=-1;layers.focus({preventScroll:true});}renderLayerList();status("Table Of Contents focused.");},
      wms:()=>{openGisDock("display");document.getElementById("wmsUrl")?.focus()},
      configureWmsBasemap:()=>{openGisDock("display");document.getElementById("wmsUrl")?.focus();status("Enter a WMS URL and layer name, then choose Use WMS as Basemap.")},
      buffer:()=>{document.getElementById("gisOperation").value="buffer";document.getElementById("runGisOperation").click()},
      clip:()=>{document.getElementById("gisOperation").value="clip";document.getElementById("runGisOperation").click()},
      intersection:()=>{document.getElementById("gisOperation").value="intersection";document.getElementById("runGisOperation").click()},
      merge:()=>{document.getElementById("gisOperation").value="merge";document.getElementById("runGisOperation").click()},
      measure:()=>document.getElementById("measureSelected")?.click(),
      searchFeature:()=>searchFeature(),
      addBookmark:()=>addBookmark(),
      showBookmarks:()=>showBookmarks(),
      crs:()=>{openGisDock("project");document.getElementById("crsPreset")?.focus()},
      attachments:()=>attachmentsTool(),
      display:()=>openGisDock("display"),
      insertTitle:()=>addMapDecoration("title"),
      insertLegend:()=>addMapDecoration("legend"),
      insertNorthArrow:()=>addMapDecoration("north"),
      insertScaleBar:()=>addMapDecoration("scale"),
      insertDataFrame:()=>addMapDecoration("frame")
    };
    try{
      if(mapActions[a])mapActions[a]();
      else status("This menu command is not wired yet: "+a);
    }catch(err){
      console.error("WebGIS menu command failed:",a,err);
      status("Menu command \""+a+"\" failed: "+(err&&err.message?err.message:"unknown error"));
    }finally{closeMenus();}
  });
  const si=document.getElementById("topToolSearch"), out=document.getElementById("topToolResults");
  function search(q){
    if(!q){out.classList.remove("show");out.innerHTML="";return}
    const arr=(typeof GIS_TOOLS!=="undefined"?GIS_TOOLS:[]).filter(t=>(t.title+" "+t.category+" "+t.keys).toLowerCase().includes(q.toLowerCase())).slice(0,20);
    out.innerHTML=arr.length?arr.map(t=>'<button class="top-tool-item" data-id="'+t.id+'" data-op="'+(t.op||"")+'">'+esc(t.title)+" — "+esc(t.category)+"</button>").join(""):'<div class="top-tool-item">No matching tools found.</div>';
    out.classList.add("show");
    out.querySelectorAll("[data-id]").forEach(b=>b.onclick=()=>{const t=GIS_TOOLS.find(x=>x.id===b.dataset.id&&(!b.dataset.op||x.op===b.dataset.op));if(t)runGisTool(t);out.classList.remove("show");si.value=""});
  }
  si.oninput=e=>search(e.target.value);
})();
  }
}
root.SabzWebGISMenuWiring=Object.freeze({init});
})(typeof window!=="undefined"?window:globalThis);
