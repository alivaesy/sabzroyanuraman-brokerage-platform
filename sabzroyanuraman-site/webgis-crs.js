(function(root){
  "use strict";
  function create({proj4}){
    if(!proj4||typeof proj4.defs!=="function")throw new Error("Proj4 dependency is required.");
    function utmEpsg(zone,north=true){if(!Number.isInteger(zone)||zone<1||zone>60)throw new Error("UTM zone must be an integer from 1 to 60.");return "EPSG:"+((north?32600:32700)+zone)}
function defineUTM(zone,north=true){
  const epsg=utmEpsg(zone,north);
  if(!proj4.defs(epsg)){
    proj4.defs(epsg,"+proj=utm +zone="+zone+(north?"":" +south")+" +datum=WGS84 +units=m +no_defs +type=crs");
  }
  return epsg;
}

    for(let z=1;z<=60;z++){defineUTM(z,true);defineUTM(z,false);}
    function ensureCRSDefinition(epsg){
  if(!/^EPSG:\d+$/.test(epsg))throw new Error("Invalid EPSG code.");
  if(!proj4.defs(epsg)){
    const n=Number(epsg.slice(5));
    if(n>=32601&&n<=32660)defineUTM(n-32600,true);
    else if(n>=32701&&n<=32760)defineUTM(n-32700,false);
    else throw new Error("Definition "+epsg+" in Proj4 found not.");
  }
  return epsg;
}
function projectCoords(from,to,coords){
  ensureCRSDefinition(to);
  return proj4(from,to,coords);
}

    return {utmEpsg,defineUTM,ensureCRSDefinition,projectCoords};
  }
  root.SabzWebGISCRS={create};
})(window);
