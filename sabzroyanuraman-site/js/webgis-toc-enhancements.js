/* ArcMap-style TOC enhancements for Sabz Royan WebGIS. */
(function () {
"use strict";
const $=id=>document.getElementById(id), expanded=new Set();
const api=()=>window.__sabzWebGISSharedBridge;
let map=null,namedLayers=[],currentCRS="",tocViewMode="drawing",contextLayerItem=null,selectedFeatures=new Set();
const layerPropertiesRecords=owner=>api()?.layerPropertiesRecords?.(owner)||[];
const layerStyleRamp=(name,count)=>window.layerStyleRamp?.(name,count)||["#56a878"];
const openLayerProperties=owner=>api()?.openLayerProperties?.(owner);
const refreshEditorTargets=()=>api()?.refreshEditorTargets?.();
const refreshAttributeLayers=()=>api()?.refreshAttributeLayers?.();
const scheduleAutoSave=()=>api()?.scheduleAutoSave?.();
const selectedRecords=()=>api()?.selectedRecords?.()||[];
const applySelectionVisual=()=>api()?.applySelectionVisual?.();
const hideLayerContextMenu=()=>api()?.hideLayerContextMenu?.();
const addNamedLayer=(name,layer,zoom)=>api()?.addNamedLayer?.(name,layer,zoom);
const status=message=>api()?.status?.(message);
let rootMenu=null;
function leaves(layer,out=[]){if(!layer)return out;if(typeof layer.eachLayer==="function")layer.eachLayer(x=>leaves(x,out));else out.push(layer);return out;}
function recordsFor(owner){try{return typeof layerPropertiesRecords==="function"?layerPropertiesRecords(owner.layer):leaves(owner.layer).filter(x=>x.feature).map(layer=>({layer,feature:layer.feature}));}catch(_){return [];}}
function validColor(v,f){return /^#[0-9a-f]{6}$/i.test(String(v||""))?v:f;}
function styleOf(owner){const first=recordsFor(owner)[0]?.layer,o=first?.options||owner.layer?.options||{};return {color:validColor(o.color,"#146c43"),fillColor:validColor(o.fillColor,validColor(o.color,"#56a878")),weight:Math.max(.5,Number(o.weight)||2),radius:Math.max(2,Number(o.radius)||6)};}
function rendererClasses(owner){
 const c=owner?._rendererConfig;if(!c||!["unique","graduated"].includes(c.mode))return [];
 const values=recordsFor(owner).map(r=>r.feature?.properties?.[c.field]);
 if(c.mode==="unique"){const u=[...new Set(values.filter(v=>v!==null&&v!==undefined&&String(v).trim()!=="").map(String))].sort((a,b)=>a.localeCompare(b,undefined,{numeric:true})),colors=layerStyleRamp(c.ramp||"green",Math.max(1,u.length));return u.map((v,i)=>({label:v,color:colors[i%colors.length]}));}
 const nums=values.map(Number).filter(Number.isFinite);if(!nums.length)return [];
 const n=Math.max(2,Math.min(7,Number(c.classes)||5)),min=Math.min(...nums),max=Math.max(...nums),colors=layerStyleRamp(c.ramp||"green",n),fmt=v=>Number(v.toPrecision(5)).toLocaleString();
 return Array.from({length:n},(_,i)=>({label:min===max?fmt(min):fmt(min+(max-min)*i/n)+" – "+fmt(min+(max-min)*(i+1)/n),color:colors[i]}));
}
function makeSwatch(owner,color){const s=styleOf(owner),records=recordsFor(owner),point=records.some(r=>typeof r.layer?.setRadius==="function"),line=records.some(r=>["LineString","MultiLineString"].includes(r.feature?.geometry?.type)),el=document.createElement("span");el.className="toc-symbol-swatch"+(point?" is-point":line?" is-line":"");el.style.setProperty("--toc-fill",color||s.fillColor);el.style.setProperty("--toc-line",s.color);el.style.setProperty("--toc-width",Math.min(4,s.weight)+"px");el.style.setProperty("--toc-size",Math.max(6,Math.min(14,s.radius*2))+"px");el.title="Layer symbol — double-click for Symbology";el.ondblclick=e=>{e.preventDefault();e.stopPropagation();activateProperties(owner,"symbology");};return el;}
function activateProperties(owner,tab){if(!owner)return;openLayerProperties(owner);if(tab==="symbology")document.querySelector('[data-lp-tab="symbology"]')?.click();}
function addLegend(row,owner){
 const classes=rendererClasses(owner),icon=row.querySelector(".layer-icon");if(!icon)return;
 if(!classes.length){icon.replaceWith(makeSwatch(owner));return;}
 const toggle=document.createElement("button");toggle.type="button";toggle.className="toc-legend-toggle";toggle.textContent=expanded.has(owner)?"▾":"▸";toggle.setAttribute("aria-expanded",expanded.has(owner)?"true":"false");toggle.setAttribute("aria-label","Expand legend");
 toggle.onclick=e=>{e.preventDefault();e.stopPropagation();expanded.has(owner)?expanded.delete(owner):expanded.add(owner);renderLayerList();};
 icon.replaceWith(toggle,makeSwatch(owner));
 if(expanded.has(owner)){const list=document.createElement("div");list.className="toc-legend-classes";classes.forEach(c=>{const line=document.createElement("div"),label=document.createElement("span"),sw=makeSwatch(owner,c.color);line.className="toc-legend-class";sw.classList.add("toc-class-swatch");label.textContent=c.label;line.append(sw,label);list.appendChild(line);});row.after(list);}
}
function frameName(){return $("projectName")?.value?.trim()||"Layers";}
function frameDialog(){let d=$("tocDataFrameDialog");if(d)return d;d=document.createElement("div");d.id="tocDataFrameDialog";d.className="data-dialog";d.setAttribute("aria-hidden","true");d.innerHTML='<section class="data-dialog-card" role="dialog" aria-modal="true"><h2>Data Frame Properties</h2><dl class="toc-frame-details"><dt>Coordinate System</dt><dd data-frame-crs></dd><dt>Current Extent</dt><dd data-frame-extent></dd></dl><div class="data-dialog-actions"><button type="button" class="action" data-close>Close</button></div></section>';document.body.appendChild(d);d.querySelector("[data-close]").onclick=()=>{d.classList.remove("show");d.setAttribute("aria-hidden","true");};d.addEventListener("click",e=>{if(e.target===d)d.querySelector("[data-close]").click();});return d;}
function showFrame(){const m=api()?.map;if(!m)return;const d=frameDialog(),b=m.getBounds();d.querySelector("[data-frame-crs]").textContent=typeof api()?.currentCRS==="string"?api().currentCRS:"Not set";d.querySelector("[data-frame-extent]").textContent=b.isValid()?"W: "+b.getWest().toFixed(6)+" · S: "+b.getSouth().toFixed(6)+" · E: "+b.getEast().toFixed(6)+" · N: "+b.getNorth().toFixed(6):"Unavailable";d.classList.add("show");d.setAttribute("aria-hidden","false");}
function rootContext(x,y){if(!rootMenu){rootMenu=document.createElement("div");rootMenu.className="arc-context-menu toc-root-menu";rootMenu.innerHTML='<button type="button">Properties...</button>';rootMenu.querySelector("button").onclick=()=>{rootMenu.classList.remove("show");showFrame();};document.body.appendChild(rootMenu);document.addEventListener("click",e=>{if(!rootMenu.contains(e.target))rootMenu.classList.remove("show");});}rootMenu.style.left=Math.max(4,Math.min(x,innerWidth-180))+"px";rootMenu.style.top=Math.max(4,Math.min(y,innerHeight-60))+"px";rootMenu.classList.add("show");}
function applyZOrder(){const bridge=api();[...(bridge?.namedLayers||[])].reverse().forEach(o=>leaves(o.layer).forEach(l=>{if(typeof l.bringToFront==="function")l.bringToFront();}));refreshEditorTargets();refreshAttributeLayers();}
function installDrag(row,owner){if(!owner||api()?.tocViewMode!=="drawing"){row.draggable=false;row.classList.add("toc-reorder-disabled");return;}row.draggable=true;row.classList.add("toc-draggable");row.addEventListener("dragstart",e=>{e.dataTransfer.effectAllowed="move";e.dataTransfer.setData("text/plain",String((api()?.namedLayers||[]).indexOf(owner)));row.classList.add("toc-dragging");});row.addEventListener("dragend",()=>row.classList.remove("toc-dragging"));row.addEventListener("dragover",e=>{e.preventDefault();e.dataTransfer.dropEffect="move";});row.addEventListener("drop",e=>{e.preventDefault();e.stopPropagation();const layers=api()?.namedLayers||[],from=Number(e.dataTransfer.getData("text/plain")),to=layers.indexOf(owner);if(!Number.isInteger(from)||from<0||from>=layers.length||to<0||from===to)return;const moved=layers.splice(from,1)[0];layers.splice(to,0,moved);applyZOrder();renderLayerList();scheduleAutoSave();status("Layer order updated.");});}
function addMenuActions(){
 const m=$("layerContextMenu");if(!m||m.dataset.tocEnhanced==="1")return;m.dataset.tocEnhanced="1";
 const sep=()=>{const x=document.createElement("div");x.className="arc-context-sep";m.appendChild(x);};
 const add=(action,label)=>{const b=document.createElement("button");b.type="button";b.dataset.tocAction=action;b.textContent=label;m.appendChild(b);b.addEventListener("click",e=>{e.preventDefault();e.stopImmediatePropagation();const item=api()?.contextLayerItem;if(!item?.layer)return;
 if(action==="zoom-selected"){const rs=selectedRecords().filter(r=>r.owner?.layer===item.layer);if(!rs.length){status("No selected features in this layer.");return;}const bounds=L.featureGroup(rs.map(r=>r.layer)).getBounds();if(bounds.isValid())map.fitBounds(bounds,{padding:[25,25],maxZoom:18});}
 else if(action==="clear-selected"){selectedRecords().filter(r=>r.owner?.layer===item.layer).forEach(r=>api()?.selectedFeatures?.delete(r.layer));applySelectionVisual();api()?.renderLayerList?.();status("Selection cleared for "+item.name+".");}
 else if(action==="create-selected"){const rs=selectedRecords().filter(r=>r.owner?.layer===item.layer);if(!rs.length){status("Select features in this layer first.");return;}const features=rs.map(r=>r.layer.toGeoJSON()).filter(f=>f?.geometry),layer=L.geoJSON({type:"FeatureCollection",features});addNamedLayer((item.name||"Layer")+" — Selection",layer,true);renderLayerList();refreshAttributeLayers();scheduleAutoSave();status("Created layer from "+features.length+" selected feature(s).");}
 else if(action==="save-layer-json"){const owner=(api()?.namedLayers||[]).find(x=>x.layer===item.layer);if(!owner){status("Only named layers can be saved.");return;}const data={format:"sabz-webgis-layer-reference",version:1,name:owner.name,source:owner.sourceName||owner.source||owner._sourceUrl||null,renderer:owner._rendererConfig||{mode:"single"},symbol:styleOf(owner),labels:{enabled:!!owner._labelsEnabled,field:owner._labelField||null,size:owner._labelSize||12,color:owner._labelColor||"#202020"},visible:map.hasLayer(owner.layer),exportedAt:new Date().toISOString(),note:"Style and source reference only; feature data is not embedded."},blob=new Blob([JSON.stringify(data,null,2)],{type:"application/json"}),url=URL.createObjectURL(blob),a=document.createElement("a");a.href=url;a.download=(owner.name||"layer").replace(/[^a-z0-9_-]+/gi,"_")+".sabz-layer.json";a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);status("Layer style/reference JSON exported.");}
 hideLayerContextMenu();});};
 sep();add("zoom-selected","Zoom To Selected Features");add("clear-selected","Clear Selected Features");add("create-selected","Create Layer From Selected Features");sep();add("save-layer-json","Save As Layer File... (Sabz JSON)");
}
function decorate(){
 const c=$("layers");if(!c)return;const bridge=api();map=bridge?.map;namedLayers=bridge?.namedLayers||[];currentCRS=bridge?.currentCRS||"";tocViewMode=bridge?.tocViewMode||"drawing";selectedFeatures=bridge?.selectedFeatures||new Set();
 [...c.querySelectorAll(":scope > .layer-row")].forEach(row=>{const i=Number(row.dataset.tocLayerIndex),owner=Number.isInteger(i)&&i>=0?(api()?.namedLayers||[])[i]:null;if(!owner)return;row.classList.add("toc-layer-row");row.style.paddingLeft="22px";const name=row.querySelector(".layer-name");if(name){name.ondblclick=e=>{e.preventDefault();e.stopPropagation();activateProperties(owner,"general");};name.title="Double-click for Layer Properties";}const icon=row.querySelector(".layer-icon");if(icon)icon.ondblclick=e=>{e.preventDefault();e.stopPropagation();activateProperties(owner,"symbology");};addLegend(row,owner);installDrag(row,owner);});
 const root=document.createElement("div");root.className="toc-data-frame-root";const caption=document.createElement("span");caption.textContent="▣  "+frameName();caption.title="Data Frame — right-click for properties";caption.oncontextmenu=e=>{e.preventDefault();e.stopPropagation();rootContext(e.clientX,e.clientY);};caption.ondblclick=showFrame;root.appendChild(caption);const probe=document.createElement("button");probe.type="button";probe.className="toc-host-probe";probe.textContent="آزمایش هاست";probe.title="برای بررسی اعمال نسخه جدید روی هاست کلیک کنید";probe.addEventListener("click",e=>{e.preventDefault();e.stopPropagation();probe.textContent="اعمال شد ✓";probe.classList.add("is-applied");probe.title="نسخه جدید اسکریپت TOC در این صفحه اجرا شده است";if(typeof status==="function")status("آزمایش TOC موفق: اسکریپت جدید روی این صفحه اجرا شده است.");});root.appendChild(probe);root.oncontextmenu=e=>{if(e.target===root){e.preventDefault();rootContext(e.clientX,e.clientY);}};c.prepend(root);
}
function install(){const bridge=api();if(!bridge||typeof bridge.renderLayerList!=="function"||!Array.isArray(bridge.namedLayers)||!bridge.map)return false;map=bridge.map;namedLayers=bridge.namedLayers;currentCRS=bridge.currentCRS||"";tocViewMode=bridge.tocViewMode||"drawing";selectedFeatures=bridge.selectedFeatures||new Set();addMenuActions();const c=$("layers");if(!c)return false;decorate();if(!c.__sabzTocObserver){let queued=false;const observer=new MutationObserver(()=>{if(queued)return;queued=true;queueMicrotask(()=>{queued=false;const current=api();if(!current||typeof current.renderLayerList!=="function")return;map=current.map;namedLayers=current.namedLayers;const root=c.querySelector(":scope > .toc-data-frame-root"),rows=[...c.querySelectorAll(":scope > .layer-row")];if(!root||rows.some(row=>row.dataset.tocEnhanced!=="1"))decorate();});});observer.observe(c,{childList:true,subtree:false});c.__sabzTocObserver=observer;}return true;}
const css=document.createElement("style");
css.textContent=".toc-data-frame-root{display:flex;align-items:center;justify-content:space-between;gap:8px;font-weight:700;background:#e9eff4;border:1px solid #c4d0d8;border-radius:3px;padding:7px 9px;margin:2px 0 5px;color:#243e50;cursor:default} "+".toc-host-probe{flex:0 0 auto;border:1px solid #8ca7b7;border-radius:3px;background:#fff;color:#244c63;padding:4px 8px;font-size:11px;font-weight:600;cursor:pointer}.toc-host-probe.is-applied{background:#e2f3e8;border-color:#4b9868;color:#176237} "+
".toc-layer-row{min-height:27px;gap:5px} "+
".toc-symbol-swatch{display:inline-block;flex:0 0 18px;width:18px;height:13px;border:var(--toc-width,2px) solid var(--toc-line,#146c43);background:var(--toc-fill,#56a878);border-radius:2px;vertical-align:middle;cursor:pointer} "+
".toc-symbol-swatch.is-point{width:var(--toc-size,10px);height:var(--toc-size,10px);flex-basis:var(--toc-size,10px);border-radius:50%;margin:0 4px} "+
".toc-symbol-swatch.is-line{height:0;border:0;border-top:var(--toc-width,2px) solid var(--toc-line,#146c43);background:transparent;border-radius:0} "+
".toc-legend-toggle{width:15px;min-width:15px;height:18px;padding:0;border:0;background:transparent;color:#40596b;cursor:pointer} "+
".toc-legend-classes{padding:2px 0 5px 54px;display:flex;flex-direction:column;gap:3px} "+
".toc-legend-class{display:flex;align-items:center;gap:7px;min-height:19px;font-size:11px;color:#344b5b} "+
".toc-class-swatch{width:15px;height:11px;flex-basis:15px;cursor:default} "+
".toc-draggable{cursor:grab}.toc-dragging{opacity:.45}.toc-reorder-disabled{cursor:default} "+
".toc-frame-details{display:grid;grid-template-columns:125px minmax(0,1fr);gap:9px;margin:12px 0} "+
".toc-frame-details dt{font-weight:700}.toc-frame-details dd{margin:0;overflow-wrap:anywhere}.toc-root-menu{z-index:10050}";
document.head.appendChild(css);
if(document.readyState==="loading")document.addEventListener("DOMContentLoaded",install,{once:true});else install();
let bridgeAttempts=0;const bridgeRetry=setInterval(()=>{if(install()||++bridgeAttempts>=80)clearInterval(bridgeRetry);},100);
})();