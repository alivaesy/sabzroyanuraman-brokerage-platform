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
 return {refreshLabelDecluttering,layerLabelClass};
}
root.SabzWebGISLayerLabeling=Object.freeze({create});
})(typeof window!=="undefined"?window:globalThis);
