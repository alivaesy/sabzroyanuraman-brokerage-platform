/* ArcMap-style dockable Attribute Table window extracted behind legacy delegates. */
(function(root){
"use strict";
function create(ctx){
 if(!ctx||typeof ctx.$!=="function")throw new Error("Attribute Table window context is incomplete.");
 function ensureAttributeTableWindow(){
  const wrap=ctx.$("attributeTableWrap");if(!wrap)return null;
  if(ctx.attributeTableWindow)return ctx.attributeTableWindow;
  ctx.attributeTableHome={parent:wrap.parentElement,next:wrap.nextSibling};
  const win=root.document.createElement("section");win.id="attributeTableWindow";win.setAttribute("aria-label","Attribute Table");
  win.innerHTML='<div id="attributeTableWindowTitlebar"><span>▦</span><span id="attributeTableWindowTitle">Attribute Table</span><div id="attributeTableWindowActions"><button type="button" title="Minimize table">_</button><button type="button" title="Maximize/restore table">□</button><button type="button" title="Close table">×</button></div></div><div id="attributeTableWindowToolbar"><span class="table-layer-label" id="attributeTableWindowLayer">Layer</span><details class="attribute-window-menu" aria-hidden="true" style="display:none"><summary>Table Menu ▾</summary><div class="attribute-window-menu-items"><button type="button" data-table-action="layout">Dock / Horizontal</button><button type="button" data-table-action="start-edit">Start Editing</button><button type="button" data-table-action="save">Save Cell Edits</button><button type="button" data-table-action="add">Add Field</button><button type="button" data-table-action="delete-field">Delete Field</button><button type="button" data-table-action="delete">Delete Selected Rows</button><button type="button" data-table-action="copy">Copy Rows</button><button type="button" data-table-action="paste">Paste Rows</button><button type="button" data-table-action="calculate">Calculate Field</button><button type="button" data-table-action="refresh">Refresh</button></div></details><span class="table-count" id="attributeTableWindowCount">0 records</span></div><div id="attributeTableViewport"></div><div id="attributeTableWindowFooter"><span id="attributeTableWindowStatus">Ready</span></div>';
  root.document.body.appendChild(win);ctx.attributeTableWindow=win;ctx.attributeTableViewport=win.querySelector("#attributeTableViewport");ctx.attributeTableViewport.appendChild(wrap);
  const buttons=win.querySelectorAll("#attributeTableWindowActions button");
  buttons[0].onclick=()=>win.classList.remove("open");
  buttons[1].onclick=()=>win.classList.toggle("maximized");
  buttons[2].onclick=()=>{win.classList.remove("open","maximized");if(ctx.attributeTableHome?.parent){ctx.attributeTableHome.parent.insertBefore(wrap,ctx.attributeTableHome.next&&ctx.attributeTableHome.next.parentNode===ctx.attributeTableHome.parent?ctx.attributeTableHome.next:null);wrap.style.display="none";}};
  win.querySelector('[data-table-action="layout"]').onclick=()=>{
   ctx.attributeTableLayoutMode=ctx.attributeTableLayoutMode==="vertical"?"horizontal":"vertical";
   win.classList.toggle("vertical-table-mode",ctx.attributeTableLayoutMode==="vertical");win.classList.remove("maximized");
   const toggle=win.querySelector('[data-table-action="layout"]');
   toggle.textContent=ctx.attributeTableLayoutMode==="vertical"?"Docked Table":"Horizontal Table";
   toggle.title=ctx.attributeTableLayoutMode==="vertical"?"Switch to the wide horizontal table":"Switch to the narrow docked table";
  };
  win.querySelector('[data-table-action="start-edit"]').onclick=()=>{const layer=ctx.currentAttributeLayer();if(!layer){ctx.status("Select a feature layer first.");return;}ctx.startEditing({layer});};
  win.querySelector('[data-table-action="save"]').onclick=()=>ctx.$("saveTableEdits").click();
  win.querySelector('[data-table-action="add"]').onclick=()=>{const button=ctx.$("attributeAddField");if(button)button.click();else ctx.status("Add Field is unavailable until the table is rendered.");};
  win.querySelector('[data-table-action="delete-field"]').onclick=()=>{const button=ctx.$("attributeDeleteField");if(button)button.click();else ctx.status("Delete Field is unavailable until the table is rendered.");};
  win.querySelector('[data-table-action="delete"]').onclick=()=>ctx.$("deleteSelectedRows").click();
  win.querySelector('[data-table-action="copy"]').onclick=()=>ctx.$("copySelectedRows").click();
  win.querySelector('[data-table-action="paste"]').onclick=()=>ctx.$("pasteRows").click();
  win.querySelector('[data-table-action="calculate"]').onclick=()=>ctx.$("fillSelected").click();
  win.querySelector('[data-table-action="refresh"]').onclick=()=>{if(!ctx.hasUnsavedTableEdits())ctx.renderEditableTable();else ctx.status("Save table edits before refreshing.");};
  win.querySelector("#attributeTableWindowTitlebar").ondblclick=()=>win.classList.toggle("maximized");
  let drag=null;const bar=win.querySelector("#attributeTableWindowTitlebar");
  bar.addEventListener("pointerdown",e=>{if(e.target.closest("button")||win.classList.contains("maximized"))return;drag={x:e.clientX,y:e.clientY,left:win.offsetLeft,top:win.offsetTop};});
  bar.addEventListener("pointermove",e=>{if(!drag)return;win.style.left=Math.max(0,Math.min(root.innerWidth-win.offsetWidth,drag.left+e.clientX-drag.x))+"px";win.style.top=Math.max(48,Math.min(root.innerHeight-100,drag.top+e.clientY-drag.y))+"px";win.style.bottom="auto";});
  bar.addEventListener("pointerup",()=>drag=null);bar.addEventListener("pointercancel",()=>drag=null);
  return win;
 }
 function showAttributeTableWindow(){
  const win=ensureAttributeTableWindow();if(!win)return;
  const wrap=ctx.$("attributeTableWrap");
  if(wrap&&ctx.attributeTableViewport&&wrap.parentElement!==ctx.attributeTableViewport)ctx.attributeTableViewport.appendChild(wrap);
  win.classList.toggle("vertical-table-mode",ctx.attributeTableLayoutMode==="vertical");win.classList.remove("maximized");
  const layoutButton=win.querySelector('[data-table-action="layout"]');
  if(layoutButton){layoutButton.textContent=ctx.attributeTableLayoutMode==="vertical"?"Docked Table":"Horizontal Table";layoutButton.title=ctx.attributeTableLayoutMode==="vertical"?"Switch to the wide horizontal table":"Switch to the narrow docked table";}
  win.classList.add("open");
  const label=ctx.$("attributeLayer")?.selectedOptions?.[0]?.textContent||"Attribute Table";
  win.querySelector("#attributeTableWindowTitle").textContent="Table — "+label;
  win.querySelector("#attributeTableWindowLayer").textContent="";
 }
 function updateAttributeTableWindowInfo(){
  if(!ctx.attributeTableWindow)return;
  ctx.attributeTableWindow.querySelector("#attributeTableWindowCount").textContent=ctx.attributeTableWindow.querySelectorAll("#attributeTableWrap tbody tr").length+" records";
  ctx.attributeTableWindow.querySelector("#attributeTableWindowStatus").textContent=ctx.hasUnsavedTableEdits()?"Edits pending — Save Table Edits":"Ready";
 }
 return Object.freeze({ensureAttributeTableWindow,showAttributeTableWindow,updateAttributeTableWindowInfo});
}
root.SabzAttributeTableWindow=Object.freeze({create});
})(window);
