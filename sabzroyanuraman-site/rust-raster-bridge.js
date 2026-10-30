/* Lazy browser bridge to the Sabz RustGIS WebAssembly adapter.
 * Build with: wasm-pack build rust-raster-wasm --target web --out-dir ../sabzroyanuraman-site/wasm/rust-raster --out-name sabz_raster_wasm
 */
(function (root) {
  "use strict";
  let modulePromise = null;
  async function load() {
    if (!modulePromise) {
      modulePromise = (async () => {
        const module = await import("./wasm/rust-raster/sabz_raster_wasm.js");
        await module.default();
        if (typeof module.run_raster_operation !== "function") throw new Error("RustGIS WASM adapter export is missing.");
        return module;
      })();
      modulePromise.catch(() => { modulePromise = null; });
    }
    return modulePromise;
  }
  async function processRaster(operation, raster, options) {
    if (!raster || !raster.sourceMetadata || !Array.isArray(raster.bands)) throw new Error("A loaded source raster is required.");
    const m = raster.sourceMetadata;
    const gt = m.geoTransform;
    if (!Array.isArray(gt) || gt.length < 6) throw new Error("Source GeoTIFF geotransform is missing.");
    if (Math.abs(gt[2]) > 1e-12 || Math.abs(gt[4]) > 1e-12) throw new Error("Rotated/skewed grids must be normalized before RustGIS terrain processing.");
    const module = await load();
    const bands = raster.bands.map(values => values instanceof Float32Array ? values : Float32Array.from(values));
    const result = module.run_raster_operation(
      operation, m.width, m.height, gt[0], gt[1], gt[3], gt[5],
      m.crs && m.crs.epsg, Number.isFinite(m.noData) ? m.noData : NaN,
      bands, options.expression || "B1", options.azimuth ?? 315,
      options.altitude ?? 45, options.zFactor ?? 1
    );
    if (!(result instanceof Float32Array) || result.length !== m.width * m.height) throw new Error("RustGIS returned an invalid raster buffer.");
    return {
      values: result, width: m.width, height: m.height,
      geoTransform: gt.slice(0, 6), extent: { ...m.extent },
      noData: Math.fround(-3.4e38), crs: m.crs, engine: "RustGIS/WASM"
    };
  }
  async function inspectPluginPackage(fileBytes) {
    const module = await load();
    const bytes = fileBytes instanceof Uint8Array ? fileBytes : new Uint8Array(fileBytes);
    return JSON.parse(module.inspect_plugin_package(bytes));
  }
  async function installPluginPackage(fileBytes, approvedPermissions) {
    const module = await load();
    const bytes = fileBytes instanceof Uint8Array ? fileBytes : new Uint8Array(fileBytes);
    return JSON.parse(module.install_plugin_package(bytes, JSON.stringify(approvedPermissions || [])));
  }
  async function listPlugins() {
    return JSON.parse((await load()).list_installed_plugins());
  }
  async function setPluginEnabled(id, enabled) {
    return (await load()).set_installed_plugin_enabled(id, !!enabled);
  }
  async function uninstallPlugin(id) {
    return (await load()).uninstall_plugin(id);
  }
  async function runPluginCommand(id, params) {
    return JSON.parse((await load()).run_installed_plugin_command(id, JSON.stringify(params || {})));
  }
  root.SabzRustRasterBridge = Object.freeze({
    load, processRaster, inspectPluginPackage, installPluginPackage,
    listPlugins, setPluginEnabled, uninstallPlugin, runPluginCommand
  });
})(window);
