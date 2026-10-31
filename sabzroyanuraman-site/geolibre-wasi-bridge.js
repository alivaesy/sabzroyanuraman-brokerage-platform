/* GeoLibre WASI runner bridge, following RustGIS' browser integration.
 * Loads only after the user requests the tools. Version and WASI binary are pinned together.
 */
(function (root) {
  "use strict";
  const BASE = "https://cdn.jsdelivr.net/npm/geolibre-wasm@1.7.0";
  const MANIFEST_URL = BASE + "/manifests.json?v=1";
  let ready = null;
  async function tools() {
    if (!ready) {
      ready = (async () => {
        const mod = await import(BASE + "/tools.mjs/+esm");
        await mod.initTools(BASE + "/geolibre-cli.wasm");
        return mod;
      })();
      ready.catch(() => { ready = null; });
    }
    return ready;
  }
  async function manifests() {
    let cache = null;
    try { cache = await caches.open("sabz-geolibre-1.7.0"); } catch (_) {}
    if (cache) {
      try {
        const hit = await cache.match(MANIFEST_URL);
        if (hit) {
          const list = await hit.json();
          if (Array.isArray(list) && list.length && typeof list[0].id === "string") return list;
        }
      } catch (_) {}
    }
    const list = await (await tools()).listManifests();
    if (!Array.isArray(list)) throw new Error("GeoLibre returned an invalid tool manifest list.");
    if (cache && list.length) {
      try { await cache.put(MANIFEST_URL, new Response(JSON.stringify(list), {headers:{"content-type":"application/json"}})); } catch (_) {}
    }
    return list;
  }
  async function run(toolId, args, files) {
    if (typeof toolId !== "string" || !toolId) throw new Error("Select a GeoLibre tool.");
    const input = {};
    for (const file of files || []) {
      if (!file || typeof file.name !== "string" || !(file.bytes instanceof Uint8Array)) throw new Error("GeoLibre input files must be named Uint8Array buffers.");
      input[file.name] = file.bytes;
    }
    const result = await (await tools()).runTool(toolId, {args: Array.isArray(args) ? args : [], input});
    return {
      exitCode: result.exitCode,
      stdout: Array.isArray(result.stdout) ? result.stdout : [],
      files: Object.entries(result.files || {}).map(([name, bytes]) => ({name, bytes: bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes)}))
    };
  }
  root.SabzGeoLibreBridge = Object.freeze({manifests, run, version:"1.7.0"});
})(window);
