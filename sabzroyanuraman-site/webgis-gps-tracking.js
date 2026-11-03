/* WebGIS GPS tracking module. Loaded after the core bridge is initialized. */
(function () {
  "use strict";
  const state = {watchId:null,sessionId:null,points:[],line:null,marker:null,layer:null,lastUpload:0,queue:Promise.resolve()};
  const shared = () => window.__sabzWebGISSharedBridge || null;
  const map = () => shared()?.map || window.__sabzGISMap || null;
  function status(message) {
    const node=document.getElementById("goToXYGpsStatus");
    if(node)node.textContent=String(message ?? "");
  }
  async function send(payload) {
    const response=await fetch("/api/location/track.php",{method:"POST",credentials:"same-origin",headers:{"Content-Type":"application/json","Accept":"application/json"},body:JSON.stringify(payload),cache:"no-store"});
    const result=await response.json().catch(()=>({}));
    if(!response.ok||!result.ok)throw new Error(result.error||("HTTP "+response.status));
    return result;
  }
  function updateButtons() {
    const start=document.getElementById("goToXYTrackStart"),stop=document.getElementById("goToXYTrackStop");
    if(start)start.disabled=state.watchId!==null;
    if(stop)stop.disabled=state.watchId===null;
  }
  function receive(position) {
    const coords=position.coords,latlng=[coords.latitude,coords.longitude],activeMap=map(),L=window.L;
    state.points.push(latlng);
    if(activeMap&&L){
      if(!state.layer)state.layer=L.layerGroup().addTo(activeMap);
      if(!state.marker)state.marker=L.circleMarker(latlng,{radius:8,color:"#fff",weight:3,fillColor:"#0878e8",fillOpacity:1}).bindTooltip("GPS track").addTo(state.layer);
      else state.marker.setLatLng(latlng);
      if(!state.line)state.line=L.polyline(state.points,{color:"#e65100",weight:4,opacity:.9}).addTo(state.layer);
      else state.line.setLatLngs(state.points);
      if(state.points.length===1)activeMap.setView(latlng,Math.max(activeMap.getZoom(),16));
    }
    if(state.sessionId&&Date.now()-state.lastUpload>=5000){
      state.lastUpload=Date.now();const sessionId=state.sessionId;
      state.queue=state.queue.then(()=>send({action:"point",session_id:sessionId,latitude:coords.latitude,longitude:coords.longitude,accuracy:coords.accuracy,speed:coords.speed,heading:coords.heading,altitude:coords.altitude,recorded_at:new Date(position.timestamp||Date.now()).toISOString()}))
        .then(()=>status("Tracking active · "+state.points.length+" points · saved to server"))
        .catch(error=>status("Tracking active; server upload failed: "+error.message));
    }else status((state.watchId!==null?"Tracking active":"Location received")+" · "+state.points.length+" points · accuracy ±"+Math.round(coords.accuracy||0)+" m");
  }
  function start() {
    if(!navigator.geolocation){status("This browser does not support GPS location.");return;}
    if(state.watchId!==null)return;
    if(!window.confirm("Start GPS tracking? Your location points will be sent to and stored on this site's server until you press Stop."))return;
    state.points=[];state.lastUpload=0;
    state.sessionId=window.crypto&&typeof window.crypto.randomUUID==="function"?window.crypto.randomUUID():("trk-"+Date.now()+"-"+Math.random().toString(36).slice(2));
    const sessionId=state.sessionId;
    state.queue=state.queue.then(()=>send({action:"start",session_id:sessionId,started_at:new Date().toISOString()}))
      .catch(error=>status("Could not start server session: "+error.message));
    state.watchId=navigator.geolocation.watchPosition(receive,error=>status(error.code===1?"Location permission denied.":error.code===2?"GPS location unavailable.":"GPS request timed out."),{enableHighAccuracy:true,timeout:30000,maximumAge:0});
    updateButtons();status("Tracking requested; waiting for GPS fix…");
  }
  function stop() {
    if(state.watchId!==null){navigator.geolocation.clearWatch(state.watchId);state.watchId=null;}
    if(state.sessionId){
      const sessionId=state.sessionId;
      state.queue=state.queue.then(()=>send({action:"stop",session_id:sessionId,stopped_at:new Date().toISOString()}))
        .catch(error=>status("Tracking stopped locally; server close failed: "+error.message));
      state.sessionId=null;
    }
    updateButtons();status("Tracking stopped · "+state.points.length+" points recorded in this session.");
  }
  function exportKml() {
    if(state.points.length<2){status("Record at least two GPS points before exporting KML.");return;}
    const coordinates=state.points.map(point=>point[1]+","+point[0]+",0").join(" ");
    const kml='<?xml version="1.0" encoding="UTF-8"?>'+'<kml xmlns="http://www.opengis.net/kml/2.2"><Document><name>GPS Track</name><Placemark><name>GPS route</name><LineString><tessellate>1</tessellate><coordinates>'+coordinates+'</coordinates></LineString></Placemark></Document></kml>';
    const url=URL.createObjectURL(new Blob([kml],{type:"application/vnd.google-earth.kml+xml"})),anchor=document.createElement("a");
    anchor.href=url;anchor.download="gps-track-"+new Date().toISOString().replace(/[:.]/g,"-")+".kml";anchor.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
  }
  function clear() {
    stop();if(state.layer)state.layer.clearLayers();state.marker=null;state.line=null;state.points=[];
    status("GPS track cleared from this map. Previously uploaded server records are not deleted.");
  }
  window.SabzGpsTrack=Object.freeze({start,stop,exportKml,clear,status,get active(){return state.watchId!==null;},get pointCount(){return state.points.length;}});
})();
