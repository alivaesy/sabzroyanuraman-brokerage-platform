/* Geographic and UTM coordinate readout for the active map pointer/center. */
(function(root){
 "use strict";
 const instances=new WeakMap();
 function utmZoneForCoordinate(lon,lat){
  let zone=Math.floor((lon+180)/6)+1;
  if(lon===180)zone=60;
  if(lat>=56&&lat<64&&lon>=3&&lon<12)zone=32;
  if(lat>=72&&lat<84){
   if(lon>=0&&lon<9)zone=31;
   else if(lon>=9&&lon<21)zone=33;
   else if(lon>=21&&lon<33)zone=35;
   else if(lon>=33&&lon<42)zone=37;
  }
  return Math.max(1,Math.min(60,zone));
 }
 function init(options){
  if(!options?.map||!options.document||typeof options.projectCoords!=="function"||typeof options.utmEpsg!=="function")throw new TypeError("Coordinate readout requires map, document, projectCoords and utmEpsg.");
  const map=options.map;
  if(instances.has(map))return instances.get(map);
  const doc=options.document;
  function update(latlng){
   const geographic=doc.getElementById("coordGeographic"),utm=doc.getElementById("coordUtm");
   if(!geographic||!utm||!latlng)return;
   const lat=Number(latlng.lat),lon=Number(latlng.lng);
   if(!Number.isFinite(lat)||!Number.isFinite(lon)||lat < -90||lat > 90||lon < -180||lon > 180)return;
   geographic.textContent=lat.toFixed(6)+", "+lon.toFixed(6);
   if(lat < -80||lat > 84){utm.textContent="UTM unavailable at this latitude";return;}
   try{
    const zone=utmZoneForCoordinate(lon,lat),north=lat>=0;
    const epsg=options.utmEpsg(zone,north);
    const xy=options.projectCoords("EPSG:4326",epsg,[lon,lat]);
    if(!Array.isArray(xy)||xy.length<2||!xy.every(Number.isFinite)||xy[0]<100000||xy[0]>900000||xy[1]<0||xy[1]>10000000)throw new Error("Projected coordinates outside valid UTM bounds");
    utm.textContent=zone+(north?"N":"S")+" · E "+xy[0].toFixed(2)+" m · N "+xy[1].toFixed(2)+" m";
   }catch(error){
    utm.textContent="UTM unavailable";
    root.console?.debug?.("UTM coordinate conversion failed.",error);
   }
  }
  const onMove=event=>update(event.latlng);
  const onMapRefresh=()=>update(map.getCenter());
  map.on("mousemove",onMove);
  map.on("moveend",onMapRefresh);
  map.on("zoomend",onMapRefresh);
  map.on("mouseout",onMapRefresh);
  map.whenReady(onMapRefresh);
  const api=Object.freeze({update,utmZoneForCoordinate});
  instances.set(map,api);
  return api;
 }
 root.SabzWebGISCoordinateReadout=Object.freeze({init,utmZoneForCoordinate});
})(typeof window!=="undefined"?window:globalThis);
