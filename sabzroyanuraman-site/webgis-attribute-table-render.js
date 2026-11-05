/* ArcMap-style Attribute Table renderer extracted behind a compatibility delegate. */
(function(root){
"use strict";
function render(ctx){
 if(!ctx||typeof ctx.$!=="function")throw new Error("Attribute Table render context is incomplete.");
 function renderEditableTable(){
 if(!ctx.currentAttributeLayer()){ctx.status("Select a feature layer.");return}
 const tableWrap=ctx.$("attributeTableWrap");
 if(tableWrap)tableWrap.style.display="block";
  ctx.showAttributeTableWindow();
 const items=ctx.currentAttributeItems(),filter=ctx.$("attributeFilter").value.trim().toLowerCase();
 let rows=items.map((x,i)=>({x,i})).filter(o=>!filter||Object.entries(o.x.feature.properties||{}).some(([k,v])=>!ctx.isReservedAttributeField(k)&&(k.toLowerCase().includes(filter)||String(v===null||v===undefined?"":typeof v==="object"?JSON.stringify(v):v).toLowerCase().includes(filter))));
 const keys=ctx.tableKeys(items);
 if(ctx.attributeSort.key&&keys.includes(ctx.attributeSort.key)){
  const key=ctx.attributeSort.key,dir=ctx.attributeSort.direction;
  rows.sort((a,b)=>{
   const av=a.x.feature.properties?.[key],bv=b.x.feature.properties?.[key];
   const cmp=ctx.attributeTableCore.compareValues(av,bv);
   return cmp===0?a.i-b.i:cmp*dir;
  });
 }
 let html="<div class='attribute-schema-toolbar'><details class='attribute-grid-menu'><summary>Table Options ▾</summary><div class='attribute-grid-menu-items'><button type='button' id='attributeAddField' title='Add a field to the feature class'>＋ Add Field</button><button type='button' id='attributeDeleteField' title='Delete a field from the feature class'>− Delete Field</button><button type='button' id='attributeSwitchSelection' title='Invert selection for displayed records'>Switch Selection</button><button type='button' id='attributeSelectByQuery' title='Select records using attribute criteria'>Select By Attributes</button><button type='button' id='attributeZoomSelected' title='Zoom the map to selected records'>Zoom To Selected</button><button type='button' id='attributeClearSelection' title='Clear the current map and table selection'>Clear Selection</button><button type='button' id='attributeExportCsv' title='Export selected records, or all currently displayed records, as CSV'>Export CSV</button><div class='attribute-menu-separator'></div><button type='button' data-forward-table-action='layout'>Dock / Horizontal</button><button type='button' data-forward-table-action='start-edit'>Start Editing</button><button type='button' data-forward-table-action='save'>Save Cell Edits</button><button type='button' data-forward-table-action='delete'>Delete Selected Rows</button><button type='button' data-forward-table-action='copy'>Copy Rows</button><button type='button' data-forward-table-action='paste'>Paste Rows</button><button type='button' data-forward-table-action='calculate'>Calculate Field</button><button type='button' data-forward-table-action='refresh'>Refresh</button></div></details><span id='attributeTableSummary'>"+keys.length+" field(s) · "+items.length+" record(s) · "+ctx.selectedFeatures.size+" selected</span></div><table class='arc-attribute-grid'><thead><tr><th><input type='checkbox' class='attr-check' id='checkAllRows'></th><th>#</th>"+
 keys.map(k=>"<th data-sort-key='"+ctx.esc(k)+"' title='Sort by "+ctx.esc(k)+"' style='padding:3px;border-bottom:1px solid #ccd9df;white-space:nowrap;cursor:pointer;user-select:none'>"+ctx.esc(k)+(ctx.attributeSort.key===k?(ctx.attributeSort.direction===1?" ▲":" ▼"):" ↕")+"</th>").join("")+"</tr></thead><tbody>";
 rows.forEach(o=>{html+="<tr data-index='"+o.i+"' tabindex='0' role='row' aria-selected='"+(ctx.selectedFeatures.has(o.x.layer)?"true":"false")+"' class='"+(ctx.selectedFeatures.has(o.x.layer)?"attribute-row-selected":"")+"'><td><input type='checkbox' class='row-check attr-check' "+(ctx.selectedFeatures.has(o.x.layer)?"checked":"")+"></td><td>"+(o.i+1)+"</td>"+
 keys.map(k=>{const v=(o.x.feature.properties||{})[k],shown=v===null||v===undefined?"":typeof v==="object"?JSON.stringify(v):String(v),computed=ctx.isComputedAttributeField(k);return "<td style='padding:2px'><input class='cell-edit"+(computed?" computed-field-readonly":"")+"' data-key='"+ctx.esc(k)+"' data-original='"+ctx.esc(shown)+"' value='"+ctx.esc(shown)+"' "+(computed?"readonly aria-readonly='true' title='Calculated field — read only'":"")+"></td>"}).join("")+"</tr>"});
 ctx.$("attributeTableWrap").innerHTML=html+"</tbody></table>";ctx.$("attributeTableWrap").style.display="block";
 // Resizable field columns, similar to the desktop ArcMap attribute grid.
 ctx.$("attributeTableWrap").querySelectorAll("th[data-sort-key]").forEach(th=>{
  th.style.position="sticky";th.style.minWidth="90px";
  const grip=document.createElement("span");
  grip.setAttribute("role","separator");grip.setAttribute("aria-label","Resize column "+th.dataset.sortKey);
  grip.title="Drag to resize column";grip.style.cssText="position:absolute;right:0;top:0;width:5px;height:100%;cursor:col-resize;user-select:none;touch-action:none;z-index:6";
  grip.addEventListener("pointerdown",event=>{
   if(event.button!==0)return;
   event.preventDefault();event.stopPropagation();
   const startX=event.clientX,startWidth=th.getBoundingClientRect().width;
   const move=e=>{const width=Math.max(70,Math.min(600,startWidth+e.clientX-startX));th.style.width=width+"px";th.style.minWidth=width+"px";ctx.$("attributeTableWrap").querySelectorAll("input.cell-edit[data-key]").forEach(input=>{if(input.dataset.key===th.dataset.sortKey)input.style.width=Math.max(60,width-12)+"px";});};
   const stop=()=>{document.removeEventListener("pointermove",move);document.removeEventListener("pointerup",stop);document.removeEventListener("pointercancel",stop);};
   document.addEventListener("pointermove",move);document.addEventListener("pointerup",stop);document.addEventListener("pointercancel",stop);
  });
  th.appendChild(grip);
 });
 const queryBtn=ctx.$("attributeSelectByQuery");if(queryBtn)queryBtn.onclick=()=>ctx.selectByAttribute();
 ctx.$("attributeTableWrap").querySelectorAll("[data-forward-table-action]").forEach(button=>{button.onclick=()=>{const action=button.dataset.forwardTableAction;const target=ctx.attributeTableWindow?.querySelector(`[data-table-action="${action}"]`);if(target)target.click();};});
 const zoomBtn=ctx.$("attributeZoomSelected");if(zoomBtn)zoomBtn.onclick=()=>ctx.zoomToSelection();
 const clearBtn=ctx.$("attributeClearSelection");if(clearBtn)clearBtn.onclick=()=>{
  items.forEach(item=>ctx.selectedFeatures.delete(item.layer));
  const first=ctx.getFeatureRecords().find(r=>ctx.selectedFeatures.has(r.layer));
  ctx.selectedLayer=first?.layer||null;ctx.selectedOverlay=null;
  if(ctx.selectedLayer)ctx.renderAttributeEditor();else{const editor=ctx.$("attributeEditor");if(editor)editor.style.display="none";}
  ctx.applySelectionVisual();ctx.status("Selection cleared for this attribute table layer.");
 };
 const addFieldBtn=ctx.$("attributeAddField");if(addFieldBtn)addFieldBtn.onclick=async()=>{
  const layer=ctx.currentAttributeLayer();if(!layer)return;
  if(ctx.hasUnsavedTableEdits()){ctx.status("Save Table Edits before adding a field.");return}
  if(!ctx.editSessionActive||!ctx.isLayerInEditTarget(layer)){
   if(ctx.editSessionActive){
    await ctx.stopEditing();
    if(ctx.editSessionActive){ctx.status("The current edit session could not be closed. Save edits before adding a field to another feature class.");return}
   }
   const targetSelect=ctx.$("editTargetLayer");
   if(targetSelect){const idx=ctx.namedLayers.findIndex(x=>x.layer===layer);targetSelect.value=layer===ctx.drawn?"drawn":String(idx)}
   ctx.startEditing({layer});
   if(!ctx.editSessionActive||!ctx.isLayerInEditTarget(layer)){ctx.status("Could not start editing this feature class.");return}
  }
  const name=await ctx.arcMapDialog.prompt({title:"Add Field",message:"Enter a new field name (letters, numbers, underscore):",label:"Field name",value:"NEW_FIELD"});if(name===null)return;
  const field=name.trim();if(!/^[A-Za-z][A-Za-z0-9_]{0,63}$/.test(field)){ctx.status("Field names must start with a letter and contain only letters, numbers, or underscores (max 64 characters).");return;}
  if(keys.some(k=>k.toLowerCase()===field.toLowerCase())){ctx.status("A field with that name already exists.");return;}
  const type=await ctx.arcMapDialog.select({title:"Add Field",message:"Choose the field data type:",label:"Field type",value:"TEXT",options:[{value:"TEXT",label:"TEXT — Text"},{value:"INTEGER",label:"INTEGER — Whole number"},{value:"REAL",label:"REAL — Decimal number"},{value:"DATE",label:"DATE — Date"}]});if(type===null)return;
  const fieldType=type.trim().toUpperCase();if(!["TEXT","INTEGER","REAL","DATE"].includes(fieldType)){ctx.status("Choose TEXT, INTEGER, REAL, or DATE.");return;}
  if(ctx.isComputedAttributeField(field)){ctx.status("Area_m2, Length_m, X and Y are reserved calculated field names.");return;}
  ctx.recordUndoState();const meta=layer._gdbMetadata||(layer._gdbMetadata={fields:[]});meta.fields=meta.fields||[];meta.fields.push({name:field,type:fieldType});
  items.forEach(item=>{const props=ctx.ensureFeatureProperties(item.layer);if(!(field in props))props[field]=null;});
  const synced=await ctx.flushGeodatabaseEdits();const projectSaved=await ctx.scheduleAutoSave();renderEditableTable();ctx.status("Field "+field+" ("+fieldType+") added to the feature class."+(synced&&projectSaved?"":" Local save failed; retry Save Edits."));
 };
 const deleteFieldBtn=ctx.$("attributeDeleteField");if(deleteFieldBtn)deleteFieldBtn.onclick=async()=>{
  const layer=ctx.currentAttributeLayer();if(!layer)return;
  if(ctx.hasUnsavedTableEdits()){ctx.status("Save or discard pending cell edits before deleting a field.");return}
  if(!ctx.editSessionActive||!ctx.isLayerInEditTarget(layer)){ctx.status("Start Editing this feature class before deleting fields.");return}
  const deletableFields=keys.filter(k=>!ctx.isProtectedAttributeValueField(k));
  if(!deletableFields.length){ctx.status("There are no deletable fields. Internal fields are protected.");return}
  const field=await ctx.arcMapDialog.select({title:"Delete Field",message:"Select the field to permanently delete from this feature class:",label:"Field",value:deletableFields[0],options:deletableFields.map(name=>({value:name,label:name}))});if(field===null)return;
  const name=field.trim(),actual=deletableFields.find(k=>k.toLowerCase()===name.toLowerCase());
  if(!actual){ctx.status("Field not found or protected: "+name);return}
  if(!await ctx.arcMapDialog.confirm({title:"Delete Field",message:"Permanently remove field '"+actual+"' from all features in this edit session?",confirmText:"Delete Field",danger:true}))return;
  ctx.recordUndoState();
  items.forEach(item=>{if(item.feature.properties)delete item.feature.properties[actual];ctx.updateFeaturePopup(item.layer)});
  if(layer._gdbMetadata?.fields)layer._gdbMetadata.fields=layer._gdbMetadata.fields.filter(f=>(typeof f==="string"?f:f.name)!==actual);
  if(ctx.attributeSort.key===actual)ctx.attributeSort={key:null,direction:1};
  const synced=await ctx.flushGeodatabaseEdits();const projectSaved=await ctx.scheduleAutoSave();renderEditableTable();ctx.status("Field "+actual+" deleted."+(synced&&projectSaved?"":" Local save failed; retry Save Edits."));
 };
 const switchSelectionBtn=ctx.$("attributeSwitchSelection");if(switchSelectionBtn)switchSelectionBtn.onclick=()=>{
  if(ctx.hasUnsavedTableEdits()){ctx.status("Save Table Edits before changing selection.");return}
  const visibleRows=[...ctx.$("attributeTableWrap").querySelectorAll("tbody tr[data-index]")];
  if(!visibleRows.length){ctx.status("There are no displayed records to switch.");return}
  visibleRows.forEach(row=>{const item=items[Number(row.dataset.index)];if(item){if(ctx.selectedFeatures.has(item.layer))ctx.selectedFeatures.delete(item.layer);else ctx.selectedFeatures.add(item.layer)}});
  const first=ctx.getFeatureRecords().find(item=>ctx.selectedFeatures.has(item.layer));
  ctx.selectedLayer=first?.layer||null;ctx.selectedOverlay=null;
  ctx.applySelectionVisual();ctx.updateMultiSelectionUI();renderEditableTable();
  ctx.status("Selection switched for "+visibleRows.length+" displayed record(s).");
 };
 const exportCsvBtn=ctx.$("attributeExportCsv");
 if(exportCsvBtn)exportCsvBtn.onclick=()=>{
  if(ctx.hasUnsavedTableEdits()){ctx.status("Save Table Edits before exporting; unsaved cell values have been preserved.");return}
  const visible=[...ctx.$("attributeTableWrap").querySelectorAll("tbody tr[data-index]")];
  const selected=visible.filter(row=>{const item=items[Number(row.dataset.index)];return item&&ctx.selectedFeatures.has(item.layer)});
  const sourceRows=selected.length?selected:visible;
  const csvCell=value=>{const text=String(value===null||value===undefined?"":typeof value==="object"?JSON.stringify(value):value);return '"'+text.replace(/"/g,'""')+'"';};
  const lines=[keys.map(csvCell).join(",")];
  sourceRows.forEach(row=>{const item=items[Number(row.dataset.index)];if(item)lines.push(keys.map(key=>csvCell(item.feature.properties?.[key])).join(","));});
  const blob=new Blob(["\uFEFF"+lines.join("\r\n")],{type:"text/csv;charset=utf-8;"});
  const url=URL.createObjectURL(blob),link=document.createElement("a");
  link.href=url;link.download="attribute-table-"+new Date().toISOString().slice(0,10)+".csv";
  document.body.appendChild(link);link.click();link.remove();setTimeout(()=>URL.revokeObjectURL(url),1000);
  ctx.status("Exported "+sourceRows.length+" record(s) to CSV"+(selected.length?" (selected records).":" (displayed records)."));
 };
 const clearTableSelectionBtn=ctx.$("attributeClearSelection");
 if(clearTableSelectionBtn)clearTableSelectionBtn.onclick=()=>{ctx.clearSelection();ctx.syncVisibleAttributeTableSelection();};
 const zoomTableSelectionBtn=ctx.$("attributeZoomSelected");
 if(zoomTableSelectionBtn)zoomTableSelectionBtn.onclick=()=>{
  const chosen=ctx.getFeatureRecords().filter(item=>ctx.selectedFeatures.has(item.layer));
  if(!chosen.length){ctx.status("Select one or more records first.");return}
  let bounds=null;
  chosen.forEach(item=>{try{const b=item.layer.getBounds?.();if(b&&b.isValid())bounds=bounds?bounds.extend(b):b;else{const ll=item.layer.getLatLng?.();if(ll)bounds=bounds?bounds.extend(ll):ctx.L.latLngBounds([ll,ll]);}}catch(e){}});
  if(bounds&&bounds.isValid()){ctx.map.fitBounds(bounds.pad(0.12),{maxZoom:18});ctx.status("Zoomed to "+chosen.length+" selected feature(s).");}
  else ctx.status("The selected features have no usable map bounds.");
 };
 const all=ctx.$("checkAllRows"),rowChecks=[...ctx.$("attributeTableWrap").querySelectorAll(".row-check")];
 const syncCheckAll=()=>{
  if(all){
   const checked=rowChecks.filter(ch=>ch.checked).length;
   all.checked=rowChecks.length>0&&checked===rowChecks.length;
   all.indeterminate=checked>0&&checked<rowChecks.length;
  }
  const summary=ctx.$("attributeTableSummary");
  if(summary)summary.textContent=keys.length+" field(s) · "+items.length+" record(s) · "+ctx.selectedFeatures.size+" selected";
 };
 const syncMapSelection=event=>{
  rowChecks.forEach(ch=>{
   const row=ch.closest("tr"),item=items[Number(row?.dataset.index)];
   if(!item)return;
   if(ch.checked)ctx.selectedFeatures.add(item.layer);
   else ctx.selectedFeatures.delete(item.layer);
  });
  const changedRow=event?.target?.closest("tr[data-index]");
  const changedItem=changedRow?items[Number(changedRow.dataset.index)]:null;
  ctx.synchronizeFeatureSelection({primary:changedItem&&ctx.selectedFeatures.has(changedItem.layer)?changedItem.layer:null,renderEditor:false});
  syncCheckAll();
 };
 if(all)all.onchange=()=>{rowChecks.forEach(ch=>ch.checked=all.checked);syncMapSelection()};
 rowChecks.forEach(ch=>ch.onchange=syncMapSelection);
  // Row selection is handled once by the delegated handler below.
 syncCheckAll();
 ctx.$("attributeTableWrap").querySelectorAll("th[data-sort-key]").forEach(th=>{
  th.onclick=()=>{
   if(ctx.hasUnsavedTableEdits()){ctx.status("Save Table Edits before sorting; unsaved cell values have been preserved.");return}
   const key=th.dataset.sortKey;
   if(ctx.attributeSort.key===key)ctx.attributeSort.direction*=-1;
   else{ctx.attributeSort.key=key;ctx.attributeSort.direction=1}
   renderEditableTable();
  };
 });
 ctx.status(rows.length+" rows displayed."+(ctx.attributeSort.key?" · sorted by "+ctx.attributeSort.key+(ctx.attributeSort.direction===1?" ascending":" descending"):""));
  ctx.updateAttributeTableWindowInfo();

 }
 return renderEditableTable();
}
root.SabzAttributeTableRender=Object.freeze({renderEditableTable:render});
})(typeof window!=="undefined"?window:globalThis);


/* Single-feature editor is bundled with the attribute-table runtime to preserve script order. */
/* ArcMap-style single-feature attribute editor; dependencies are injected explicitly. */
(function(root){
"use strict";
function renderAttributeEditor(ctx){
  const box=ctx.$("attributeEditor");
  if(!box)return;
  if(!ctx.selectedLayer){box.style.display="none";ctx.status("Select the first input feature.");return}
  const props=ctx.ensureFeatureProperties(ctx.selectedLayer);
  const keys=Object.keys(props).filter(k=>k!=="_attachments");
  let html="<div id='attributeRows'>";
  keys.forEach(k=>{
    html+="<div class='row attr-edit-row' data-key='"+ctx.esc(k)+"' style='align-items:flex-start'>"+
      "<input class='attr-key' value='"+ctx.esc(k)+"' title='Field Name' style='width:42%' "+(ctx.isComputedAttributeField(k)?"readonly aria-readonly='true' title='Calculated field name — read only'":"")+">"+
      "<input class='attr-value"+(ctx.isComputedAttributeField(k)?" computed-field-readonly":"")+"' value='"+ctx.esc(typeof props[k]==="object"&&props[k]!==null?JSON.stringify(props[k]):props[k])+"' title='"+(ctx.isComputedAttributeField(k)?"Calculated field — read only":"Value")+"' style='width:48%' "+(ctx.isComputedAttributeField(k)?"readonly aria-readonly='true'":"")+">"+
      (ctx.isComputedAttributeField(k)?"<span class='small' title='Calculated field'>Auto</span>":"<button class='action danger attr-delete' type='button' title='Delete Field'>×</button>")+"</div>";
  });
  if(!keys.length)html+="<div class='small'>This feature has no editable attributes.</div>";
  html+="</div><div class='row'><button class='action secondary' id='addAttributeField' type='button'>+ Add Field</button><button class='action' id='saveAttributes' type='button'>Save Attribute</button></div>";
  box.innerHTML=html;box.style.display="block";
  box.querySelectorAll(".attr-delete").forEach(btn=>btn.onclick=()=>btn.closest(".attr-edit-row").remove());
  ctx.$("addAttributeField").onclick=()=>{
    const row=document.createElement("div");row.className="row attr-edit-row";row.style.alignItems="flex-start";
    row.innerHTML="<input class='attr-key' placeholder='Field Name' style='width:42%'><input class='attr-value' placeholder='Value' style='width:48%'><button class='action danger attr-delete' type='button'>×</button>";
    row.querySelector(".attr-delete").onclick=()=>row.remove();ctx.$("attributeRows").appendChild(row);
  };
  ctx.$("saveAttributes").onclick=ctx.saveAttributes;
}
root.SabzAttributeEditor={renderAttributeEditor};
})(window);
