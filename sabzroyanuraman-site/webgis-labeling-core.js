/* Pure ArcMap-style label configuration and text helpers. */
(function (root) {
  "use strict";

  function normalizeLabelConfig(config = {}) {
    const source = config && typeof config === "object" ? config : {};
    const color = /^#[0-9a-f]{6}$/i.test(source.color || "") ? source.color : "#202020";
    return Object.freeze({
      enabled: source.enabled === true,
      field: typeof source.field === "string" ? source.field : "",
      size: Math.max(8, Math.min(32, Number(source.size) || 12)),
      color,
      placement: ["auto", "center", "top", "right", "bottom", "left"].includes(source.placement) ? source.placement : "auto",
      minZoom: Math.max(0, Math.min(22, Number.isFinite(Number(source.minZoom)) ? Number(source.minZoom) : 0)),
      density: ["sparse", "balanced", "dense"].includes(source.density) ? source.density : "balanced"
    });
  }

  function labelPlacement(geometryType, preference = "auto") {
    const allowed = ["center", "top", "right", "bottom", "left"];
    const direction = allowed.includes(preference) ? preference :
      (geometryType === "Point" || geometryType === "MultiPoint" ? "top" : "center");
    const offsets = { center: [0, 0], top: [0, -8], right: [8, 0], bottom: [0, 8], left: [-8, 0] };
    return Object.freeze({ direction, offset: offsets[direction] });
  }

  function labelText(value) {
    if (value === null || value === undefined || String(value).trim() === "") return null;
    const text = typeof value === "object" ? JSON.stringify(value) : String(value);
    return text === undefined || text.trim() === "" ? null : text;
  }

  function labelClassCss(className, config) {
    const id = String(className || "gis-feature-label").replace(/[^a-z0-9_-]/gi, "");
    const normalized = normalizeLabelConfig({ enabled: true, ...config });
    return "." + id + "{color:" + normalized.color +
      "!important;font-size:" + normalized.size +
      "px!important;font-weight:600;background:transparent!important;" +
      "border:0!important;border-radius:0;padding:0;box-shadow:none;" +
      "text-shadow:-1px -1px 0 #fff,1px -1px 0 #fff,-1px 1px 0 #fff,1px 1px 0 #fff}" +
      "." + id + ":before{display:none!important;border:0!important}"
  }


  function selectVisibleLabels(candidates, options = {}) {
    const density = ["sparse", "balanced", "dense"].includes(options.density) ? options.density : "balanced";
    const padding = density === "sparse" ? 12 : density === "dense" ? 2 : 6;
    const accepted = [];
    const visible = [];
    (Array.isArray(candidates) ? candidates : []).forEach((candidate, index) => {
      if (!candidate || ![candidate.left, candidate.top, candidate.right, candidate.bottom].every(Number.isFinite)) return;
      const box = { left: candidate.left - padding, top: candidate.top - padding,
        right: candidate.right + padding, bottom: candidate.bottom + padding };
      const overlaps = accepted.some(other => box.left < other.right && box.right > other.left &&
        box.top < other.bottom && box.bottom > other.top);
      if (!overlaps) { accepted.push(box); visible.push(index); }
    });
    return visible;
  }

  root.SabzWebGISLabelingCore = Object.freeze({
    normalizeLabelConfig,
    labelText,
    labelPlacement,
    labelClassCss,
    selectVisibleLabels
  });
})(typeof window !== "undefined" ? window : globalThis);
