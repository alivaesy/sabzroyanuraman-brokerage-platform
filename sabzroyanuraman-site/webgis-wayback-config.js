(function(root){
  "use strict";
  function create({fetch:fetchImpl,storage}){
    if(typeof fetchImpl!=="function")throw new Error("Wayback fetch dependency is required.");
    const WAYBACK_CONFIG_URL="https://s3-us-west-2.amazonaws.com/config.maptiles.arcgis.com/waybackconfig.json";
    const WAYBACK_CACHE_KEY="sabzroyan-webgis-wayback-config-v1";
    function waybackNormalize(data){const src=Array.isArray(data)?data:Object.keys(data||{}).map(k=>data[k]&&typeof data[k]==="object"?Object.assign({releaseNum:Number(k)},data[k]):null);return src.filter(x=>x&&(x.releaseNum!==undefined)&&(x.releaseDateLabel||x.releaseDatetime)).map(x=>({releaseNum:Number(x.releaseNum),releaseDateLabel:x.releaseDateLabel||(x.releaseDatetime?new Date(x.releaseDatetime).toISOString().slice(0,10):String(x.releaseNum)),releaseDatetime:Number(x.releaseDatetime||0),itemURL:x.itemURL||""})).filter(x=>Number.isFinite(x.releaseNum)&&x.itemURL).sort((a,b)=>a.releaseDatetime-b.releaseDatetime);}

    async function fetchWaybackConfig(){
  let lastError=null;
  // Retry transient network failures; do not use public third-party proxy services for map metadata.
  for(let attempt=0;attempt<3;attempt++){
    const controller=typeof AbortController!=="undefined"?new AbortController():null;
    const timeout=controller?setTimeout(()=>controller.abort(),9000):null;
    try{
      const res=await fetchImpl(WAYBACK_CONFIG_URL,{cache:attempt===0?"no-cache":"reload",mode:"cors",signal:controller?controller.signal:undefined});
      if(!res.ok)throw new Error("HTTP "+res.status);
      const data=await res.json();
      const normalized=waybackNormalize(data);
      if(!normalized.length)throw new Error("فهرست نسخه‌ها خالی یا با قالب نامعتبر است");
      try{storage.setItem(WAYBACK_CACHE_KEY,JSON.stringify({savedAt:Date.now(),items:normalized}));}catch(_){}
      return normalized;
    }catch(e){lastError=e;if(attempt<2)await new Promise(resolve=>setTimeout(resolve,500*(attempt+1)));}
    finally{if(timeout)clearTimeout(timeout);}
  }
  // Previously loaded metadata can keep the archive selector usable during an outage.
  try{
    const cached=JSON.parse(storage.getItem(WAYBACK_CACHE_KEY)||"null");
    const items=Array.isArray(cached?.items)?waybackNormalize(cached.items):[];
    if(items.length){items._fromCache=true;return items;}
  }catch(_){}
  throw lastError||new Error("دریافت فهرست آرشیو ناموفق بود");
}

    return {waybackNormalize,fetchWaybackConfig};
  }
  root.SabzWebGISWaybackConfig={create};
})(window);
