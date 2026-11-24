(function(global){
"use strict";
function create({map,arcMapDialog,fetch:fetchImpl,geoJsonLayer,status,$}){
  if(!map||!arcMapDialog||typeof fetchImpl!=="function"||typeof geoJsonLayer!=="function")throw new Error("Find Route dependencies are incomplete.");
  return async function findRoute(){
    const center=map.getCenter();
    const from=await arcMapDialog.prompt({title:"Find Route",message:"Enter the start coordinate as longitude,latitude:",label:"Start coordinate",value:center.lng.toFixed(5)+","+center.lat.toFixed(5)});
    if(!from)return;
    const to=await arcMapDialog.prompt({title:"Find Route",message:"Enter the destination coordinate as longitude,latitude:",label:"Destination coordinate",value:""});
    if(!to)return;
    const parsePair=value=>value.split(/[ ,;]+/).map(Number).slice(0,2);
    const start=parsePair(from),end=parsePair(to);
    if(start.length!==2||end.length!==2||![...start,...end].every(Number.isFinite)||Math.abs(start[0])>180||Math.abs(end[0])>180||Math.abs(start[1])>90||Math.abs(end[1])>90){status("Enter valid longitude,latitude coordinates.");return;}
    status("Requesting a driving route...");
    try{
      const response=await fetchImpl("https://router.project-osrm.org/route/v1/driving/"+start.join(",")+";"+end.join(",")+"?overview=full&geometries=geojson");
      if(!response.ok)throw new Error("HTTP "+response.status);
      const data=await response.json(),route=data.routes?.[0];
      if(data.code!=="Ok"||!route?.geometry)throw new Error(data.code||"No route found.");
      const feature={type:"Feature",properties:{distance_m:Math.round(route.distance),duration_s:Math.round(route.duration),source:"OSRM"},geometry:route.geometry};
      geoJsonLayer({type:"FeatureCollection",features:[feature]},"Route");
      const output=$("professionalToolOutput");
      if(output)output.textContent="Route distance: "+(route.distance/1000).toFixed(2)+" km | Estimated driving time: "+Math.round(route.duration/60)+" min.";
      status("Driving route added.");
    }catch(error){status("Route service unavailable: "+error.message);}
  };
}
global.SabzWebGISFindRoute={create};
})(window);
