/* FileGDB Catalog UI: explicit feature-class selection and a separate non-spatial Tables view. */
(function(){
 "use strict";
 const state={tables:[]};
 const esc=value=>String(value??"").replace(/[&<>'"]/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;","'":"&#39;",'"':"&quot;"}[c]));
 function ensureStyles(){
  if(document.getElementById("sabzGdbCatalogStyles"))return;
  const style=document.createElement("style");style.id="sabzGdbCatalogStyles";
  style.textContent="#sabzGdbPicker,#sabzGdbTableView{position:fixed;inset:0;z-index:12000;background:#172b36aa;display:none;align-items:center;justify-content:center;padding:18px}#sabzGdbPicker.show,#sabzGdbTableView.show{display:flex}.sabz-gdb-card{width:min(720px,96vw);max-height:88vh;overflow:auto;background:#f5f7f8;border:1px solid #8798a1;box-shadow:0 8px 30px #0005;color:#243d4b;padding:14px}.sabz-gdb-card h3{margin:0 0 8px;font-size:15px}.sabz-gdb-list{max-height:55vh;overflow:auto;border:1px solid #bcc8ce;background:#fff}.sabz-gdb-entry{display:flex;gap:8px;align-items:center;padding:7px 9px;border-bottom:1px solid #e2e8eb;font-size:11px}.sabz-gdb-entry small{margin-left:auto;color:#6b7f89}.sabz-gdb-actions{display:flex;gap:6px;flex-wrap:wrap;margin-top:10px}.sabz-gdb-actions button{border:1px solid #9eafb7;background:#fff;padding:6px 10px;cursor:pointer}.sabz-gdb-actions button.primary{background:#176b4b;color:#fff;border-color:#176b4b}.sabz-gdb-table{width:100%;border-collapse:collapse;font-size:10px}.sabz-gdb-table th,.sabz-gdb-table td{border:1px solid #cbd5da;padding:5px;text-align:left;white-space:nowrap}.sabz-gdb-table th{position:sticky;top:0;background:#e7edf0}";
  document.head.appendChild(style);
 }
 async function importGdbZip(file){
  if(!window.JSZip)throw new Error("JSZip is unavailable.");
  const zip=await JSZip.loadAsync(await file.arrayBuffer());
  const entries=Object.values(zip.files).filter(entry=>!entry.dir);
  const root=entries.map(entry=>entry.name.split("/")[0]).find(name=>/\.gdb$/i.test(name));
  if(!root)throw new Error("ZIP must contain a complete folder ending in .gdb.");
  const files=[];
  for(const entry of entries){
   if(!entry.name.startsWith(root+"/"))continue;
   const bytes=await entry.async("uint8array");
   const part=new File([bytes],entry.name.split("/").pop(),{lastModified:Date.now()});
   Object.defineProperty(part,"webkitRelativePath",{value:entry.name});
   files.push(part);
  }
  if(!files.length)throw new Error("No files were found inside the .gdb folder.");
  const b=window.__sabzWebGISSharedBridge;
  if(typeof b?.importGDBFiles!=="function")throw new Error("The WebGIS FileGDB importer is unavailable.");
  await b.importGDBFiles(files);
 }
 function buildUi(){
  ensureStyles();
  if(document.getElementById("sabzGdbPicker"))return;
  const picker=document.createElement("div");picker.id="sabzGdbPicker";picker.innerHTML='<section class="sabz-gdb-card" role="dialog" aria-modal="true"><h3>File Geodatabase — Select Feature Classes</h3><p id="sabzGdbPickerInfo" class="small"></p><div class="sabz-gdb-actions"><button type="button" id="sabzGdbSelectAll">Select all</button><button type="button" id="sabzGdbSelectNone">Select none</button></div><div id="sabzGdbLayerList" class="sabz-gdb-list"></div><div class="sabz-gdb-actions"><button type="button" id="sabzGdbCancel">Cancel</button><button type="button" id="sabzGdbImport" class="primary">Import selected</button></div></section>';
  const viewer=document.createElement("div");viewer.id="sabzGdbTableView";viewer.innerHTML='<section class="sabz-gdb-card" role="dialog" aria-modal="true"><h3 id="sabzGdbTableTitle">Geodatabase Tables</h3><div id="sabzGdbTableContent" style="overflow:auto;max-height:68vh"></div><div class="sabz-gdb-actions"><button type="button" id="sabzGdbTableClose">Close</button></div></section>';
  document.body.append(picker,viewer);
  document.getElementById("sabzGdbSelectAll").onclick=()=>picker.querySelectorAll('input[type="checkbox"]').forEach(x=>x.checked=true);
  document.getElementById("sabzGdbSelectNone").onclick=()=>picker.querySelectorAll('input[type="checkbox"]').forEach(x=>x.checked=false);
  document.getElementById("sabzGdbTableClose").onclick=()=>viewer.classList.remove("show");
 }
 function chooseLayers(layers,workspace){
  buildUi();
  const picker=document.getElementById("sabzGdbPicker"),list=document.getElementById("sabzGdbLayerList");
  document.getElementById("sabzGdbPickerInfo").textContent=workspace+": "+layers.length+" discovered source layers. Choose which spatial feature classes to add to the map.";
  list.innerHTML=layers.map((item,index)=>{
   const name=String(item.name||"Layer "+(index+1));
   const group=item.featureDataset||"Root";
   const geometry=item.geometryType||"geometry not reported";
   return '<label class="sabz-gdb-entry"><input type="checkbox" checked data-layer-index="'+index+'"><span>'+esc(name)+'</span><small>'+esc(group)+' · '+esc(geometry)+'</small></label>';
  }).join("");
  picker.classList.add("show");
  return new Promise(resolve=>{
   let settled=false;
   const finish=value=>{if(settled)return;settled=true;picker.classList.remove("show");resolve(value);};
   document.getElementById("sabzGdbCancel").onclick=()=>finish([]);
   document.getElementById("sabzGdbImport").onclick=()=>{
    const selected=[...list.querySelectorAll('input[type="checkbox"]:checked')].map(x=>layers[Number(x.dataset.layerIndex)]?.name).filter(Boolean);
    finish(selected);
   };
  });
 }
 function registerTables(tables){
  state.tables=Array.isArray(tables)?tables:[];
  const panel=document.getElementById("layers")?.parentElement;
  if(!panel)return;
  let section=document.getElementById("gdbTablesCatalog");
  if(!section){section=document.createElement("section");section.id="gdbTablesCatalog";section.className="section";panel.appendChild(section);}
  section.innerHTML='<h3>Tables ('+state.tables.length+')</h3>'+
   (state.tables.length?state.tables.map((table,index)=>'<div class="layer-row"><span class="layer-icon">▤</span><span class="layer-name">'+esc(table.name)+'</span><button type="button" data-gdb-table="'+index+'">Open</button><span class="small">'+Number(table.rows?.length||0)+' rows</span></div>').join(""):'<div class="small">No non-spatial user tables were discovered. Internal GDB catalog tables are hidden.</div>');
  section.querySelectorAll("[data-gdb-table]").forEach(button=>button.addEventListener("click",()=>openTable(Number(button.dataset.gdbTable))));
 }
 function openTable(index){
  const table=state.tables[index];if(!table)return;
  buildUi();
  document.getElementById("sabzGdbTableTitle").textContent="Table: "+table.name;
  const rows=Array.isArray(table.rows)?table.rows:[];
  const fields=[...new Set([...(table.fields||[]),...rows.flatMap(row=>Object.keys(row||{}))])];
  const html=fields.length?'<table class="sabz-gdb-table"><thead><tr>'+fields.map(field=>'<th>'+esc(field)+'</th>').join("")+'</tr></thead><tbody>'+
   (rows.length?rows.map(row=>'<tr>'+fields.map(field=>'<td>'+esc(row?.[field])+'</td>').join("")+'</tr>').join(""):'<tr><td colspan="'+fields.length+'">No records</td></tr>')+
   '</tbody></table>':'<p class="small">No fields were returned by the source reader.</p>';
  document.getElementById("sabzGdbTableContent").innerHTML=html;
  document.getElementById("sabzGdbTableView").classList.add("show");
 }
 function installImportControl(){
  const layers=document.getElementById("layers"),target=layers?.parentElement||document.getElementById("panel");
  if(!target||document.getElementById("gdbImportControl"))return;
  const section=document.createElement("section");section.id="gdbImportControl";section.className="section";
  section.innerHTML='<h3>File Geodatabase</h3><div class="row"><button type="button" class="action secondary" id="gdbArchiveButton">Import .gdb ZIP</button></div><input id="gdbArchiveInput" type="file" accept=".zip" hidden><div class="small">Select a ZIP containing the complete .gdb folder. Folder import remains available in Add Data.</div>';
  target.insertBefore(section,layers);
  document.getElementById("gdbArchiveButton").onclick=()=>document.getElementById("gdbArchiveInput").click();
  document.getElementById("gdbArchiveInput").onchange=async event=>{
   const file=event.target.files?.[0];if(!file)return;
   try{await importGdbZip(file);}catch(error){window.__sabzWebGISSharedBridge?.status?.("FileGDB ZIP import failed: "+(error.message||error));}
   finally{event.target.value="";}
  };
 }
 window.SabzGdbCatalog={chooseLayers,registerTables,openTable,importGdbZip,installImportControl,get tables(){return state.tables.slice();}};
 if(document.readyState==="loading")document.addEventListener("DOMContentLoaded",installImportControl,{once:true});else installImportControl();
})();