/* Pure ArcMap-style renderer palette utilities for Sabz WebGIS. */
(function (root) {
  "use strict";

  const RAMPS = Object.freeze({
    green: Object.freeze(["#edf8e9", "#bae4b3", "#74c476", "#31a354", "#006d2c", "#00441b", "#002b12"]),
    blue: Object.freeze(["#eff3ff", "#c6dbef", "#9ecae1", "#6baed6", "#3182bd", "#08519c", "#08306b"]),
    red: Object.freeze(["#fee5d9", "#fcae91", "#fb6a4a", "#ef3b2c", "#cb181d", "#99000d", "#67000d"]),
    purple: Object.freeze(["#f2f0f7", "#cbc9e2", "#9e9ac8", "#756bb1", "#54278f", "#3f007d", "#2a0054"]),
    spectral: Object.freeze(["#2c7bb6", "#abd9e9", "#ffffbf", "#fdae61", "#d7191c", "#a50026", "#67001f"])
  });

  function layerStyleRamp(name, count) {
    const palette = RAMPS[name] || RAMPS.green;
    const size = Math.max(1, Math.floor(Number(count) || 1));
    if (size === 1) return [palette[Math.floor(palette.length / 2)]];
    return Array.from({ length: size }, (_, i) =>
      palette[Math.round(i * (palette.length - 1) / (size - 1))]
    );
  }

  root.SabzWebGISSymbologyCore = Object.freeze({ layerStyleRamp });
})(typeof window !== "undefined" ? window : globalThis);
