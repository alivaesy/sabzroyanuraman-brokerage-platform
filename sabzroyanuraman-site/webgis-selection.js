(function(root){
  "use strict";
  function create({turf,nearestPointOnFeatureSafe}={}){
    if(!turf||typeof nearestPointOnFeatureSafe!=="function") throw new Error("Selection module requires turf and nearestPointOnFeatureSafe.");
    function featureDistanceMeters(a,b){
      if(!a?.geometry||!b?.geometry)return Infinity;
      try{if(turf.booleanIntersects(a,b))return 0;}catch(_){}
      const collectCoordinates=(geometry,out)=>{
        if(!geometry)return;
        if(geometry.type==="GeometryCollection"){
          (geometry.geometries||[]).forEach(g=>collectCoordinates(g,out));
          return;
        }
        const walk=value=>{
          if(!Array.isArray(value))return;
          if(value.length>=2&&typeof value[0]==="number"&&typeof value[1]==="number"){out.push([value[0],value[1]]);return;}
          value.forEach(walk);
        };
        walk(geometry.coordinates);
      };
      const coords=[];collectCoordinates(a.geometry,coords);
      let best=Infinity;
      coords.forEach(c=>{
        try{const near=nearestPointOnFeatureSafe(b,turf.point(c));if(near){const d=turf.distance(turf.point(c),near,{units:"meters"});if(d<best)best=d;}}catch(_){}
      });
      const reverse=[];collectCoordinates(b.geometry,reverse);
      reverse.forEach(c=>{
        try{const near=nearestPointOnFeatureSafe(a,turf.point(c));if(near){const d=turf.distance(turf.point(c),near,{units:"meters"});if(d<best)best=d;}}catch(_){}
      });
      return best;
    }
    return Object.freeze({featureDistanceMeters});
  }
  root.SabzWebGISSelection={create};
})(window);
