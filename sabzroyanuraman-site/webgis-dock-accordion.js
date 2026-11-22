/* ArcMap-style GIS dock accordion navigation. */
(function(root){
  "use strict";
  function init(){
    const sections=[...document.querySelectorAll(".accordion-section")];
    sections.forEach(section=>{
      const head=section.querySelector(".accordion-head");
      head?.addEventListener("click",()=>{
        const wasOpen=section.classList.contains("open");
        sections.forEach(item=>item.classList.remove("open"));
        if(!wasOpen)section.classList.add("open");
      });
    });
    const openSection=name=>{
      const section=document.querySelector('.accordion-section[data-accordion="'+name+'"]');
      if(section){
        sections.forEach(item=>item.classList.remove("open"));
        section.classList.add("open");
        section.scrollIntoView({block:"nearest"});
      }
    };
    root.openWebGisPanelSection=openSection;
  }
  root.SabzWebGISDockAccordion=Object.freeze({init});
})(typeof window!=="undefined"?window:globalThis);
