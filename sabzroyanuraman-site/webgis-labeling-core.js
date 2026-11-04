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
      placement: ["auto", "center", "top", "right", "bottom", "left"].includes(source.placement) ? source.placement : "auto"
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

  root.SabzWebGISLabelingCore = Object.freeze({
    normalizeLabelConfig,
    labelText,
    labelPlacement,
    labelClassCss
  });
})(typeof window !== "undefined" ? window : globalThis);
