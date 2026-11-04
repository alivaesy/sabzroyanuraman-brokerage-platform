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
      color
    });
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
      "px!important;font-weight:500;background:rgba(255,255,255,.86);" +
      "border:1px solid #9aa8af;border-radius:2px;padding:1px 3px;box-shadow:none}";
  }

  root.SabzWebGISLabelingCore = Object.freeze({
    normalizeLabelConfig,
    labelText,
    labelClassCss
  });
})(typeof window !== "undefined" ? window : globalThis);
