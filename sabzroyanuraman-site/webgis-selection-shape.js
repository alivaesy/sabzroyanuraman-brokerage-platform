(function(root){
  "use strict";
  function create({window:windowRef,Leaflet,map,stopActiveDraw,status,setActiveDrawHandler,setActiveDrawPurpose,getActiveDrawCreatedHandler,setActiveDrawCreatedHandler}){
    if(!windowRef||!Leaflet||!map||typeof stopActiveDraw!=="function"||typeof status!=="function"||typeof setActiveDrawHandler!=="function"||typeof setActiveDrawPurpose!=="function"||typeof getActiveDrawCreatedHandler!=="function"||typeof setActiveDrawCreatedHandler!=="function")throw new Error("Selection shape dependencies are incomplete.");
    return function startSelectionShape(type,options,callback){
      if(windowRef._vertexEditControl||windowRef._reshapeDrawHandler){status("Finish or cancel the active geometry edit before starting a selection tool.");return;}
      stopActiveDraw();
      if(!Leaflet||!Leaflet.Draw){status("The drawing library is unavailable.");return;}
      const Ctor=Leaflet.Draw[type];if(typeof Ctor!=="function"){status("This selection shape is unavailable.");return;}
      let handler;try{handler=new Ctor(map,options);}catch(err){console.error(err);status("Could not start the selection tool.");return;}
      setActiveDrawHandler(handler);setActiveDrawPurpose("selection");
      const once=e=>{
        map.off(Leaflet.Draw.Event.CREATED,once);
        if(getActiveDrawCreatedHandler()===once)setActiveDrawCreatedHandler(null);
        stopActiveDraw(true);
        if(e.layerType!==type.toLowerCase())return;
        map.removeLayer(e.layer);
        callback(e.layer);
      };
      setActiveDrawCreatedHandler(once);map.on(Leaflet.Draw.Event.CREATED,once);
      try{handler.enable();}catch(err){map.off(Leaflet.Draw.Event.CREATED,once);if(getActiveDrawCreatedHandler()===once)setActiveDrawCreatedHandler(null);stopActiveDraw();console.error(err);status("Could not start the selection tool.");}
    };
  }
  root.SabzWebGISSelectionShape={create};
})(window);
