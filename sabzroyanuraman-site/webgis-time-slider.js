(function(root){
  "use strict";
  function create({getFeatureRecords,document,status}){
    if(typeof getFeatureRecords!=="function"||!document||typeof status!=="function")throw new Error("Time slider dependencies are required.");
    return function startTimeSlider(){
      const records=getFeatureRecords()||[];
      if(!records.length){status("Add a vector layer with date or time attributes first.");return;}
      const candidates=[...new Set(records.flatMap(r=>Object.keys(r?.feature?.properties||{})))]
        .filter(field=>/(date|time|year)/i.test(field))
        .map(field=>({field,dates:records.map(r=>Date.parse(r?.feature?.properties?.[field])).filter(Number.isFinite)}))
        .filter(item=>item.dates.length);
      if(!candidates.length){status("No date or time fields were found in the feature attributes.");return;}
      const existing=document.getElementById("timeSliderPanel");
      if(existing){const close=existing.querySelector("button");if(close)close.click();else existing.remove();}
      const panel=document.createElement("div");panel.id="timeSliderPanel";panel.className="time-slider-panel";
      const select=document.createElement("select");
      candidates.forEach(candidate=>{const option=document.createElement("option");option.value=candidate.field;option.textContent=candidate.field;select.appendChild(option);});
      const range=document.createElement("input");range.type="range";range.min="0";range.max="1000";range.value="1000";
      const label=document.createElement("span");label.className="time-slider-label";
      const close=document.createElement("button");close.type="button";close.textContent="Close";
      panel.append("Time field: ",select,range,label,close);document.body.appendChild(panel);
      let closed=false;
      const restoreStyles=()=>{
        if(closed)return;closed=true;
        records.forEach(record=>{
          if(record?._timeStyle&&record.layer&&typeof record.layer.setStyle==="function")record.layer.setStyle(record._timeStyle);
          if(record)delete record._timeStyle;
        });
        panel.remove();
      };
      const apply=()=>{
        if(closed)return;
        const candidate=candidates.find(item=>item.field===select.value);if(!candidate)return;
        const min=Math.min(...candidate.dates),max=Math.max(...candidate.dates);
        const cutoff=min+(max-min)*(Number(range.value)/1000);
        label.textContent="Through "+new Date(cutoff).toLocaleDateString();
        records.forEach(record=>{
          const layer=record?.layer;if(!layer||typeof layer.setStyle!=="function")return;
          const date=Date.parse(record?.feature?.properties?.[select.value]);
          if(!record._timeStyle)record._timeStyle={...(layer.options||{})};
          const visible=Number.isFinite(date)&&date<=cutoff;
          layer.setStyle({opacity:visible?(record._timeStyle.opacity??1):0,fillOpacity:visible?(record._timeStyle.fillOpacity??.2):0});
        });
      };
      select.addEventListener("change",apply);range.addEventListener("input",apply);close.addEventListener("click",restoreStyles);
      apply();status("Time slider ready.");
    };
  }
  root.SabzWebGISTimeSlider={create};
})(window);
