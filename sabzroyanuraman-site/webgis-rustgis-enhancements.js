/* Sabz Royan WebGIS ModelBuilder canvas and Layout editing enhancement. */
(function () {
  "use strict";
  const STYLE_ID = "sabz-rustgis-package-style";
  if (!document.getElementById(STYLE_ID)) {
    const style = document.createElement("style");
    style.id = STYLE_ID;
    style.textContent = [
      "#gisModelBuilder [data-mb-graph]{overflow:auto!important;min-height:260px!important;height:clamp(260px,38vh,440px)!important;cursor:grab;touch-action:pan-x pan-y}",
      "#gisModelBuilder [data-mb-graph].sabz-mb-panning{cursor:grabbing}",
      "#gisModelBuilder [data-mb-graph] [data-sabz-mb-node]{cursor:grab!important;user-select:none}",
      "#gisModelBuilder [data-mb-graph] [data-sabz-mb-node].sabz-mb-dragging{cursor:grabbing!important;z-index:5!important;box-shadow:0 5px 18px #17212b35!important;outline:2px solid #3b82a0}",
      "#webgisLayoutView .sabz-layout-selected{outline:2px solid #2684b7!important;outline-offset:2px;resize:both;overflow:auto;min-width:34px;min-height:20px}",
      "#webgisLayoutView.layout-move-mode .webgis-layout-item{touch-action:none}",
      "#webgisLayoutView .webgis-layout-item[hidden]{display:none!important}"
    ].join("");
    document.head.appendChild(style);
  }
  function installModelBuilderCanvas() {
    const original = window.renderModelBuilderGraph;
    if (typeof original !== "function" || original.__sabzEnhanced) return;
    function enhancedRender(modal) {
      original(modal);
      const graph = modal && modal.querySelector("[data-mb-graph]");
      const host = modal && modal.querySelector("[data-mb-steps]");
      const canvas = graph && graph.firstElementChild;
      if (!graph || !host || !canvas) return;
      const rows = Array.from(host.children);
      const nodeEls = Array.from(canvas.children).filter(el => el.tagName === "DIV");
      if (nodeEls.length !== rows.length + 2) return;
      modal._sabzMbPositions = modal._sabzMbPositions || Object.create(null);
      const positions = modal._sabzMbPositions;
      const ids = ["source", ...rows.map(row => row.dataset.mbNodeId), "output"];
      nodeEls.forEach((el, index) => {
        const id = ids[index];
        el.dataset.sabzMbNode = id;
        el.setAttribute("data-sabz-mb-node", "1");
        const left = Number.parseFloat(el.style.left) || 0;
        const top = Number.parseFloat(el.style.top) || 0;
        if (!positions[id]) positions[id] = { left, top };
        el.style.left = positions[id].left + "px";
        el.style.top = positions[id].top + "px";
        el.style.cursor = index > 0 && index < nodeEls.length - 1 ? "grab" : "default";
      });
      const svg = Array.from(canvas.children).find(el => el.tagName === "svg");
      if (!svg) return;
      const ns = "http://www.w3.org/2000/svg";
      const defs = svg.querySelector("defs");
      Array.from(svg.children).forEach(el => { if (el !== defs) el.remove(); });
      const center = el => ({
        x: (Number.parseFloat(el.style.left) || 0) + el.offsetWidth / 2,
        y: (Number.parseFloat(el.style.top) || 0) + el.offsetHeight / 2
      });
      const edge = (from, to) => {
        if (!from || !to) return;
        const a = center(from), b = center(to), path = document.createElementNS(ns, "path");
        const bend = Math.max(35, Math.abs(b.x - a.x) * 0.42);
        const startX = a.x + (b.x >= a.x ? from.offsetWidth / 2 : -from.offsetWidth / 2);
        const endX = b.x + (b.x >= a.x ? -to.offsetWidth / 2 : to.offsetWidth / 2);
        path.setAttribute("d", "M" + startX + " " + a.y + " C" + (startX + bend) + " " + a.y + " " + (endX - bend) + " " + b.y + " " + endX + " " + b.y);
        path.setAttribute("fill", "none");
        path.setAttribute("stroke", "#64849b");
        path.setAttribute("stroke-width", "2.5");
        path.setAttribute("marker-end", "url(#mbArrowHead)");
        svg.appendChild(path);
      };
      const redrawEdges = () => {
        Array.from(svg.querySelectorAll("path")).forEach(el => el.remove());
        const lookup = key => nodeEls.find(el => el.dataset.sabzMbNode === key);
        rows.forEach(row => {
          edge(lookup(row.querySelector("[data-mb-input]")?.value || "source"), lookup(row.dataset.mbNodeId));
          const second = row.querySelector("[data-mb-overlay-node]")?.value || "";
          if (second) edge(lookup(second), lookup(row.dataset.mbNodeId));
        });
        edge(lookup(modal.querySelector("[data-mb-output]")?.value || rows[rows.length - 1]?.dataset.mbNodeId), lookup("output"));
      };
      redrawEdges();
      if (canvas.dataset.sabzInteractions === "1") return;
      canvas.dataset.sabzInteractions = "1";
      let dragging = null;
      canvas.addEventListener("pointerdown", event => {
        const node = event.target.closest("[data-sabz-mb-node]");
        if (!node || event.button !== 0) return;
        event.preventDefault();
        dragging = { node, pointerId: event.pointerId, startX: event.clientX, startY: event.clientY,
          left: Number.parseFloat(node.style.left) || 0, top: Number.parseFloat(node.style.top) || 0,
          scale: modal._sabzMbZoom || 1 };
        node.classList.add("sabz-mb-dragging");
        node.setPointerCapture?.(event.pointerId);
      });
      canvas.addEventListener("pointermove", event => {
        if (!dragging || event.pointerId !== dragging.pointerId) return;
        const left = Math.max(0, dragging.left + (event.clientX - dragging.startX) / dragging.scale);
        const top = Math.max(0, dragging.top + (event.clientY - dragging.startY) / dragging.scale);
        dragging.node.style.left = left + "px";
        dragging.node.style.top = top + "px";
        positions[dragging.node.dataset.sabzMbNode] = { left, top };
        redrawEdges();
      });
      const stopDrag = event => {
        if (!dragging || (event && event.pointerId !== dragging.pointerId)) return;
        dragging.node.classList.remove("sabz-mb-dragging");
        dragging = null;
      };
      canvas.addEventListener("pointerup", stopDrag);
      canvas.addEventListener("pointercancel", stopDrag);
      if (graph.dataset.sabzViewportInstalled !== "1") {
        graph.dataset.sabzViewportInstalled = "1";
        let pan = null;
        graph.addEventListener("pointerdown", event => {
          if (event.target.closest("[data-sabz-mb-node]")) return;
          if (!(event.button === 1 || (event.button === 0 && event.target === graph))) return;
          event.preventDefault();
          pan = { x: event.clientX, y: event.clientY, left: graph.scrollLeft, top: graph.scrollTop, id: event.pointerId };
          graph.classList.add("sabz-mb-panning");
          graph.setPointerCapture?.(event.pointerId);
        });
        graph.addEventListener("pointermove", event => {
          if (!pan || event.pointerId !== pan.id) return;
          graph.scrollLeft = pan.left - (event.clientX - pan.x);
          graph.scrollTop = pan.top - (event.clientY - pan.y);
        });
        const stopPan = event => {
          if (!pan || (event && event.pointerId !== pan.id)) return;
          pan = null;
          graph.classList.remove("sabz-mb-panning");
        };
        graph.addEventListener("pointerup", stopPan);
        graph.addEventListener("pointercancel", stopPan);
        graph.addEventListener("wheel", event => {
          if (!event.ctrlKey) return;
          event.preventDefault();
          const current = modal._sabzMbZoom || 1;
          const next = Math.max(0.55, Math.min(1.8, current * (event.deltaY < 0 ? 1.08 : 0.92)));
          if (Math.abs(next - current) < 0.001) return;
          modal._sabzMbZoom = next;
          canvas.style.transformOrigin = "0 0";
          canvas.style.transform = "scale(" + next + ")";
          graph.style.backgroundSize = (20 * next) + "px " + (20 * next) + "px";
        }, { passive: false });
      }
    }
    enhancedRender.__sabzEnhanced = true;
    window.renderModelBuilderGraph = enhancedRender;
  }
  function installLayoutSelection(panel) {
    if (!panel || panel.dataset.sabzSelectionInstalled === "1") return;
    panel.dataset.sabzSelectionInstalled = "1";
    panel.addEventListener("click", event => {
      const item = event.target.closest(".webgis-layout-item");
      if (!item || item.hidden || event.target.isContentEditable) return;
      panel.querySelectorAll(".sabz-layout-selected").forEach(el => el.classList.remove("sabz-layout-selected"));
      item.classList.add("sabz-layout-selected");
      panel._selectedLayoutItem = item;
    }, true);
    panel.addEventListener("keydown", event => {
      if (!panel.classList.contains("layout-move-mode")) return;
      const item = panel._selectedLayoutItem;
      if (!item || !["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight"].includes(event.key)) return;
      if (event.target.isContentEditable || /INPUT|TEXTAREA|SELECT/.test(event.target.tagName)) return;
      event.preventDefault();
      const step = event.shiftKey ? 10 : 1;
      const left = Number.parseFloat(item.style.left) || item.offsetLeft || 0;
      const top = Number.parseFloat(item.style.top) || item.offsetTop || 0;
      if (event.key === "ArrowLeft") item.style.left = Math.max(0, left - step) + "px";
      if (event.key === "ArrowRight") item.style.left = left + step + "px";
      if (event.key === "ArrowUp") item.style.top = Math.max(0, top - step) + "px";
      if (event.key === "ArrowDown") item.style.top = top + step + "px";
    });
  }
  function watchLayout() {
    const observer = new MutationObserver(() => {
      const panel = document.getElementById("webgisLayoutView");
      if (panel) installLayoutSelection(panel);
    });
    observer.observe(document.body, { childList: true, subtree: true });
  }
  installModelBuilderCanvas();
  watchLayout();
  const ready = () => {
    installModelBuilderCanvas();
    const panel = document.getElementById("webgisLayoutView");
    if (panel) installLayoutSelection(panel);
  };
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", ready, { once: true });
  else ready();
})();