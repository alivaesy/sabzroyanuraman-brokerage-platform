/* Versioned compatibility facade for the legacy WebGIS core bridge. */
(function(){
  "use strict";
  const core=window.__sabzWebGISSharedBridge;
  if(core){
    if(!Array.isArray(core.rasterSources))core.rasterSources=[];
    if(typeof core.registerRasterSource!=="function"){
      core.registerRasterSource=(name,bytes,fileName)=>{
        const finalName=String(name||"Raster").trim()||"Raster";
        const payload=bytes instanceof Uint8Array?new Uint8Array(bytes):new Uint8Array(bytes||[]);
        const record={name:finalName,fileName:fileName||finalName+".tif",bytes:payload};
        const index=core.rasterSources.findIndex(item=>String(item.name||"").toLowerCase()===finalName.toLowerCase());
        if(index>=0)core.rasterSources[index]=record;else core.rasterSources.push(record);
        return finalName;
      };
    }
    if(typeof core.addRasterOverlay!=="function"){
      core.addRasterOverlay=(name,imageUrl,bounds,bytes,fileName)=>{
        if(!window.L||typeof window.L.imageOverlay!=="function")throw new Error("Leaflet image overlay is unavailable.");
        if(!Array.isArray(bounds)||bounds.length!==2)throw new Error("Raster overlay requires Leaflet bounds.");
        const layer=window.L.imageOverlay(imageUrl,bounds,{opacity:1,interactive:true});
        layer._sabzRasterMetadata={name:String(name||"Raster"),fileName:fileName||String(name||"Raster")+".tif"};
        core.addNamedLayer(layer._sabzRasterMetadata.name,layer,false);
        if(bytes)core.registerRasterSource(layer._sabzRasterMetadata.name,bytes,fileName);
        return layer._sabzRasterMetadata.name;
      };
    }
  }
  const required=["map","currentCRS","namedLayers","addNamedLayer","status","scheduleAutoSave","serializeProject","restoreProject"];
  const missing=required.filter(key=>!(key in (core||{})) || (typeof core[key]==="undefined"));
  const api={
    version:1,
    get ready(){return missing.length===0;},
    get missingCapabilities(){return missing.slice();},
    core
  };
  ["map","currentCRS","namedLayers","selectedFeatures","drawn","rasterSources"].forEach(key=>{
    Object.defineProperty(api,key,{enumerable:true,get:()=>core?.[key]??null});
  });
  ["addNamedLayer","status","scheduleAutoSave","serializeProject","restoreProject","zoomToSelection","openLayerProperties","openAttributeTableForLayer","refreshEditorTargets","refreshAttributeLayers","removeNamedLayer","renameNamedLayer","canRemoveNamedLayer","importGDBFiles","addGeoJSONLayer","applyRustGISSelection","registerRasterSource","addRasterOverlay"].forEach(key=>{
    if(typeof core?.[key]==="function")Object.defineProperty(api,key,{enumerable:true,value:(...args)=>core[key](...args)});
  });
  window.SabzWebGIS=Object.freeze(api);
  if(!api.ready){
    const message="WebGIS bridge v1 is missing: "+missing.join(", ");
    window.__sabzWebGISBridgeError=message;
    console.error(message);
  }else{
    window.__sabzWebGISBridgeError=null;
  }
})();
