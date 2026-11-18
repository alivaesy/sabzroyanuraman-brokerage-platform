/* Layer Properties Symbology tab helpers extracted from web-gis.html. */
(function(root){
"use strict";
function create(ctx){
 if(!ctx||typeof ctx.$!=="function")throw new Error("Symbology tab context is incomplete.");
 function layerStyleRamp(name,count){
  const core=window.SabzWebGISSymbologyCore;
  if(!core||typeof core.layerStyleRamp!=="function")throw new Error("WebGIS symbology core is unavailable.");
  return core.layerStyleRamp(name,count);
}
function refreshLayerRendererControls(){
  const mode=$("lpRenderer")?.value||"single",field=$("lpStyleField")?.value||"",legend=$("lpRendererLegend");
  ["lpStyleField","lpColorRamp","lpClassCount","lpClassificationMethod"].forEach(id=>{const el=$(id);if(el)el.disabled=mode==="single"});
  if(!legend)return;
  const records=ctx.layerPropertiesRecords(ctx.layerPropertiesTarget?.target),values=records.map(r=>r.feature?.properties?.[field]);
  if(mode==="single"){legend.textContent="Single symbol: one symbol for all features.";return}
  if(!field){legend.textContent="Choose an attribute field.";return}
  const core=window.SabzWebGISSymbologyCore;
  if(mode==="unique"){
    const classes=core.uniqueValueClasses(values,$("lpColorRamp").value);
    legend.replaceChildren();classes.slice(0,30).forEach(item=>{const row=document.createElement("div");row.className="lp-field-chip";const sw=document.createElement("span");sw.style.cssText="display:inline-block;width:12px;height:12px;border:1px solid #52616b;margin-right:6px;background:"+item.color+";vertical-align:middle";row.append(sw,document.createTextNode(item.value+" ("+item.count+")"));legend.appendChild(row)});
    if(!classes.length)legend.textContent="No non-empty values in this field.";
    if(classes.length>30){const note=document.createElement("div");note.textContent="Legend limited to first 30 values; all "+classes.length+" unique values are styled.";legend.appendChild(note)}return;
  }
  const method=$("lpClassificationMethod").value||"equal-interval";
  const classes=core.graduatedClasses(values,$("lpClassCount").value,$("lpColorRamp").value,method);
  if(!classes.length){legend.textContent="Graduated Colors requires a numeric field.";return}
  legend.replaceChildren();classes.forEach(item=>{const row=document.createElement("div");row.className="lp-field-chip";const sw=document.createElement("span");sw.style.cssText="display:inline-block;width:12px;height:12px;border:1px solid #52616b;margin-right:6px;background:"+item.color+";vertical-align:middle";row.append(sw,document.createTextNode(item.min.toLocaleString()+" – "+item.max.toLocaleString()));legend.appendChild(row)});
}
function updateLayerSymbolPreview(){
  const preview=$("lpSymbolPreview");if(!preview)return;
  preview.style.borderColor=$("lpOutlineColor").value;preview.style.backgroundColor=$("lpFillColor").value;
  preview.style.borderWidth=Math.min(8,Number($("lpLineWidth").value)||0)+"px";
  preview.style.opacity=$("lpOpacity").value;
  $("lpOpacityValue").textContent=Math.round(Number($("lpOpacity").value)*100)+"%";
  $("lpFillOpacityValue").textContent=Math.round(Number($("lpFillOpacity").value)*100)+"%";
  preview.style.backgroundColor=$("lpFillColor").value;
  preview.style.setProperty("--lp-fill-opacity",$("lpFillOpacity").value);
  preview.style.backgroundImage="linear-gradient(rgba(255,255,255,"+(1-Number($("lpFillOpacity").value))+"),rgba(255,255,255,"+(1-Number($("lpFillOpacity").value))+"))";
}
 return {layerStyleRamp,refreshLayerRendererControls,updateLayerSymbolPreview};
}
root.SabzWebGISLayerSymbology=Object.freeze({create});
})(typeof window!=="undefined"?window:globalThis);
