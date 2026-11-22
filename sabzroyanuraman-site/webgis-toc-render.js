/* ArcMap-style Table Of Contents renderer extracted behind a compatibility factory. */
(function(root){
"use strict";
function create(ctx){
 if(!ctx||typeof ctx.$!=="function")throw new Error("TOC renderer context is incomplete.");
 const {$,map,namedLayers,drawn,selectedRecords,applyDefinitionQuery,openLayerProperties,applySelectionVisual,showLayerContextMenu,scheduleAutoSave}=ctx;
 const state=ctx.state;
 function syncLayerDrawingOrder(){
  // TOC order is top-to-bottom; Leaflet paints the last SVG path on top.
  // Re-apply paths from bottom to top so map stacking matches the TOC.
  [...namedLayers].reverse().forEach(owner=>{
   const layer=owner?.layer;if(!layer)return;
   if(typeof layer.eachLayer==="function")layer.eachLayer(child=>{
    if(typeof child.bringToFront==="function")child.bringToFront();
    else if(typeof child.setZIndex==="function"&&Number.isFinite(Number(owner._tocZIndex)))child.setZIndex(owner._tocZIndex);
   });
   else if(typeof layer.bringToFront==="function")layer.bringToFront();
  });
 }
 function renderLayerList(){
  syncLayerDrawingOrder();
  const el=$("layers");if(!el)return;el.innerHTML="";
  if(state.tocViewMode==="option"){
   const panel=document.createElement("div");panel.className="toc-options-panel";panel.style.cssText="padding:8px;font-size:11px;color:#354b59";
   const heading=document.createElement("strong");heading.textContent="Table Of Contents Options";panel.appendChild(heading);
   const note=document.createElement("p");note.textContent="Manage drawing order and layer visibility. Changes are saved with the current project.";panel.appendChild(note);
   const list=document.createElement("div");list.setAttribute("role","list");list.style.cssText="display:flex;flex-direction:column;gap:4px";
   namedLayers.forEach((owner,index)=>{
    const row=document.createElement("div");row.setAttribute("role","listitem");row.style.cssText="display:flex;align-items:center;gap:5px;padding:5px;border:1px solid #c5d0d5;background:#f8fafb";
    const visibility=document.createElement("input");visibility.type="checkbox";visibility.checked=!!(owner.layer&&map.hasLayer(owner.layer));visibility.title="Toggle layer visibility";visibility.setAttribute("aria-label","Show "+(owner.name||"layer"));
    visibility.addEventListener("change",()=>{if(!owner.layer)return;if(visibility.checked)map.addLayer(owner.layer);else map.removeLayer(owner.layer);if(owner._definitionQuery)applyDefinitionQuery(owner);scheduleAutoSave();});
    const label=document.createElement("span");label.textContent=owner.name||("Layer "+(index+1));label.style.cssText="flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap";
    const source=document.createElement("span");source.textContent=owner.sourceName||owner.source||"";source.title=source.textContent;source.style.cssText="max-width:78px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;color:#71808a";
    const up=document.createElement("button");up.type="button";up.textContent="↑";up.title="Move layer up in drawing order";up.setAttribute("aria-label","Move "+label.textContent+" up");up.disabled=index===0;
    const down=document.createElement("button");down.type="button";down.textContent="↓";down.title="Move layer down in drawing order";down.setAttribute("aria-label","Move "+label.textContent+" down");down.disabled=index===namedLayers.length-1;
    [up,down].forEach(button=>button.style.cssText="width:23px;height:22px;padding:0;border:1px solid #aebbc2;background:#fff;cursor:pointer");
    up.addEventListener("click",()=>{if(index<1)return;namedLayers.splice(index,1);namedLayers.splice(index-1,0,owner);syncLayerDrawingOrder();renderLayerList();scheduleAutoSave();});
    down.addEventListener("click",()=>{if(index>=namedLayers.length-1)return;namedLayers.splice(index,1);namedLayers.splice(index+1,0,owner);syncLayerDrawingOrder();renderLayerList();scheduleAutoSave();});
    row.append(visibility,label,source,up,down);list.appendChild(row);
   });
   if(!namedLayers.length){const empty=document.createElement("p");empty.textContent="No operational layers are loaded.";list.appendChild(empty);}
   panel.appendChild(list);el.appendChild(panel);return;
  }
  const selectedOwnerLayers=new Set(selectedRecords().map(record=>record.owner?.layer).filter(Boolean));
  const groups=new Map();
const addRow=(x,label,indent)=>{

    if(state.tocViewMode==="visibility"&&x.layer&&!map.hasLayer(x.layer))return;

    if(state.tocViewMode==="selection"&&x.layer!==state.selectedOverlay&&!selectedOwnerLayers.has(x.layer))return;
    const row=document.createElement("div");row.className="layer-row"+(indent?" layer-child-row":"");
    row.style.paddingLeft=indent?"28px":"6px";
    if(x.layer&&(state.selectedOverlay===x.layer||selectedOwnerLayers.has(x.layer)))row.classList.add("layer-active");
    const cb=document.createElement("input");cb.type="checkbox";cb.checked=x.layer?map.hasLayer(x.layer):true;cb.title="Toggle Visibility";
    cb.onchange=()=>{if(x.layer){if(cb.checked)map.addLayer(x.layer);else map.removeLayer(x.layer);const owner=namedLayers.find(item=>item.layer===x.layer);if(owner?._definitionQuery)applyDefinitionQuery(owner)}else{(groups.get(x.name)||[]).forEach(item=>{if(cb.checked)map.addLayer(item.layer);else map.removeLayer(item.layer)});renderLayerList()}scheduleAutoSave()};
    const icon=document.createElement("span");icon.className="layer-icon";icon.setAttribute("aria-hidden","true");
    const nm=document.createElement("span");nm.className="layer-name";nm.textContent=label;
    const layerOwner=x.layer?namedLayers.find(item=>item.layer===x.layer):null;
    if(layerOwner){row.dataset.tocLayerIndex=String(namedLayers.indexOf(layerOwner));row.dataset.tocLayerName=layerOwner.name||label;}
    /* ArcMap-style layer reordering: only Drawing Order rows are draggable. */
    if(layerOwner&&state.tocViewMode==="drawing"){
      row.draggable=true;
      row.addEventListener("dragstart",e=>{
        e.dataTransfer.effectAllowed="move";
        e.dataTransfer.setData("text/plain",String(namedLayers.indexOf(layerOwner)));
        row.classList.add("toc-row-dragging");
      });
      row.addEventListener("dragend",()=>row.classList.remove("toc-row-dragging"));
      row.addEventListener("dragover",e=>{e.preventDefault();e.dataTransfer.dropEffect="move";});
      row.addEventListener("drop",e=>{
        e.preventDefault();e.stopPropagation();
        const from=Number(e.dataTransfer.getData("text/plain"));
        const to=namedLayers.indexOf(layerOwner);
        if(!Number.isInteger(from)||from<0||from>=namedLayers.length||to<0||from===to)return;
        const moved=namedLayers.splice(from,1)[0];
        namedLayers.splice(to,0,moved);
        syncLayerDrawingOrder();
        renderLayerList();
        scheduleAutoSave();
      });
    }
    if(layerOwner?._definitionQuery){nm.title="Definition Query: "+layerOwner._definitionQuery.field+" "+layerOwner._definitionQuery.operator+(layerOwner._definitionQuery.value===""?"":" "+layerOwner._definitionQuery.value);const badge=document.createElement("span");badge.className="small";badge.textContent=" [Q]";badge.title=nm.title;row.append(cb,icon,nm,badge);}else row.append(cb,icon,nm);
    if(x.layer)nm.ondblclick=e=>{e.preventDefault();e.stopPropagation();const owner=namedLayers.find(item=>item.layer===x.layer);if(owner)openLayerProperties(owner)};
    if(x.layer){row.onclick=e=>{if(e.target.closest("input,button"))return;state.selectedOverlay=x.layer;applySelectionVisual();renderLayerList()};row.oncontextmenu=e=>{e.preventDefault();e.stopPropagation();state.selectedOverlay=x.layer;renderLayerList();showLayerContextMenu(x,e.clientX,e.clientY)}}
    el.appendChild(row);
  };
  

  if(state.tocViewMode==="selection"&&!state.selectedOverlay){el.innerHTML='<div class="small" style="padding:10px;color:#667781">Select a layer to show it here.</div>';return;}

  if(state.tocViewMode==="source"){

    const sorted=[...namedLayers].sort((a,b)=>String(a.sourceName||a.source||a.name||"").localeCompare(String(b.sourceName||b.source||b.name||""),undefined,{numeric:true,sensitivity:"base"}));

    sorted.forEach((x,i)=>addRow(x,(x.sourceName||x.source)?String(x.sourceName||x.source)+" — "+x.name:x.name||("Layer "+(i+1)),false));

    if(drawn.getLayers().length)addRow({name:"Legacy drawings",layer:drawn,isDrawn:true},"Legacy drawings",false);

    return;

  }

  
  if(drawn.getLayers().length)addRow({name:"Legacy drawings",layer:drawn,isDrawn:true},"Legacy drawings",false);
  const renderedGroups=new Set();
  namedLayers.forEach(x=>{
    if(x.drawingSet){
      if(!renderedGroups.has(x.drawingSet)){
        const children=namedLayers.filter(item=>item.drawingSet===x.drawingSet);
        groups.set(x.drawingSet,children);
        addRow({name:x.drawingSet,isGroup:true},x.drawingSet,false);
        renderedGroups.add(x.drawingSet);
      }
      addRow(x,x.displayName||x._webgisDrawingType||x.name,true);
    }else{
      addRow(x,x.name,false);
    }
  });
}
 renderLayerList.bindViewTabs=function(){
  document.querySelectorAll("[data-toc-view]").forEach(button=>button.addEventListener("click",()=>{
   state.setTocViewMode(button.dataset.tocView||"drawing");
   document.querySelectorAll("[data-toc-view]").forEach(tab=>{const active=tab===button;tab.classList.toggle("active",active);tab.setAttribute("aria-selected",active?"true":"false")});
   renderLayerList();
  }));
 };
 return renderLayerList;
}
root.SabzWebGISTocRenderer=Object.freeze({create});
})(typeof window!=="undefined"?window:globalThis);
