/* ArcMap-style previous/next map extent history. */
(function(root){
 "use strict";
 function create(options){
  if(!options||!options.map||!options.document)throw new TypeError("Extent history requires a map and document.");
  const map=options.map,doc=options.document;
  const getElementById=id=>doc.getElementById(id);
  let history=[],index=-1,target=null;
  const same=(a,b)=>!!a&&!!b&&Math.abs(a.lat-b.lat)<1e-7&&Math.abs(a.lng-b.lng)<1e-7&&a.zoom===b.zoom;

  function updateExtentButtons(){
   const back=getElementById("backExtentBtn"),next=getElementById("nextExtentBtn");
   if(back)back.disabled=index<=0;
   if(next)next.disabled=index<0||index>=history.length-1;
  }
  function recordMapExtent(){
   const center=map.getCenter(),entry={lat:center.lat,lng:center.lng,zoom:map.getZoom()};
   if(target!==null){
    if(same(history[target],entry)){index=target;target=null;updateExtentButtons();return;}
    target=null;
   }
   if(same(history[index],entry))return;
   history=history.slice(0,index+1);
   history.push(entry);
   if(history.length>80)history.shift();
   index=history.length-1;
   updateExtentButtons();
   if(!options.isHistoryRestoring?.())options.scheduleAutoSave?.();
  }
  function moveExtentHistory(offset){
   const next=index+Number(offset);
   if(!Number.isInteger(next)||next<0||next>=history.length)return false;
   index=next;target=next;
   const extent=history[next];
   map.setView([extent.lat,extent.lng],extent.zoom,{animate:false});
   updateExtentButtons();
   setTimeout(()=>{if(target===next)target=null;},1000);
   return true;
  }
  function getState(){return {length:history.length,index,target,entries:history.map(item=>({...item}))};}
  return Object.freeze({recordMapExtent,moveExtentHistory,updateExtentButtons,getState});
 }
 root.SabzWebGISMapExtentHistory=Object.freeze({create});
})(typeof window!=="undefined"?window:globalThis);
