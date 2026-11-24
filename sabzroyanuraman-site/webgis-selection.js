(function(root){
  "use strict";
  function create({turf,nearestPointOnFeatureSafe,getDrawnLayer,getNamedLayers,isVectorLayer,vectorFeatures,getSelectedFeatures,arcMapDialog,status,getNamedLayerRegistry,getDrawnLayerRef,getFeatureRecords,getSelectedLayer,setSelectedLayer,setSelectedOverlay,renderAttributeEditor,applySelectionVisual,$,currentAttributeLayer,isReservedAttributeField}={}){
    if(!turf||typeof nearestPointOnFeatureSafe!=="function") throw new Error("Selection module requires turf and nearestPointOnFeatureSafe.");
    if(typeof getDrawnLayer!=="function"||typeof getNamedLayers!=="function"||typeof isVectorLayer!=="function"||typeof vectorFeatures!=="function"||typeof getSelectedFeatures!=="function"||!arcMapDialog||typeof status!=="function"||typeof getFeatureRecords!=="function"||typeof getSelectedLayer!=="function"||typeof setSelectedLayer!=="function"||typeof setSelectedOverlay!=="function"||typeof renderAttributeEditor!=="function"||typeof applySelectionVisual!=="function"||typeof $!=="function"||typeof currentAttributeLayer!=="function"||typeof isReservedAttributeField!=="function")throw new Error("Selection operation dependencies are incomplete.");
    function allSelectableFeatureLayers(){
      return [{name:"My Drawings",layer:getDrawnLayer()}].concat(getNamedLayers()).filter(x=>isVectorLayer(x.layer));
    }
    function getFeatureRecords(){
      return allSelectableFeatureLayers().flatMap(x=>vectorFeatures(x.layer).map(v=>({owner:x,layer:v.layer,feature:v.feature})));
    }
    function selectedRecords(){
      const records=getFeatureRecords(),byLayer=new Map();
      records.forEach(record=>{if(record?.layer&&!byLayer.has(record.layer))byLayer.set(record.layer,record);});
      return [...getSelectedFeatures()].map(layer=>byLayer.get(layer)).filter(Boolean);
    }
async function selectByLocation(){
  const items=[{name:"My Drawings",layer:drawn},...namedLayers]
    .filter((x,i,arr)=>x.layer&&isVectorLayer(x.layer)&&vectorFeatures(x.layer).length&&arr.findIndex(y=>y.layer===x.layer)===i)
    .map((x,index)=>({...x,key:String(index)}));
  if(items.length<2){status("Load at least two vector layers (including My Drawings when it contains features) before spatial selection.");return;}
  const relationships=[{value:"intersects",label:"Intersects"},{value:"within",label:"Within"},{value:"contains",label:"Contains"},{value:"touches",label:"Touches"},{value:"overlaps",label:"Overlaps"},{value:"crosses",label:"Crosses"},{value:"disjoint",label:"Disjoint"}];
  const layerOptions=items.map(x=>({value:x.key,label:x.name+" ("+vectorFeatures(x.layer).length+" features)"}));
  const result=await arcMapDialog.form({
    title:"Select By Location",
    message:"Choose the spatial relationship, source and target layers, and selection method.",
    fields:[
      {name:"relationship",label:"Relationship",options:relationships,value:"intersects"},
      {name:"source",label:"Source layer",options:layerOptions,value:items[0].key},
      {name:"target",label:"Target layer",options:layerOptions,value:items[1].key},
      {name:"mode",label:"Selection type",options:[{value:"1",label:"New selection"},{value:"2",label:"Add to current selection"},{value:"3",label:"Remove from current selection"},{value:"4",label:"Select from current selection"}],value:"1"}
    ]
  });
  if(result===null)return;
  const {relationship:relation,source:a,target:b,mode}=result;
  const ai=items.findIndex(x=>x.key===a),bi=items.findIndex(x=>x.key===b);
  if(!items[ai]||!items[bi]||ai===bi){status("Choose different, valid source and target layers.");return;}
  if(!relationships.some(x=>x.value===relation)||!["1","2","3","4"].includes(mode)){status("Choose a valid spatial relationship and selection type.");return;}
  const source=vectorFeatures(items[ai].layer),target=vectorFeatures(items[bi].layer).map(v=>v.feature).filter(f=>f?.geometry);
  if(!target.length){status("The target layer has no readable geometries.");return;}
  const relationFunctions={intersects:"booleanIntersects",within:"booleanWithin",contains:"booleanContains",touches:"booleanTouches",overlaps:"booleanOverlap",crosses:"booleanCrosses"};
  if(relation!=="disjoint"&&typeof turf[relationFunctions[relation]]!=="function"){status("Spatial selection unavailable: Turf does not provide the "+relation+" relationship.");return;}
  if(typeof turf.booleanIntersects!=="function"){status("Spatial selection unavailable: Turf booleanIntersects is missing.");return;}
  const matches=[];
  source.forEach(v=>{
    if(!v.feature?.geometry)return;
    let ok=relation==="disjoint";
    for(const f of target){
      try{
        if(relation==="disjoint"){
          if(turf.booleanIntersects(v.feature,f)){ok=false;break;}
        }else{
          ok=turf[relationFunctions[relation]](v.feature,f);
          if(ok)break;
        }
      }catch(e){ok=false;}
    }
    if(ok)matches.push(v.layer);
  });
  if(mode==="1")selectedFeatures.clear();
  if(mode==="4"){
    const current=new Set(selectedFeatures);
    selectedFeatures.clear();
    matches.forEach(layer=>{if(current.has(layer))selectedFeatures.add(layer);});
  }else if(mode==="3")matches.forEach(layer=>selectedFeatures.delete(layer));
  else matches.forEach(layer=>selectedFeatures.add(layer));
  const all=getFeatureRecords(),first=all.find(r=>selectedFeatures.has(r.layer));
  setSelectedLayer(first?.layer||null);setSelectedOverlay(null);
  if(selectedLayer)renderAttributeEditor();else{const editor=$("attributeEditor");if(editor)editor.style.display="none";}
  applySelectionVisual();
  status(selectedFeatures.size+" feature(s) selected; spatial match count "+matches.length+" using "+relation+".");
}

async function selectByAttribute(){
  const allRecords=getFeatureRecords();
  const vectorLayers=[{name:"My Drawings",layer:drawn},...namedLayers.filter(x=>isVectorLayer(x.layer)).map(x=>({name:x.name,layer:x.layer}))]
    .filter((x,i,arr)=>x.layer&&arr.findIndex(y=>y.layer===x.layer)===i);
  if(!vectorLayers.length||!allRecords.length){status("Select By Attributes requires at least one vector layer with features.");return;}
  const layerFields={};
  vectorLayers.forEach(target=>{
    const records=allRecords.filter(r=>(r.owner?.layer||drawn)===target.layer);
    const schemaFields=(target.layer?._gdbMetadata?.fields||[]).map(f=>typeof f==="string"?f:f?.name).filter(Boolean);
    layerFields[target.name]=[...new Set([...schemaFields,...records.flatMap(r=>Object.keys(r.feature.properties||{}))].filter(k=>!isReservedAttributeField(k)))].map(name=>({value:name,label:name}));
  });
  const usableLayers=vectorLayers.filter(x=>layerFields[x.name].length);
  if(!usableLayers.length){status("The available vector layers have no attribute fields.");return;}
  const current=currentAttributeLayer();
  const defaultLayer=usableLayers.find(x=>x.layer===current)?.name||usableLayers[0].name;
  const operators=["=","!=","contains","starts","ends",">",">=","<","<=","is null","is not null"];
  const result=await arcMapDialog.form({
    title:"Select By Attributes",
    message:"Select the layer, query condition and selection method.",
    fields:[
      {name:"layer",label:"Layer",options:usableLayers.map(x=>({value:x.name,label:x.name})),value:defaultLayer},
      {name:"field",label:"Field",options:layerFields[defaultLayer]},
      {name:"operator",label:"Operator",options:operators.map(op=>({value:op,label:op})),value:"="},
      {name:"value",label:"Value",type:"text",value:""},
      {name:"mode",label:"Selection type",options:[{value:"1",label:"New selection"},{value:"2",label:"Add to current selection"},{value:"3",label:"Remove from current selection"},{value:"4",label:"Select from current selection"}],value:"1"}
    ],
    optionMaps:{field:{dependsOn:"layer",values:layerFields}}
  });
  if(result===null)return;
  const {layer:layerName,field,operator:op,value:val,mode}=result;
  const target=usableLayers.find(x=>x.name===layerName);
  if(!target||!layerFields[layerName].some(x=>x.value===field)){status("Choose a valid layer and field.");return;}
  if(!["1","2","3","4"].includes(mode)){status("Choose a valid selection type.");return;}
  if(["contains","starts","ends"].includes(op)&&val.trim()===""){status("Enter a non-empty value for the text comparison.");return;}
  const records=allRecords.filter(r=>(r.owner?.layer||drawn)===target.layer);
  const needle=val.toLowerCase(),numericNeedle=val.trim()!==""?Number(val):NaN;
  const matches=r=>{
    const raw=r.feature.properties?.[field];
    if(op==="is null")return raw===null||raw===undefined;
    if(op==="is not null")return raw!==null&&raw!==undefined;
    if(raw===null||raw===undefined)return false;
    if(op==="="||op==="!="){
      let equal;
      if(typeof raw==="number"&&Number.isFinite(numericNeedle))equal=raw===numericNeedle;
      else if(typeof raw==="boolean"&&/^(true|false)$/i.test(val.trim()))equal=raw===(val.trim().toLowerCase()==="true");
      else equal=String(typeof raw==="object"?JSON.stringify(raw):raw).toLowerCase()===needle;
      return op==="="?equal:!equal;
    }
    if(op==="contains")return String(typeof raw==="object"?JSON.stringify(raw):raw).toLowerCase().includes(needle);
    if(op==="starts")return String(typeof raw==="object"?JSON.stringify(raw):raw).toLowerCase().startsWith(needle);
    if(op==="ends")return String(typeof raw==="object"?JSON.stringify(raw):raw).toLowerCase().endsWith(needle);
    if(String(raw).trim()===""||!Number.isFinite(Number(raw))||!Number.isFinite(numericNeedle))return false;
    const n=Number(raw);
    return op===">"?n>numericNeedle:op===">="?n>=numericNeedle:op==="<"?n<numericNeedle:n<=numericNeedle;
  };
  const matched=records.filter(matches);
  const targetLayers=new Set(records.map(r=>r.layer));
  if(mode==="1")selectedFeatures.clear();
  if(mode==="4"){
    const currentSelection=new Set(selectedFeatures);
    targetLayers.forEach(layer=>selectedFeatures.delete(layer));
    matched.forEach(r=>{if(currentSelection.has(r.layer))selectedFeatures.add(r.layer);});
  }else if(mode==="3")matched.forEach(r=>selectedFeatures.delete(r.layer));
  else matched.forEach(r=>selectedFeatures.add(r.layer));
  const first=allRecords.find(r=>selectedFeatures.has(r.layer));
  setSelectedLayer(first?.layer||null);setSelectedOverlay(null);
  if(selectedLayer)renderAttributeEditor();else{const editor=$("attributeEditor");if(editor)editor.style.display="none";}
  applySelectionVisual();
  status(matched.length+" feature(s) matched in "+target.name+"; "+selectedFeatures.size+" feature(s) selected.");
}

    function featureDistanceMeters(a,b){
      if(!a?.geometry||!b?.geometry)return Infinity;
      try{if(turf.booleanIntersects(a,b))return 0;}catch(_){}
      const collectCoordinates=(geometry,out)=>{
        if(!geometry)return;
        if(geometry.type==="GeometryCollection"){(geometry.geometries||[]).forEach(g=>collectCoordinates(g,out));return;}
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

    function createInteractiveSelectionController(deps={}){
      const required=["getMap","getLeaflet","getActiveMapTool","getSelectedFeatures","getSelectedLayer","setSelectedLayer","getSelectedOverlay","setSelectedOverlay","updateStandardActionButtons","renderAttributeEditor","applySelectionVisual","status","$","esc","asFeature","identifyTool","showUTM","calcMeasure","synchronizeFeatureSelection","getWindow"];
      const missing=required.filter(name=>typeof deps[name]!=="function");
      if(missing.length)throw new Error("Interactive selection controller is missing dependencies: "+missing.join(", "));
      if(!deps.document||typeof deps.document.querySelectorAll!=="function")throw new Error("Interactive selection controller requires a document.");
      const read=name=>deps[name]();
      function updateMultiSelectionUI(){
        const selected=read("getSelectedFeatures");
        allSelectableFeatureLayers().forEach(entry=>vectorFeatures(entry.layer).forEach(record=>{
          const layer=record.layer;if(!layer)return;
          const isSelected=selected.has(layer);
          if(typeof layer.setStyle==="function"){
            if(isSelected){
              if(!layer._webgisPreSelectionStyle){
                const options=layer.options||{};layer._webgisPreSelectionStyle={};
                ["color","weight","opacity","fillColor","fillOpacity","dashArray","lineCap","lineJoin"].forEach(key=>{
                  if(Object.prototype.hasOwnProperty.call(options,key))layer._webgisPreSelectionStyle[key]=options[key];
                });
              }
              layer.setStyle({color:"#ff8c00",weight:5,opacity:1,fillColor:"#ffd54f",fillOpacity:.72});
            }else if(layer._webgisPreSelectionStyle){
              layer.setStyle(layer._webgisPreSelectionStyle);delete layer._webgisPreSelectionStyle;
            }
          }
          const element=(typeof layer.getElement==="function"&&layer.getElement())||layer._path||layer._icon;
          if(element)element.classList.toggle("multi-selected-feature",isSelected);
        }));
        deps.document.querySelectorAll(".multi-selected-feature").forEach(element=>{
          const owner=selected.has(element._leaflet_layer)?element._leaflet_layer:null;
          if(!owner&&!element.classList.contains("leaflet-marker-icon"))element.classList.remove("multi-selected-feature");
        });
        const box=deps.$("multiFeatureEditor"),info=deps.$("multiSelectionInfo");
        if(info)info.textContent=selected.size+" Feature Selection selected";
        if(box)box.style.display=selected.size?"block":"none";
        deps.updateStandardActionButtons();
      }
      function clearMultiSelection(){
        const selected=read("getSelectedFeatures");
        selected.forEach(layer=>{
          const element=layer&&((typeof layer.getElement==="function"&&layer.getElement())||layer._path||layer._icon);
          if(element)read("getLeaflet").DomUtil.removeClass(element,"multi-selected-feature");
        });
        selected.clear();deps.setSelectedLayer(null);deps.setSelectedOverlay(null);
        const editor=deps.$("attributeEditor");if(editor)editor.style.display="none";
        deps.applySelectionVisual();
      }
      function toggleMultiSelection(layer){
        const selected=read("getSelectedFeatures"),wasSelected=selected.has(layer);
        if(wasSelected)selected.delete(layer);else selected.add(layer);
        if(!wasSelected)deps.setSelectedLayer(layer);
        else{
          if(deps.getSelectedLayer()===layer)deps.setSelectedLayer([...selected].pop()||null);
          if(deps.getSelectedOverlay()===layer)deps.setSelectedOverlay(null);
        }
        updateMultiSelectionUI();deps.renderAttributeEditor();
      }
      function featureLabel(layer){
        try{
          const properties=layer.feature&&layer.feature.properties||{};
          if(properties.name)return String(properties.name);
          const keys=Object.keys(properties);if(keys.length)return String(properties[keys[0]]);
        }catch(_){}
        return "Selected feature";
      }
      function setGisSelection(layer,second){
        if(second)deps.setSelectedOverlay(layer);
        else{deps.setSelectedOverlay(null);deps.setSelectedLayer(layer);deps.renderAttributeEditor();}
        const primary=deps.getSelectedLayer()?featureLabel(deps.getSelectedLayer()):"—";
        const overlay=deps.getSelectedOverlay()?featureLabel(deps.getSelectedOverlay()):"—";
        const element=deps.$("gisSelectionInfo");
        if(element)element.innerHTML="Input feature: "+deps.esc(primary)+"<br>Overlay feature: "+deps.esc(overlay);
      }
      function handleFeatureSelectionClick(layer,event){
        if(!layer)return;
        const originalEvent=event?.originalEvent;
        if(originalEvent){
          if(originalEvent.__webgisSelectionHandled)return;
          originalEvent.__webgisSelectionHandled=true;
        }
        const activeTool=deps.getActiveMapTool();
        if(activeTool==="identify"){
          deps.setSelectedOverlay(layer);if(!layer.feature)layer.feature=deps.asFeature(layer);
          deps.identifyTool();return;
        }
        if(activeTool==="hyperlink"){
          const feature=layer.feature||deps.asFeature(layer),properties=feature?.properties||{};
          const value=Object.entries(properties).find(([key,val])=>/(url|link|website|href|hyperlink)/i.test(key)&&/^https?:/i.test(String(val||""))&&String(val||"").includes("://"));
          if(!value){deps.status("This feature has no HTTP or HTTPS link attribute.");return;}
          deps.getWindow().open(value[1],"_blank","noopener,noreferrer");deps.status("Feature link opened.");return;
        }
        if(activeTool==="htmlPopup"){
          const feature=layer.feature||deps.asFeature(layer),properties=feature?.properties||{};
          const rows=Object.entries(properties).map(([key,val])=>"<tr><th>"+deps.esc(key)+"</th><td>"+deps.esc(val)+"</td></tr>").join("");
          layer.bindPopup("<table class='identify-popup'>"+(rows||"<tr><td>No attributes</td></tr>")+"</table>").openPopup(event?.latlng);
          deps.setSelectedOverlay(layer);deps.status("Feature attributes opened.");return;
        }
        const selected=read("getSelectedFeatures"),multi=!!(originalEvent&&(originalEvent.ctrlKey||originalEvent.metaKey||originalEvent.shiftKey));
        if(multi){
          toggleMultiSelection(layer);
          if(originalEvent.shiftKey&&!originalEvent.ctrlKey&&!originalEvent.metaKey){
            deps.setSelectedOverlay(selected.has(layer)?layer:null);
            if(!deps.getSelectedLayer()&&deps.getSelectedOverlay())deps.setSelectedLayer([...selected].find(item=>item!==deps.getSelectedOverlay())||deps.getSelectedOverlay());
            deps.synchronizeFeatureSelection({primary:deps.getSelectedLayer(),overlay:deps.getSelectedOverlay(),renderEditor:true});
            deps.status(selected.size+" feature(s) selected; Shift-click a selected feature to designate the overlay.");
          }else{
            if(deps.getSelectedOverlay()&&!selected.has(deps.getSelectedOverlay()))deps.setSelectedOverlay(null);
            deps.synchronizeFeatureSelection({primary:deps.getSelectedLayer(),overlay:deps.getSelectedOverlay(),renderEditor:true});
            deps.status(selected.size+" feature(s) selected.");
          }
        }else{
          selected.clear();selected.add(layer);
          deps.synchronizeFeatureSelection({primary:layer,renderEditor:true});
          deps.showUTM(layer);deps.calcMeasure(layer);
          deps.status("Feature selected. Ctrl-click or Shift-click to add features to the selection.");
        }
      }
      function attachFeatureSelection(layer){
        if(!layer||!layer.on)return;
        if(typeof layer.options==="object")layer.options.interactive=true;
        const element=(typeof layer.getElement==="function"&&layer.getElement())||layer._path||layer._icon;
        if(element)element.style.pointerEvents="auto";
        if(!layer._webgisSelectionHandlerAttached){
          layer._webgisSelectionHandlerAttached=true;
          layer.on("click",event=>handleFeatureSelectionClick(layer,event));
        }
      }
      function attachMapSelectionFallback(){
        const map=deps.getMap();
        if(!map||map._webgisSelectionFallback)return false;
        map._webgisSelectionFallback=true;
        map.on("click",event=>{
          if(event?.originalEvent?.__webgisSelectionHandled)return;
          const target=event?.originalEvent?.target;if(!target||!target.closest)return;
          const hit=target.closest(".leaflet-interactive,.leaflet-marker-icon");if(!hit)return;
          for(const entry of allSelectableFeatureLayers()){
            for(const record of vectorFeatures(entry.layer)){
              const layer=record.layer;
              const element=(typeof layer.getElement==="function"&&layer.getElement())||layer._path||layer._icon;
              if(element===hit||element?.contains?.(hit)){handleFeatureSelectionClick(layer,event);return;}
            }
          }
        });
        return true;
      }
      attachMapSelectionFallback();
      return Object.freeze({updateMultiSelectionUI,clearMultiSelection,toggleMultiSelection,setGisSelection,featureLabel,handleFeatureSelectionClick,attachFeatureSelection,attachMapSelectionFallback});
    }

    return Object.freeze({createInteractiveSelectionController,featureDistanceMeters,allSelectableFeatureLayers,getFeatureRecords,selectedRecords,selectByLocation,selectByAttribute});
  }
  root.SabzWebGISSelection={create};
})(window);
