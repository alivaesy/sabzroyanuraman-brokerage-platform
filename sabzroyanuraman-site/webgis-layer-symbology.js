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
  const mode=ctx.$("lpRenderer")?.value||"single",field=ctx.$("lpStyleField")?.value||"",legend=ctx.$("lpRendererLegend");
  ["lpStyleField","lpColorRamp","lpClassCount","lpClassificationMethod"].forEach(id=>{const el=ctx.$(id);if(el)el.disabled=mode==="single"});
  if(!legend)return;
  const records=ctx.layerPropertiesRecords(ctx.layerPropertiesTarget?.target),values=records.map(r=>r.feature?.properties?.[field]);
  if(mode==="single"){legend.textContent="Single symbol: one symbol for all features.";return}
  if(!field){legend.textContent="Choose an attribute field.";return}
  const core=window.SabzWebGISSymbologyCore;
  if(mode==="unique"){
    const classes=core.uniqueValueClasses(values,ctx.$("lpColorRamp").value);
    legend.replaceChildren();classes.slice(0,30).forEach(item=>{const row=document.createElement("div");row.className="lp-field-chip";const sw=document.createElement("span");sw.style.cssText="display:inline-block;width:12px;height:12px;border:1px solid #52616b;margin-right:6px;background:"+item.color+";vertical-align:middle";row.append(sw,document.createTextNode(item.value+" ("+item.count+")"));legend.appendChild(row)});
    if(!classes.length)legend.textContent="No non-empty values in this field.";
    if(classes.length>30){const note=document.createElement("div");note.textContent="Legend limited to first 30 values; all "+classes.length+" unique values are styled.";legend.appendChild(note)}return;
  }
  const method=ctx.$("lpClassificationMethod").value||"equal-interval";
  const classes=core.graduatedClasses(values,ctx.$("lpClassCount").value,ctx.$("lpColorRamp").value,method);
  if(!classes.length){legend.textContent="Graduated Colors requires a numeric field.";return}
  legend.replaceChildren();classes.forEach(item=>{const row=document.createElement("div");row.className="lp-field-chip";const sw=document.createElement("span");sw.style.cssText="display:inline-block;width:12px;height:12px;border:1px solid #52616b;margin-right:6px;background:"+item.color+";vertical-align:middle";row.append(sw,document.createTextNode(item.min.toLocaleString()+" – "+item.max.toLocaleString()));legend.appendChild(row)});
}
function updateLayerSymbolPreview(){
  const preview=ctx.$("lpSymbolPreview");if(!preview)return;
  preview.style.borderColor=ctx.$("lpOutlineColor").value;preview.style.backgroundColor=ctx.$("lpFillColor").value;
  preview.style.borderWidth=Math.min(8,Number(ctx.$("lpLineWidth").value)||0)+"px";
  preview.style.opacity=ctx.$("lpOpacity").value;
  ctx.$("lpOpacityValue").textContent=Math.round(Number(ctx.$("lpOpacity").value)*100)+"%";
  ctx.$("lpFillOpacityValue").textContent=Math.round(Number(ctx.$("lpFillOpacity").value)*100)+"%";
  preview.style.backgroundColor=ctx.$("lpFillColor").value;
  preview.style.setProperty("--lp-fill-opacity",ctx.$("lpFillOpacity").value);
  preview.style.backgroundImage="linear-gradient(rgba(255,255,255,"+(1-Number(ctx.$("lpFillOpacity").value))+"),rgba(255,255,255,"+(1-Number(ctx.$("lpFillOpacity").value))+"))";
}

function applyLayerProperties(e){
  e.preventDefault();if(!ctx.layerPropertiesTarget)return;
  const {item,target}=ctx.layerPropertiesTarget,owner=ctx.namedLayers.find(x=>x.layer===target)||item;
  const records=ctx.layerPropertiesRecords(target);
  if(!owner&&!records.length){ctx.status("Could not resolve the selected layer.");return;}
  const name=ctx.$("lpLayerName").value.trim();if(!name){ctx.status("Layer name cannot be empty.");return;}
  const width=Number(ctx.$("lpLineWidth").value),radius=Number(ctx.$("lpPointSize").value),opacity=Number(ctx.$("lpOpacity").value),fillOpacity=Number(ctx.$("lpFillOpacity").value);
  if(!Number.isFinite(width)||width<0||width>20||!Number.isFinite(radius)||radius<1||radius>40){ctx.status("Check the symbol width and point size.");return;}
  if(owner){const labelConfig=ctx.labelCore.normalizeLabelConfig({enabled:ctx.$("lpLabelsEnabled").checked,field:ctx.$("lpLabelField").value,size:ctx.$("lpLabelSize").value,color:ctx.$("lpLabelColor").value,placement:ctx.$("lpLabelPlacement").value,minZoom:ctx.$("lpLabelMinZoom").value,density:ctx.$("lpLabelDensity").value});owner.name=name;owner._labelsEnabled=labelConfig.enabled;owner._labelField=labelConfig.field;owner._labelSize=labelConfig.size;owner._labelColor=labelConfig.color;owner._labelPlacement=labelConfig.placement;owner._labelMinZoom=labelConfig.minZoom;owner._labelDensity=labelConfig.density;ctx.layerLabelClass(owner);}
  const visible=ctx.$("lpLayerVisible").checked;
  if(visible&&!ctx.map.hasLayer(target))ctx.map.addLayer(target);else if(!visible&&ctx.map.hasLayer(target))ctx.map.removeLayer(target);
  const baseStyle={color:ctx.$("lpOutlineColor").value,fillColor:ctx.$("lpFillColor").value,weight:width,opacity,fillOpacity,radius};
  const rendererMode=ctx.$("lpRenderer").value,styleField=ctx.$("lpStyleField").value,ramp=ctx.$("lpColorRamp").value,classCount=Math.max(2,Math.min(7,Number(ctx.$("lpClassCount").value)||5)),method=ctx.$("lpClassificationMethod").value||"equal-interval";
  const values=records.map(r=>r.feature?.properties?.[styleField]),core=ctx.core;
  const uniqueClasses=core.uniqueValueClasses(values,ramp),graduated=core.graduatedClasses(values,classCount,ramp,method);
  if(rendererMode==="graduated"&&!graduated.length){ctx.status("Graduated Colors requires a numeric field with values.");return}
  const rendererConfig={mode:rendererMode,field:styleField,ramp,classes:classCount,method};
  if(owner)owner._rendererConfig=rendererConfig;
  let styled=0;
  records.forEach(r=>{
    try{
      let style={...baseStyle},symbolColor=null;
      const raw=r.feature?.properties?.[styleField];
      if(rendererMode==="unique"){
        const item=uniqueClasses.find(entry=>entry.value===String(raw));
        if(item)symbolColor=item.color;
      }else if(rendererMode==="graduated"){
        const index=core.classifyGraduatedValue(raw,graduated);
        if(index>=0)symbolColor=graduated[index].color;
      }
      if(symbolColor){
        const isLine=r.layer instanceof ctx.L.Polyline&&!(r.layer instanceof ctx.L.Polygon);
        if(isLine)style.color=symbolColor;
        else style.fillColor=symbolColor;
      }
      if(typeof r.layer.setStyle==="function"){r.layer.setStyle(style);styled++;}
      if(typeof r.layer.setRadius==="function")r.layer.setRadius(radius);
      if(owner?._labelsEnabled&&owner._labelField&&typeof r.layer.bindTooltip==="function"){
        const labelText=ctx.labelCore.labelText(r.feature.properties?.[owner._labelField]);r.layer.unbindTooltip();
        if(labelText!==null)r.layer.bindTooltip(labelText,{permanent:true,...ctx.labelCore.labelPlacement(r.feature?.geometry?.type,owner._labelPlacement),className:ctx.layerLabelClass(owner),interactive:false});
      }else if(typeof r.layer.unbindTooltip==="function")r.layer.unbindTooltip();
    }catch(err){console.debug("Layer Properties style skipped",err);}
  });
  if(!records.length&&typeof target.setStyle==="function"){try{target.setStyle(baseStyle);styled=1}catch(_){}}
  ctx.refreshLabelDecluttering();
  if(owner&&owner.layerControl&&owner.layerControl.removeLayer){}
  ctx.renderLayerList();
  const layoutPanel=document.getElementById("webgisLayoutView");
  if(layoutPanel?._legendListener)layoutPanel._legendListener();
  ctx.scheduleAutoSave();
  ctx.status("Layer Properties applied to "+(owner?.name||name)+" ("+styled+" feature symbol(s)).");
  if(e.submitter?.id==="layerPropertiesOk")ctx.closeLayerProperties();
}

function configureLayerPropertiesSymbology(owner,fields){
 const styleField=ctx.$("lpStyleField");styleField.replaceChildren();
 fields.forEach(field=>{const option=document.createElement("option");option.value=field;option.textContent=field;styleField.appendChild(option)});
 styleField.value=fields.includes(owner?._rendererConfig?.field)?owner._rendererConfig.field:(fields.includes("type")?"type":fields[0]||"");
 ctx.$("lpRenderer").value=["single","unique","graduated"].includes(owner?._rendererConfig?.mode)?owner._rendererConfig.mode:"single";
 ctx.$("lpColorRamp").value=["green","blue","red","purple","spectral"].includes(owner?._rendererConfig?.ramp)?owner._rendererConfig.ramp:"green";
 ctx.$("lpClassCount").value=Math.max(2,Math.min(7,Number(owner?._rendererConfig?.classes)||5));
 ctx.$("lpClassificationMethod").value=owner?._rendererConfig?.method==="quantile"?"quantile":"equal-interval";
}
function bindLayerPropertiesSymbology(){
 ["lpOutlineColor","lpFillColor","lpLineWidth","lpOpacity","lpFillOpacity"].forEach(id=>ctx.$(id).addEventListener("input",updateLayerSymbolPreview));
 ["lpRenderer","lpStyleField","lpColorRamp","lpClassCount","lpClassificationMethod"].forEach(id=>ctx.$(id).addEventListener("change",refreshLayerRendererControls));
 ctx.$("layerPropertiesClose").onclick=ctx.$("layerPropertiesCancel").onclick=ctx.closeLayerProperties;
 ctx.$("layerPropertiesDialog").addEventListener("click",event=>{if(event.target===ctx.$("layerPropertiesDialog"))ctx.closeLayerProperties()});
 ctx.$("layerPropertiesForm").addEventListener("submit",event=>applyLayerProperties(event));
}
 return {layerStyleRamp,refreshLayerRendererControls,updateLayerSymbolPreview,applyLayerProperties,configureLayerPropertiesSymbology,bindLayerPropertiesSymbology};
}
root.SabzWebGISLayerSymbology=Object.freeze({create});
})(typeof window!=="undefined"?window:globalThis);
