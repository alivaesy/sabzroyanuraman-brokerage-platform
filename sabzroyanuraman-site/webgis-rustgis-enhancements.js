/* Sabz Royan WebGIS: ModelBuilder free-canvas and Layout editing layer. */
(function () {
  "use strict";
  const installRenderer = window.__sabzInstallModelBuilderRenderer;
  if (typeof installRenderer === "function") installRenderer(original => {
    if (typeof original !== "function" || original.__sabzEnhanced) return original;
    function enhanced(modal) {
      original(modal);
      const graph = modal?.querySelector("[data-mb-graph]");
      const host = modal?.querySelector("[data-mb-steps]");
      const canvas = graph?.firstElementChild;
      if (!graph || !host || !canvas) return;
      const rows = Array.from(host.children);
      const nodes = Array.from(canvas.children).filter(el => el.tagName === "DIV");
      if (nodes.length !== rows.length + 2) return;
      const ids = ["source", ...rows.map(row => row.dataset.mbNodeId), "output"];
      const positions = modal._sabzMbPositions ||= Object.create(null);
      nodes.forEach((node, i) => {
        const id = ids[i];
        node.dataset.sabzMbNode = id;
        const left = parseFloat(node.style.left) || 0, top = parseFloat(node.style.top) || 0;
        positions[id] ||= { left, top };
        node.style.left = positions[id].left + "px";
        node.style.top = positions[id].top + "px";
        node.style.cursor = i > 0 && i < nodes.length - 1 ? "grab" : "default";
      });
      const svg = Array.from(canvas.children).find(el => el.tagName === "svg");
      if (!svg) return;
      const defs = svg.querySelector("defs"), ns = "http://www.w3.org/2000/svg";
      const center = n => ({ x: parseFloat(n.style.left) + n.offsetWidth / 2, y: parseFloat(n.style.top) + n.offsetHeight / 2 });
      const lookup = id => nodes.find(n => n.dataset.sabzMbNode === id);
      const drawEdge = (from, to) => {
        if (!from || !to) return;
        const a = center(from), b = center(to), path = document.createElementNS(ns, "path");
        const start = a.x + (b.x >= a.x ? from.offsetWidth / 2 : -from.offsetWidth / 2);
        const end = b.x + (b.x >= a.x ? -to.offsetWidth / 2 : to.offsetWidth / 2);
        const bend = Math.max(35, Math.abs(end - start) * .42);
        path.setAttribute("d", "M"+start+" "+a.y+" C"+(start+bend)+" "+a.y+" "+(end-bend)+" "+b.y+" "+end+" "+b.y);
        path.setAttribute("fill", "none"); path.setAttribute("stroke", "#64849b");
        path.setAttribute("stroke-width", "2.5"); path.setAttribute("marker-end", "url(#mbArrowHead)");
        svg.appendChild(path);
      };
      const redraw = () => {
        svg.querySelectorAll("path").forEach(p => p.remove());
        rows.forEach(row => {
          drawEdge(lookup(row.querySelector("[data-mb-input]")?.value || "source"), lookup(row.dataset.mbNodeId));
          const secondary = row.querySelector("[data-mb-overlay-node]")?.value || "";
          if (secondary) drawEdge(lookup(secondary), lookup(row.dataset.mbNodeId));
        });
        drawEdge(lookup(modal.querySelector("[data-mb-output]")?.value || rows.at(-1)?.dataset.mbNodeId), lookup("output"));
      };
      redraw();
      if (canvas.dataset.sabzDragInstalled === "1") return;
      canvas.dataset.sabzDragInstalled = "1";
      let drag = null;
      canvas.addEventListener("pointerdown", e => {
        const node = e.target.closest("[data-sabz-mb-node]");
        if (!node || e.button !== 0) return;
        e.preventDefault();
        drag = { node, id:e.pointerId, x:e.clientX, y:e.clientY,
          left:parseFloat(node.style.left)||0, top:parseFloat(node.style.top)||0, zoom:modal._sabzMbZoom||1 };
        node.classList.add("sabz-mb-dragging");
        node.setPointerCapture?.(e.pointerId);
      });
      canvas.addEventListener("pointermove", e => {
        if (!drag || drag.id !== e.pointerId) return;
        const left=Math.max(0,drag.left+(e.clientX-drag.x)/drag.zoom);
        const top=Math.max(0,drag.top+(e.clientY-drag.y)/drag.zoom);
        drag.node.style.left=left+"px"; drag.node.style.top=top+"px";
        positions[drag.node.dataset.sabzMbNode]={left,top}; redraw();
      });
      const stopDrag=e=>{if(!drag||(e&&e.pointerId!==drag.id))return;drag.node.classList.remove("sabz-mb-dragging");drag=null;};
      canvas.addEventListener("pointerup",stopDrag); canvas.addEventListener("pointercancel",stopDrag);
      if (graph.dataset.sabzViewportInstalled !== "1") {
        graph.dataset.sabzViewportInstalled = "1";
        let pan=null;
        graph.addEventListener("pointerdown",e=>{
          if(e.target.closest("[data-sabz-mb-node]"))return;
          if(!(e.button===1||(e.button===0&&e.target===graph)))return;
          e.preventDefault();pan={x:e.clientX,y:e.clientY,left:graph.scrollLeft,top:graph.scrollTop,id:e.pointerId};
          graph.classList.add("sabz-mb-panning");graph.setPointerCapture?.(e.pointerId);
        });
        graph.addEventListener("pointermove",e=>{
          if(!pan||pan.id!==e.pointerId)return;
          graph.scrollLeft=pan.left-(e.clientX-pan.x);graph.scrollTop=pan.top-(e.clientY-pan.y);
        });
        const stopPan=e=>{if(!pan||(e&&e.pointerId!==pan.id))return;pan=null;graph.classList.remove("sabz-mb-panning");};
        graph.addEventListener("pointerup",stopPan);graph.addEventListener("pointercancel",stopPan);
        graph.addEventListener("wheel",e=>{
          if(!e.ctrlKey)return;e.preventDefault();
          const old=modal._sabzMbZoom||1,next=Math.max(.55,Math.min(1.8,old*(e.deltaY<0?1.08:.92)));
          if(Math.abs(next-old)<.001)return;modal._sabzMbZoom=next;
          canvas.style.transformOrigin="0 0";canvas.style.transform="scale("+next+")";
          graph.style.backgroundSize=(20*next)+"px "+(20*next)+"px";
        },{passive:false});
      }
    }
    enhanced.__sabzEnhanced = true;
    return enhanced;
  });

  const style=document.createElement("style");
  style.id="sabz-rustgis-package-style";
  style.textContent=[
    "#gisModelBuilder [data-mb-graph]{overflow:auto!important;min-height:260px!important;height:clamp(260px,38vh,440px)!important;cursor:grab}",
    "#gisModelBuilder [data-mb-graph].sabz-mb-panning{cursor:grabbing}",
    "#gisModelBuilder [data-sabz-mb-node]{user-select:none}",
    "#gisModelBuilder [data-sabz-mb-node].sabz-mb-dragging{cursor:grabbing!important;z-index:5!important;box-shadow:0 5px 18px #17212b35!important;outline:2px solid #3b82a0}",
    "#webgisLayoutView .sabz-layout-selected{outline:2px solid #2684b7!important;outline-offset:2px;resize:both;overflow:auto;min-width:34px;min-height:20px}",
    "#webgisLayoutView .webgis-layout-item[hidden]{display:none!important}"
  ].join("");
  document.head.appendChild(style);

  function bindLayout(panel){
    if(!panel||panel.dataset.sabzSelectionInstalled==="1")return;
    panel.dataset.sabzSelectionInstalled="1";
    panel.addEventListener("click",e=>{
      const item=e.target.closest(".webgis-layout-item");
      if(!item||item.hidden||e.target.isContentEditable)return;
      panel.querySelectorAll(".sabz-layout-selected").forEach(el=>el.classList.remove("sabz-layout-selected"));
      item.classList.add("sabz-layout-selected");panel._selectedLayoutItem=item;
    },true);
    panel.addEventListener("keydown",e=>{
      if(!panel.classList.contains("layout-move-mode"))return;
      const item=panel._selectedLayoutItem;
      if(!item||!["ArrowUp","ArrowDown","ArrowLeft","ArrowRight"].includes(e.key))return;
      if(e.target.isContentEditable||/INPUT|TEXTAREA|SELECT/.test(e.target.tagName))return;
      e.preventDefault();const d=e.shiftKey?10:1;
      const x=parseFloat(item.style.left)||item.offsetLeft||0,y=parseFloat(item.style.top)||item.offsetTop||0;
      if(e.key==="ArrowLeft")item.style.left=Math.max(0,x-d)+"px";
      if(e.key==="ArrowRight")item.style.left=x+d+"px";
      if(e.key==="ArrowUp")item.style.top=Math.max(0,y-d)+"px";
      if(e.key==="ArrowDown")item.style.top=y+d+"px";
    });
  }
  const observer=new MutationObserver(()=>{
    const panel=document.getElementById("webgisLayoutView");if(panel)bindLayout(panel);
  });
  observer.observe(document.body,{childList:true,subtree:true});
  bindLayout(document.getElementById("webgisLayoutView"));
})();