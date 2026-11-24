/* ArcMap Go To XY dialog, coordinate navigation and GPS location actions. */
(function(root){
 "use strict";
 function open(ctx){
  if(!ctx?.map||!ctx.L||!ctx.document||typeof ctx.getCurrentCRS!=="function"||typeof ctx.utmEpsg!=="function"||typeof ctx.projectCoords!=="function"||typeof ctx.status!=="function")throw new TypeError("Go To XY requires map, Leaflet, document, CRS and projection dependencies.");
  const map=ctx.map,L=ctx.L,document=ctx.document,status=ctx.status;
  const currentCRS=ctx.getCurrentCRS(),utmEpsg=ctx.utmEpsg,projectCoords=ctx.projectCoords;
  const window=root,navigator=root.navigator||{};

  const existing=document.getElementById("goToXYDialog");
  if(existing){existing.remove();return;}
  const overlay=document.createElement("div");
  overlay.id="goToXYDialog";
  overlay.setAttribute("role","dialog");
  overlay.setAttribute("aria-modal","true");
  overlay.setAttribute("aria-labelledby","goToXYTitle");
  overlay.style.cssText="position:fixed;inset:0;z-index:10050;background:rgba(15,23,42,.42);display:flex;align-items:center;justify-content:center;padding:18px;";
  const panel=document.createElement("section");
  panel.style.cssText="width:min(470px,100%);background:var(--panel-bg,#fff);color:var(--text-color,#202124);border:1px solid var(--border-color,#c8ccd0);border-radius:7px;box-shadow:0 12px 40px rgba(0,0,0,.25);font:13px/1.45 Arial,sans-serif;overflow:hidden;";
  panel.innerHTML='<header style="display:flex;align-items:center;justify-content:space-between;padding:11px 14px;background:var(--toolbar-bg,#f2f3f5);border-bottom:1px solid var(--border-color,#c8ccd0)"><strong id="goToXYTitle" style="font-size:14px">Go To XY</strong><button type="button" id="goToXYClose" aria-label="Close" style="border:0;background:transparent;font-size:21px;cursor:pointer;line-height:1">×</button></header><div style="padding:16px"><label for="goToXYSystem" style="display:block;margin-bottom:5px;font-weight:600">Coordinate system</label><select id="goToXYSystem" style="box-sizing:border-box;width:100%;padding:7px;margin-bottom:14px"><option value="map">Map coordinate system ('+String(currentCRS).replace(/&/g,"&amp;").replace(/</g,"&lt;") +')</option><option value="utm">UTM (meters)</option><option value="wgs84">Geographic coordinates (WGS 84, longitude/latitude)</option></select><div id="goToXYUtmOptions" style="display:none;grid-template-columns:1fr 1fr;gap:12px;margin:-5px 0 12px"><label style="display:block;font-weight:600">UTM Zone<select id="goToXYUtmZone" style="box-sizing:border-box;width:100%;padding:7px;margin-top:5px"><option value="1">Zone 1</option><option value="2">Zone 2</option><option value="3">Zone 3</option><option value="4">Zone 4</option><option value="5">Zone 5</option><option value="6">Zone 6</option><option value="7">Zone 7</option><option value="8">Zone 8</option><option value="9">Zone 9</option><option value="10">Zone 10</option><option value="11">Zone 11</option><option value="12">Zone 12</option><option value="13">Zone 13</option><option value="14">Zone 14</option><option value="15">Zone 15</option><option value="16">Zone 16</option><option value="17">Zone 17</option><option value="18">Zone 18</option><option value="19">Zone 19</option><option value="20">Zone 20</option><option value="21">Zone 21</option><option value="22">Zone 22</option><option value="23">Zone 23</option><option value="24">Zone 24</option><option value="25">Zone 25</option><option value="26">Zone 26</option><option value="27">Zone 27</option><option value="28">Zone 28</option><option value="29">Zone 29</option><option value="30">Zone 30</option><option value="31">Zone 31</option><option value="32">Zone 32</option><option value="33">Zone 33</option><option value="34">Zone 34</option><option value="35">Zone 35</option><option value="36">Zone 36</option><option value="37">Zone 37</option><option value="38" selected>Zone 38</option><option value="39">Zone 39</option><option value="40">Zone 40</option><option value="41">Zone 41</option><option value="42">Zone 42</option><option value="43">Zone 43</option><option value="44">Zone 44</option><option value="45">Zone 45</option><option value="46">Zone 46</option><option value="47">Zone 47</option><option value="48">Zone 48</option><option value="49">Zone 49</option><option value="50">Zone 50</option><option value="51">Zone 51</option><option value="52">Zone 52</option><option value="53">Zone 53</option><option value="54">Zone 54</option><option value="55">Zone 55</option><option value="56">Zone 56</option><option value="57">Zone 57</option><option value="58">Zone 58</option><option value="59">Zone 59</option><option value="60">Zone 60</option></select></label><label style="display:block;font-weight:600">Hemisphere<select id="goToXYUtmHemisphere" style="box-sizing:border-box;width:100%;padding:7px;margin-top:5px"><option value="N" selected>Northern</option><option value="S">Southern</option></select></label></div><div style="display:grid;grid-template-columns:1fr 1fr;gap:12px"><label style="display:block;font-weight:600">X / Longitude<input id="goToXYX" inputmode="decimal" autocomplete="off" placeholder="Enter X" style="box-sizing:border-box;width:100%;padding:8px;margin-top:5px;border:1px solid #9ca3af;border-radius:3px;font-weight:400"></label><label style="display:block;font-weight:600">Y / Latitude<input id="goToXYY" inputmode="decimal" autocomplete="off" placeholder="Enter Y" style="box-sizing:border-box;width:100%;padding:8px;margin-top:5px;border:1px solid #9ca3af;border-radius:3px;font-weight:400"></label></div><div id="goToXYError" role="alert" style="min-height:18px;margin-top:8px;color:#b42318"></div><div style="font-size:12px;color:var(--muted,#666);margin:2px 0 14px">Enter decimal coordinates. In WGS 84, X is longitude and Y is latitude.</div><div style="display:flex;gap:8px;justify-content:flex-end;flex-wrap:wrap"><button type="button" id="goToXYCurrentLocation" title="Use browser GPS/location permission">My Current Location</button><button type="button" id="goToXYFlash">Flash Location</button><button type="button" id="goToXYPan">Pan To</button><button type="button" id="goToXYZoom" style="font-weight:600">Zoom To</button></div><hr style="margin:14px 0 10px;border:0;border-top:1px solid var(--border-color,#ddd)"><div style="font-weight:700;margin-bottom:7px">GPS Track</div><div style="display:flex;gap:7px;flex-wrap:wrap"><button type="button" id="goToXYTrackStart">Start Tracking</button><button type="button" id="goToXYTrackStop" disabled>Stop</button><button type="button" id="goToXYTrackKml">Export KML</button><button type="button" id="goToXYTrackClear">Clear Map Track</button></div><div id="goToXYGpsStatus" role="status" style="white-space:pre-wrap;font-size:12px;margin-top:8px;color:var(--muted,#666)">Tracking is off. Start explicitly to begin recording and uploading GPS points.</div><div style="font-size:11px;margin-top:5px;color:var(--muted,#666)">Starting tracking uploads location points to the site server. Stop ends tracking; clearing the map does not delete server records.</div></div></div>';
  overlay.appendChild(panel);document.body.appendChild(overlay);
  const xInput=panel.querySelector("#goToXYX"),yInput=panel.querySelector("#goToXYY"),system=panel.querySelector("#goToXYSystem"),error=panel.querySelector("#goToXYError"),utmOptions=panel.querySelector("#goToXYUtmOptions"),utmZone=panel.querySelector("#goToXYUtmZone"),utmHemisphere=panel.querySelector("#goToXYUtmHemisphere");
  const gpsTrack=window.SabzGpsTrack;
  panel.querySelector("#goToXYTrackStart").onclick=()=>gpsTrack?.start();panel.querySelector("#goToXYTrackStop").onclick=()=>gpsTrack?.stop();panel.querySelector("#goToXYTrackKml").onclick=()=>gpsTrack?.exportKml();panel.querySelector("#goToXYTrackClear").onclick=()=>gpsTrack?.clear();
  if(gpsTrack?.active){panel.querySelector("#goToXYTrackStart").disabled=true;panel.querySelector("#goToXYTrackStop").disabled=false;gpsTrack.status("Tracking active · "+gpsTrack.pointCount+" points recorded.");}
  const updateCoordinateInputs=()=>{const utm=system.value==="utm";utmOptions.style.display=utm?"grid":"none";if(utm){const center=map.getCenter(),zone=Math.max(1,Math.min(60,Math.floor((center.lng+180)/6)+1));utmZone.value=String(zone);utmHemisphere.value=center.lat>=0?"N":"S";}xInput.parentElement.childNodes[0].textContent=system.value==="wgs84"?"X / Longitude":utm?"Easting (m)":"X";yInput.parentElement.childNodes[0].textContent=system.value==="wgs84"?"Y / Latitude":utm?"Northing (m)":"Y";xInput.placeholder=utm?"e.g. 500000":"Enter X";yInput.placeholder=utm?"e.g. 4500000":"Enter Y";};
  system.addEventListener("change",updateCoordinateInputs);updateCoordinateInputs();
  const close=()=>{overlay.remove();document.removeEventListener("keydown",onKey)};
  const onKey=e=>{if(e.key==="Escape")close()};
  panel.querySelector("#goToXYClose").onclick=close;
  overlay.addEventListener("click",e=>{if(e.target===overlay)close()});
  document.addEventListener("keydown",onKey);
  const getLatLng=()=>{
    const x=Number(xInput.value.trim()),y=Number(yInput.value.trim());
    if(!xInput.value.trim()||!yInput.value.trim()||!Number.isFinite(x)||!Number.isFinite(y))throw new Error("Enter valid numeric values for both X and Y.");
    let ll;
    if(system.value==="wgs84"){
      if(x < -180 || x > 180 || y < -90 || y > 90)throw new Error("WGS 84 requires longitude between -180 and 180 and latitude between -90 and 90.");
      ll=[x,y];
    }else{
      const sourceCRS=system.value==="utm"?utmEpsg(Number(utmZone.value),utmHemisphere.value==="N"):currentCRS;
      if(system.value==="utm"&&(!Number.isInteger(Number(utmZone.value))||Number(utmZone.value)<1||Number(utmZone.value)>60))throw new Error("UTM zone must be between 1 and 60.");
      ll=sourceCRS==="EPSG:4326"?[x,y]:projectCoords(sourceCRS,"EPSG:4326",[x,y]);
      if(!Array.isArray(ll)||ll.length<2||!ll.slice(0,2).every(Number.isFinite))throw new Error("The coordinates could not be converted from "+currentCRS+".");
      if(ll[0] < -180 || ll[0] > 180 || ll[1] < -90 || ll[1] > 90)throw new Error("The coordinates fall outside the valid geographic range. Check the selected coordinate system.");
    }
    return L.latLng(ll[1],ll[0]);
  };
  const go=(action)=>{
    try{
      const ll=getLatLng();error.textContent="";
      if(action==="flash"){
        const marker=L.circleMarker(ll,{radius:10,color:"#ff2d2d",weight:3,fillColor:"#ffeb3b",fillOpacity:.9,interactive:false}).addTo(map);
        map.setView(ll,Math.max(map.getZoom(),14));
        setTimeout(()=>{if(map.hasLayer(marker))map.removeLayer(marker)},1800);
        status("Flashing XY location.");
      }else{
        map.setView(ll,action==="zoom"?Math.max(map.getZoom(),16):map.getZoom());
        status((action==="zoom"?"Zoomed to":"Centered map at")+" X: "+xInput.value.trim()+" | Y: "+yInput.value.trim());
        close();
      }
    }catch(err){error.textContent=err.message||"Could not navigate to these coordinates."}
  };
  let currentLocationMarker=null;
  panel.querySelector("#goToXYCurrentLocation").onclick=()=>{
    const button=panel.querySelector("#goToXYCurrentLocation");
    if(!navigator.geolocation){error.textContent="Geolocation is not supported by this browser.";return;}
    button.disabled=true;button.textContent="Finding location…";error.textContent="";
    navigator.geolocation.getCurrentPosition(position=>{
      const longitude=position.coords.longitude,latitude=position.coords.latitude;
      system.value="wgs84";updateCoordinateInputs();
      xInput.value=longitude.toFixed(7);yInput.value=latitude.toFixed(7);
      try{
        const ll=L.latLng(latitude,longitude);
        map.setView(ll,Math.max(map.getZoom(),16));
        if(currentLocationMarker&&map.hasLayer(currentLocationMarker))map.removeLayer(currentLocationMarker);
        currentLocationMarker=L.circleMarker(ll,{radius:9,color:"#fff",weight:3,fillColor:"#0878e8",fillOpacity:1}).addTo(map);
        currentLocationMarker.bindTooltip("My current location",{permanent:false,direction:"top"});
        status("Current location displayed on the map.");
        close();
      }catch(err){error.textContent=err.message||"Could not display your current location.";button.disabled=false;button.textContent="My Current Location";}
    },err=>{
      const messages={1:"Location permission was denied. Allow location access for this website in your browser.",2:"Your location is currently unavailable. Check GPS or network access.",3:"Location request timed out. Please try again."};
      error.textContent=messages[err.code]||"Could not get your current location.";
      button.disabled=false;button.textContent="My Current Location";
    },{enableHighAccuracy:true,timeout:15000,maximumAge:30000});
  };
  panel.querySelector("#goToXYFlash").onclick=()=>go("flash");
  panel.querySelector("#goToXYPan").onclick=()=>go("pan");
  panel.querySelector("#goToXYZoom").onclick=()=>go("zoom");
  [xInput,yInput].forEach(input=>input.addEventListener("keydown",e=>{if(e.key==="Enter"){e.preventDefault();go("zoom")}}));
  const center=map.getCenter();
  if(currentCRS==="EPSG:4326"){xInput.value=center.lng.toFixed(6);yInput.value=center.lat.toFixed(6);system.value="wgs84";}
  xInput.focus();xInput.select();
 }
 root.SabzWebGISGoToXY=Object.freeze({open});
})(typeof window!=="undefined"?window:globalThis);
