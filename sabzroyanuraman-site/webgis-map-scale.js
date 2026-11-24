/* ArcMap-style map scale display and denominator input. */
(function(root){
 "use strict";
 function init(options){
  if(!options||!options.map||!options.document)throw new TypeError("Map scale controller requires a map and document.");
  const map=options.map,input=options.document.getElementById("mapScale");
  if(!input)return false;
  const format=n=>"1:"+Math.max(1,Math.round(n)).toLocaleString("en-US");
  function denominator(){
   const latitude=map.getCenter().lat;
   return 156543.03392804097*Math.cos(latitude*Math.PI/180)/Math.pow(2,map.getZoom())*96/0.0254;
  }
  function refresh(){
   if(options.document.activeElement!==input)input.value=format(denominator());
  }
  input.addEventListener("keydown",event=>{
   event.stopPropagation();
   if(event.key!=="Enter")return;
   event.preventDefault();
   const raw=input.value.trim().replace(/\s/g,"").replace(/,/g,"");
   const match=raw.match(/^(?:1:)?([0-9]+(?:\.[0-9]+)?)$/);
   if(!match||!Number.isFinite(Number(match[1]))||Number(match[1])<=0){refresh();return;}
   const target=Number(match[1]);
   const numerator=156543.03392804097*Math.cos(map.getCenter().lat*Math.PI/180)*96/0.0254;
   const zoom=Math.log2(numerator/target);
   const minZoom=Number.isFinite(map.getMinZoom?.())?map.getMinZoom():0;
   const maxZoom=Number.isFinite(map.getMaxZoom?.())?map.getMaxZoom():19;
   const previousSnap=map.options.zoomSnap;
   map.options.zoomSnap=0;
   try{map.setZoom(Math.max(minZoom,Math.min(maxZoom,zoom)));}
   finally{map.options.zoomSnap=previousSnap;}
   input.blur();refresh();
  });
  map.on("zoomend moveend",refresh);
  refresh();
  return Object.freeze({refresh,denominator});
 }
 root.SabzWebGISMapScale=Object.freeze({init});
})(typeof window!=="undefined"?window:globalThis);
