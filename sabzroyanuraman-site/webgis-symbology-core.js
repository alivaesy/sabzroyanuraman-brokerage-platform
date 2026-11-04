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

  function numericValues(values) {
    return (Array.isArray(values) ? values : []).filter(value =>
      value !== null && value !== undefined &&
      !(typeof value === "string" && value.trim() === "") &&
      (typeof value === "number" || typeof value === "string") &&
      Number.isFinite(Number(value))
    ).map(Number);
  }

  function uniqueValueClasses(values, rampName) {
    const counts = new Map();
    (Array.isArray(values) ? values : []).forEach(value => {
      if (value === null || value === undefined || String(value).trim() === "") return;
      const key = String(value);
      counts.set(key, (counts.get(key) || 0) + 1);
    });
    const keys = Array.from(counts.keys()).sort((a, b) =>
      a.localeCompare(b, undefined, { numeric: true })
    );
    const colors = layerStyleRamp(rampName, Math.max(1, keys.length));
    return keys.map((value, index) => ({
      value,
      count: counts.get(value),
      color: colors[index % colors.length]
    }));
  }

  function graduatedClasses(values, count, rampName, method = "equal-interval") {
    const numbers = numericValues(values).sort((a, b) => a - b);
    if (!numbers.length) return [];
    const size = Math.max(2, Math.min(7, Math.floor(Number(count) || 5)));
    const min = numbers[0], max = numbers[numbers.length - 1];
    const colors = layerStyleRamp(rampName, size);
    const classes = [];
    for (let index = 0; index < size; index++) {
      let upper;
      if (method === "quantile") {
        upper = numbers[Math.min(numbers.length - 1,
          Math.max(0, Math.ceil((index + 1) * numbers.length / size) - 1))];
      } else {
        upper = index === size - 1 ? max : min + (max - min) * (index + 1) / size;
      }
      const lower = index === 0 ? min : classes[index - 1].max;
      classes.push({ min: lower, max: upper, color: colors[index] });
    }
    return classes;
  }

  function classifyGraduatedValue(value, classes) {
    if (value === null || value === undefined ||
        (typeof value === "string" && value.trim() === "") ||
        !Number.isFinite(Number(value)) || !Array.isArray(classes) || !classes.length) return -1;
    const number = Number(value);
    const index = classes.findIndex((item, i) =>
      number <= item.max || i === classes.length - 1
    );
    return index;
  }

  root.SabzWebGISSymbologyCore = Object.freeze({
    layerStyleRamp,
    numericValues,
    uniqueValueClasses,
    graduatedClasses,
    classifyGraduatedValue
  });
})(typeof window !== "undefined" ? window : globalThis);
