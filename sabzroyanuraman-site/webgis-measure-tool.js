(function(global){
"use strict";
function create({stopActiveDraw,arcMapDialog,startSelectionShape,turf,map,status,getLastMeasureLayer,setLastMeasureLayer}){
  if(typeof startSelectionShape!=="function"||!turf||!map)throw new Error("Measure tool dependencies are incomplete.");
  return async function startMeasureTool(){
    stopActiveDraw(true);
    const shapeType=await arcMapDialog.select({title:"Measure",message:"Choose the measurement type:",label:"Measurement",value:"distance",options:[{value:"distance",label:"Distance"},{value:"area",label:"Area"}]});
    if(!shapeType)return;
    const isArea=/^a/i.test(shapeType);
    startSelectionShape(isArea?"Polygon":"Polyline",{allowIntersection:false,shapeOptions:{color:"#d17b00",weight:3,dashArray:"6,4"}},shape=>{
      const geo=shape.toGeoJSON();
      if(isArea){
        const result=turf.area(geo),label=(result/1e6).toFixed(4)+" km²";
        shape.addTo(map);status("Measured area: "+label+".");shape.bindPopup("Area: "+label).openPopup();
      }else{
        const result=turf.length(geo,{units:"kilometers"}),label=result.toFixed(3)+" km";
        shape.addTo(map);status("Measured distance: "+label+".");shape.bindPopup("Distance: "+label).openPopup();
      }
      const previous=getLastMeasureLayer();
      if(previous&&map.hasLayer(previous))map.removeLayer(previous);
      setLastMeasureLayer(shape);
    });
  };
}
global.SabzWebGISMeasureTool={create};
})(window);
