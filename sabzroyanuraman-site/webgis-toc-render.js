/* ArcMap-style Table Of Contents renderer extracted behind a compatibility factory. */
(function(root){
"use strict";
function create(ctx){
 if(!ctx||typeof ctx.$!=="function")throw new Error("TOC renderer context is incomplete.");
 const {$,map,namedLayers,drawn,selectedRecords,applyDefinitionQuery,openLayerProperties,applySelectionVisual,showLayerContextMenu,scheduleAutoSave}=ctx;
 const state=ctx.state;
 function renderLayerList(){
  const el=$("layers");if(!el)return;el.innerHTML="";
  if(state.tocViewMode==="option"){el.innerHTML='<div style="padding:12px 10px;font-size:11px;line-height:1.6;color:#354b59"><strong>Table Of Contents Options</strong><p>Manage layer visibility from Drawing Order or Visibility. Use Source to inspect layer sources and Selection to review selected layers.</p><p>Layer ordering, grouping, and display preferences will be expanded in this panel.</p><div style="padding:7px;border:1px solid #c5d0d5;background:#f4f7f8">Catalog and data-source management are available from Windows → Catalog.</div></div>';return;}
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
 return renderLayerList;
}
root.SabzWebGISTocRenderer=Object.freeze({create});
})(typeof window!=="undefined"?window:globalThis);
