
/* Read-only server project sharing: requires the project to be saved to the GIS server. */
(function(){
 const shareButton=document.getElementById("shareProjectBtn");
 const query=new URLSearchParams(location.search);
 async function readJson(response){let data={};try{data=await response.json()}catch(_){}if(!response.ok||data.ok===false)throw new Error(data.error||("HTTP "+response.status));return data;}
 async function loadSharedProject(){
   const token=query.get("share");if(!token)return;
   const bridge=window.__sabzWebGISSharedBridge;
   const status=message=>{const b=window.__sabzWebGISSharedBridge;if(b&&typeof b.status==="function")b.status(message);else{const n=document.getElementById("status");if(n)n.textContent=String(message??"");console.info("[Shared WebGIS]",message);}};
   if(!bridge){console.error("Shared GIS bridge unavailable: core WebGIS script did not initialize.");return;}
   window.__sharedProjectReadOnly=true;
   // Wait briefly for map construction; if the core script crashed, show the actual reason.
   for(let attempt=0;attempt<50&&!bridge.map&&!window.__sabzWebGISCoreError;attempt++)await new Promise(resolve=>setTimeout(resolve,100));
   if(window.__sabzWebGISCoreError){status("WebGIS startup failed before map creation: "+window.__sabzWebGISCoreError);console.error("WebGIS startup failed:",window.__sabzWebGISCoreError);return;}
   if(!bridge.map){status("WebGIS map did not initialize. Check the browser console for the first JavaScript error.");return;}
   if(!/^[a-f0-9]{64}$/.test(token)){status("Shared project link is invalid.");return;}
   try{
     const result=await readJson(await fetch("/api/gis/share.php?token="+encodeURIComponent(token),{credentials:"omit",cache:"no-store",headers:{Accept:"application/json"}}));
     if(!result.project||!result.project.project_data||typeof bridge.restoreProject!=="function")throw new Error("shared_project_data_missing");
     let sharedData=result.project.project_data;
     if(typeof sharedData==="string")sharedData=JSON.parse(sharedData);
     if(!sharedData||typeof sharedData!=="object")throw new Error("shared_project_data_invalid");
     // Older server saves may omit the client-side version/envelope fields. Normalize
     // only the shared copy, never the stored project, before passing it to restoreProject.
     if(![1,2,3].includes(Number(sharedData.version)))sharedData.version=3;
     sharedData.drawn=Array.isArray(sharedData.drawn)?sharedData.drawn:[];
     sharedData.layers=Array.isArray(sharedData.layers)?sharedData.layers:[];
     sharedData.layers=sharedData.layers.filter(item=>item&&typeof item==="object").map(item=>({
       ...item,
       features:Array.isArray(item.features)?item.features.filter(feature=>feature&&feature.type==="Feature"):[]
     }));
     if(!Array.isArray(sharedData.selected))sharedData.selected=[];
     if(!Array.isArray(sharedData.drawnStyles))sharedData.drawnStyles=[];
     sharedData.integrity={drawnCount:sharedData.drawn.length,layerCount:sharedData.layers.length,featureCount:sharedData.drawn.length+sharedData.layers.reduce((n,item)=>n+item.features.length,0)};
     const sharedFeatureCount=sharedData.drawn.length+sharedData.layers.reduce((n,item)=>n+item.features.length,0);
     // Restore the full project when possible, but never let a restore validation
     // failure prevent the public read-only viewer from drawing the saved GeoJSON.
     let restoreWarning="";
     try{
       if(!bridge.restoreProject(sharedData))restoreWarning="Project restore validation failed";
     }catch(restoreError){
       restoreWarning="Project restore failed: "+(restoreError.message||"unknown error");
       console.error(restoreWarning,restoreError);
     }
     // Render from the API payload independently of restoreProject. This is the
     // critical fallback: a stale extent or other restore-only validation issue
     // must not suppress otherwise valid saved geometries.
     try{
       const contentBounds=L.latLngBounds([]);
       let renderedFeatureCount=0;
       (bridge.namedLayers||[]).forEach(item=>{
         if(!item||!item.layer||!bridge.map.hasLayer(item.layer))return;
         if(typeof item.layer.getBounds==="function"){
           const b=item.layer.getBounds();
           if(b&&b.isValid())contentBounds.extend(b);
         }
         if(typeof item.layer.getLayers==="function")renderedFeatureCount+=item.layer.getLayers().length;
       });
       const savedFeatures=[];
       sharedData.drawn.forEach(f=>{if(f&&f.type==="Feature"&&f.geometry)savedFeatures.push(f);});
       sharedData.layers.forEach(item=>{
         (Array.isArray(item.features)?item.features:[]).forEach(f=>{
           if(f&&f.type==="Feature"&&f.geometry)savedFeatures.push(f);
         });
       });
       // Never trust a non-empty layer list alone: verify the map actually has
       // rendered child features before considering restoration successful.
       if(savedFeatures.length){
         const fallback=L.geoJSON({type:"FeatureCollection",features:savedFeatures},{
           style:{color:"#146c43",weight:3,fillOpacity:.2},
           pointToLayer:(feature,latlng)=>L.circleMarker(latlng,{
             radius:6,color:"#146c43",fillColor:"#fff",fillOpacity:.95,weight:2
           })
         });
         const fb=fallback.getBounds();
         if(fb&&fb.isValid()){
           fallback.addTo(bridge.map);
           bridge.map.__sabzSharedFallbackCount=savedFeatures.length;
           contentBounds.extend(fb);
           console.warn("Shared map: rendered GeoJSON fallback",savedFeatures.length,"features");
         }
       }
       // Always fit to actual feature coordinates, even when a stale saved extent
       // is valid but spans a continent.
       const actualBounds=L.latLngBounds([]);
       savedFeatures.forEach(feature=>{
         try{
           const one=L.geoJSON(feature);
           const b=one.getBounds();
           if(b&&b.isValid())actualBounds.extend(b);
         }catch(geometryError){console.warn("Invalid shared feature geometry:",geometryError);}
       });
       const targetBounds=actualBounds.isValid()?actualBounds:contentBounds;
       if(targetBounds.isValid()){
         bridge.map.fitBounds(targetBounds,{padding:[28,28],maxZoom:17,animate:false});
         if(typeof bridge.map.invalidateSize==="function")setTimeout(()=>bridge.map.invalidateSize({pan:false}),150);
       }else if(!savedFeatures.length){
         status("Shared project opened, but it contains no saved geometries.");
       }else{
         status("Shared project has "+savedFeatures.length+" geometries, but Leaflet could not calculate their extent. Check the browser console.");
       }
     }catch(fitError){
       console.error("Shared map rendering fallback failed:",fitError);
       status("Shared features were received, but rendering failed: "+(fitError.message||"unknown error"));
     }
     const p=result.project;
     if(document.getElementById("projectName"))document.getElementById("projectName").value=p.name||"Shared map";
     if(document.getElementById("projectStatus"))document.getElementById("projectStatus").textContent="Shared read-only map · updated "+new Date(p.updated_at||Date.now()).toLocaleString();
     document.title=(p.name||"Shared map")+" — Sabz Royan GIS";
     document.querySelectorAll("#shareProjectBtn,#saveProjectIcon,#openProjectBtn,#newMapBtn,#startEditingBtn,#stopEditingBtn,#deleteBtn").forEach(el=>{if(el)el.disabled=true;});
     document.body.classList.add("shared-project-readonly");
     const verifiedMap=bridge.map;
     const visibleSharedLayers=(bridge.namedLayers||[]).filter(item=>item&&item.layer&&verifiedMap.hasLayer(item.layer));
     const verifiedFeatureCount=visibleSharedLayers.reduce((count,item)=>count+(typeof item.layer.getLayers==="function"?item.layer.getLayers().length:0),0);
     const fallbackCount=verifiedMap.__sabzSharedFallbackCount||0;
     const actuallyDisplayed=verifiedFeatureCount>0||fallbackCount>0;
     if(!actuallyDisplayed){status("Shared data loaded, but no features are confirmed visible on the map. Check the browser console.");console.error("Shared project visibility verification failed",{visibleLayers:visibleSharedLayers.length,verifiedFeatureCount,restoreWarning});return;}
     status("Shared map opened: "+Math.max(verifiedFeatureCount,fallbackCount)+" features displayed in read-only mode."+(restoreWarning?" (GeoJSON fallback used; "+restoreWarning+")":""));
   }catch(error){status("Unable to open shared map: "+(error.message||"link unavailable"));}
 }
 if(query.has("share")){if(shareButton)shareButton.hidden=true;loadSharedProject();}

  // Allow the post-load ModelBuilder enhancement to wrap the existing renderer.
  window.__sabzInstallModelBuilderRenderer = factory => { window.renderModelBuilderGraph = factory(window.renderModelBuilderGraph); };
})();
