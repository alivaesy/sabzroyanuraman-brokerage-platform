/* Layer Properties Labels tab helpers extracted from web-gis.html. */
(function(root){
"use strict";
function create(ctx){
 if(!ctx||!ctx.map||!Array.isArray(ctx.namedLayers))throw new Error("Labeling tab context is incomplete.");
 const {map,namedLayers}=ctx;
 function refreshLabelDecluttering(){
  if(!map||!window.SabzWebGISLabelingCore||!window.SabzWebGISLabelingCore.selectVisibleLabels)return;
  const zoom=map.getZoom();
  const candidates=[];
  // One shared collision pass is required: labels from separate visible layers
  // compete for the same map space just as they do in a desktop GIS.
  [...namedLayers].reverse().forEach(owner=>{
    if(!owner||!owner._labelsEnabled||!owner.layer||!map.hasLayer(owner.layer)||
      typeof owner.layer.eachLayer!=="function")return;
    const minZoom=Math.max(0,Math.min(22,Number(owner._labelMinZoom)||0));
    owner.layer.eachLayer(featureLayer=>{
      const tooltip=featureLayer.getTooltip&&featureLayer.getTooltip();
      const element=tooltip&&(tooltip.getElement?tooltip.getElement():tooltip._container);
      if(!element)return;
      if(zoom<minZoom){element.style.visibility="hidden";return;}
      const rect=element.getBoundingClientRect();
      if(!rect.width||!rect.height)return;
      candidates.push({
        element,
        density:owner._labelDensity||"balanced",
        box:{left:rect.left,top:rect.top,right:rect.right,bottom:rect.bottom}
      });
    });
  });
  // The tooltip pane's DOM order reflects paint order; retain topmost labels first.
  candidates.sort((a,b)=>{
    if(!a.element.isConnected||!b.element.isConnected)return 0;
    const position=a.element.compareDocumentPosition(b.element);
    if(position&Node.DOCUMENT_POSITION_FOLLOWING)return 1;
    if(position&Node.DOCUMENT_POSITION_PRECEDING)return -1;
    return 0;
  });
  const visible=window.SabzWebGISLabelingCore.selectVisibleLabels(
    candidates.map(item=>({...item.box,density:item.density})),
    {density:"balanced"}
  );
  const keep=new Set(visible);
  candidates.forEach((item,index)=>{item.element.style.visibility=keep.has(index)?"":"hidden";});
}
map.on("zoomend moveend layeradd layerremove",refreshLabelDecluttering);
function layerLabelClass(owner){
  if(!owner)return "gis-feature-label";
  if(!owner._labelClass)owner._labelClass="gis-feature-label-"+Math.random().toString(36).slice(2,10);
  const id=owner._labelClass.replace(/[^a-z0-9_-]/gi,"");
  const config=window.SabzWebGISLabelingCore.normalizeLabelConfig({enabled:owner._labelsEnabled,field:owner._labelField,size:owner._labelSize,color:owner._labelColor,placement:owner._labelPlacement});
  owner._labelSize=config.size;owner._labelColor=config.color;
  let style=document.getElementById(id+"-style");
  if(!style){style=document.createElement("style");style.id=id+"-style";document.head.appendChild(style);}
  style.textContent=window.SabzWebGISLabelingCore.labelClassCss(id,config);
  return id;
}

 function configureLayerPropertiesLabels(owner,fields){
  const fieldSelect=ctx.$("lpLabelField");fieldSelect.innerHTML="";
  fields.forEach(field=>{const o=ctx.document.createElement("option");o.value=field;o.textContent=field;fieldSelect.appendChild(o)});
  fieldSelect.value=fields.includes(owner?._labelField)?owner._labelField:(fields.includes("name")?"name":fields[0]||"");
  ctx.$("lpLabelsEnabled").checked=!!owner?._labelsEnabled;
  ctx.$("lpLabelSize").value=owner?._labelSize||12;
  ctx.$("lpLabelColor").value=owner?._labelColor||"#202020";
  ctx.$("lpLabelPlacement").value=owner?._labelPlacement||"auto";
  ctx.$("lpLabelMinZoom").value=owner?._labelMinZoom??0;
  ctx.$("lpLabelDensity").value=owner?._labelDensity||"balanced";
 }
 function resetLayerPropertiesTabs(){
  ctx.document.querySelectorAll("[data-lp-tab]").forEach((b,i)=>{b.classList.toggle("active",i===0);b.setAttribute("aria-selected",i===0?"true":"false")});
  ctx.document.querySelectorAll("[data-lp-pane]").forEach((p,i)=>p.hidden=i!==0);
 }
 function bindLayerPropertiesTabs(){
  ctx.document.querySelectorAll("[data-lp-tab]").forEach(button=>button.addEventListener("click",()=>{
   const key=button.dataset.lpTab;
   ctx.document.querySelectorAll("[data-lp-tab]").forEach(b=>{const active=b===button;b.classList.toggle("active",active);b.setAttribute("aria-selected",active?"true":"false")});
   ctx.document.querySelectorAll("[data-lp-pane]").forEach(p=>p.hidden=p.dataset.lpPane!==key);
  }));
 }
 async function labelLayer(){
  const target=ctx.contextLayerItem?.layer||null;const rs=target?ctx.layerPropertiesRecords(target):ctx.getFeatureRecords();
  if(!rs.length){ctx.status("No vector features are available for labeling.");return;}
  const fields=[...new Set(rs.flatMap(r=>Object.keys(r.feature.properties||{}).filter(k=>!ctx.isReservedAttributeField(k))))];
  if(!fields.length){ctx.status("Features have no attribute fields to use as labels.");return;}
  const fieldInput=await ctx.gisAsk("Label Features","Label field (case-sensitive):\n"+fields.join(", "),fields.includes("name")?"name":fields[0]);
  if(fieldInput===null)return;
  const field=fieldInput.trim();
  if(!fields.includes(field)){ctx.status("Unknown label field. Choose one of the listed fields.");return;}
  const owner=target?(ctx.namedLayers.find(x=>x.layer===target)||ctx.contextLayerItem):null;
  if(owner){const config=ctx.core.normalizeLabelConfig({enabled:true,field,size:12,color:"#202020"});owner._labelsEnabled=config.enabled;owner._labelField=config.field;owner._labelSize=config.size;owner._labelColor=config.color;}
  const labelClass=owner?ctx.layerLabelClass(owner):"gis-feature-label";
  let count=0;
  rs.forEach(r=>{
    try{
      if(typeof r.layer.unbindTooltip==="function")r.layer.unbindTooltip();
      const value=ctx.core.labelText(r.feature.properties?.[field]);
      if(value!==null&&typeof r.layer.bindTooltip==="function"){
        r.layer.bindTooltip(value,{permanent:true,...ctx.core.labelPlacement(r.feature?.geometry?.type,owner?._labelPlacement),className:labelClass,interactive:false});
        count++;
      }
    }catch(e){ctx.debug("Could not label feature",e);}
  });
  if(owner)ctx.scheduleAutoSave();
  ctx.status("Labels enabled for "+count+" feature(s) using field '"+field+"'.");
 }
 return {refreshLabelDecluttering,layerLabelClass,labelLayer,configureLayerPropertiesLabels,resetLayerPropertiesTabs,bindLayerPropertiesTabs};
}
root.SabzWebGISLayerLabeling=Object.freeze({create});
})(typeof window!=="undefined"?window:globalThis);
