/* Versioned compatibility facade for the legacy WebGIS core bridge. */
(function(){
  "use strict";
  const core=window.__sabzWebGISSharedBridge;
  const required=["map","currentCRS","namedLayers","addNamedLayer","status","scheduleAutoSave","serializeProject","restoreProject"];
  const missing=required.filter(key=>!(key in (core||{})) || (typeof core[key]==="undefined"));
  const api={
    version:1,
    get ready(){return missing.length===0;},
    get missingCapabilities(){return missing.slice();},
    core
  };
  ["map","currentCRS","namedLayers","selectedFeatures","drawn"].forEach(key=>{
    Object.defineProperty(api,key,{enumerable:true,get:()=>core?.[key]??null});
  });
  ["addNamedLayer","status","scheduleAutoSave","serializeProject","restoreProject","zoomToSelection","openLayerProperties","openAttributeTableForLayer","refreshEditorTargets","refreshAttributeLayers","removeNamedLayer","renameNamedLayer","canRemoveNamedLayer","importGDBFiles","addGeoJSONLayer","applyRustGISSelection"].forEach(key=>{
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
