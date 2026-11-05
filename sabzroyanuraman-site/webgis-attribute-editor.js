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
  html+="</div><div class='row'><button class='action secondary' id='addAttributeField' type='button'>+ Add Field</button><button class='action' id='ctx.saveAttributes' type='button'>Save Attribute</button></div>";
  box.innerHTML=html;box.style.display="block";
  box.querySelectorAll(".attr-delete").forEach(btn=>btn.onclick=()=>btn.closest(".attr-edit-row").remove());
  ctx.$("addAttributeField").onclick=()=>{
    const row=document.createElement("div");row.className="row attr-edit-row";row.style.alignItems="flex-start";
    row.innerHTML="<input class='attr-key' placeholder='Field Name' style='width:42%'><input class='attr-value' placeholder='Value' style='width:48%'><button class='action danger attr-delete' type='button'>×</button>";
    row.querySelector(".attr-delete").onclick=()=>row.remove();ctx.$("attributeRows").appendChild(row);
  };
  ctx.$("ctx.saveAttributes").onclick=ctx.saveAttributes;
}
root.SabzAttributeEditor={renderAttributeEditor};
})(window);
