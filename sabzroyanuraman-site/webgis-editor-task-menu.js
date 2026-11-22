/* ArcMap Editor task menu wiring. */
(function(root){
  "use strict";
  function init(){
    const toggle=document.getElementById("editorToolbarToggleBtn");
    const menu=document.getElementById("editorTaskMenu");
    if(!toggle||!menu)return;
    toggle.title="Editor Toolbar — click to open Editor tasks";
    toggle.addEventListener("click",function(e){
      e.preventDefault();e.stopPropagation();
      const r=toggle.getBoundingClientRect();
      menu.style.left=Math.min(r.left,window.innerWidth-230)+"px";
      menu.style.top=Math.min(r.bottom+3,window.innerHeight-360)+"px";
      menu.classList.toggle("open");
      toggle.classList.toggle("active",menu.classList.contains("open"));
    });
    menu.addEventListener("click",function(e){
      const b=e.target.closest("[data-editor-task]");
      if(!b)return;
      const actions={start:"startEditingBtn",save:"saveEditsBtn",stop:"stopEditingBtn",attributes:"openTable",clear:null};
      const draw={point:"Marker",line:"Polyline",polygon:"Polygon",rectangle:"Rectangle",circle:"Circle",ellipse:"Ellipse",freehandLine:"FreehandLine",freehandPolygon:"FreehandPolygon"};
      const task=b.dataset.editorTask;
      if(draw[task])document.querySelector('[data-editor-draw="'+draw[task]+'"]')?.click();
      else if(task==="clear")document.querySelector('[data-action="clearDraw"]')?.click();
      else document.getElementById(actions[task])?.click();
      menu.classList.remove("open");toggle.classList.remove("active");
    });
    document.addEventListener("click",e=>{
      if(!menu.contains(e.target)&&e.target!==toggle){
        menu.classList.remove("open");toggle.classList.remove("active");
      }
    });
    document.addEventListener("keydown",e=>{
      if(e.key==="Escape"){menu.classList.remove("open");toggle.classList.remove("active");}
    });
  }
  root.SabzWebGISEditorTaskMenu=Object.freeze({init});
})(typeof window!=="undefined"?window:globalThis);
