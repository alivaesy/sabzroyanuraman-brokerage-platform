# WebGIS RustGIS geoprocessing status

Updated: 2026-10-31

## Current implementation
- `runRustGisVectorTool()` is the shared browser adapter: it lazy-loads the vector WASM runtime through `SabzRustGISPluginBridge`, packages WebGIS GeoJSON layers, calls the pinned RustGIS analysis catalogue, and returns vector outputs.
- The Plugin Manager ABI now uses `window.SabzRustGISPluginBridge`; the raster adapter retains `window.SabzRustRasterBridge`.
- Whole-layer geoprocessing now routes Buffer, Clip, Intersect, Erase, Union, Identity, Dissolve, and Convex Hull through RustGIS. The source layers are preserved and outputs are added to the WebGIS map.
- ModelBuilder now routes Buffer, Clip, Intersect, Erase, Union, Identity, Dissolve, Dissolve by Field, Convex Hull, and Feature To Point through RustGIS. Existing model JSON format, graph dependencies, cancellation, and intermediate-output flow remain in place.
- Other operations without a RustGIS mapping remain on their existing JavaScript implementation; there is no silent fallback after a selected RustGIS operation fails.
- RustGIS source remains pinned at `38f24336140d59092a31f964e9ac86aab18fdecd`.

## Tests added
- Browser adapter fixture test verifies bridge initialization, tool dispatch, layer payload, parameters, and vector output handling.
- Static regression tests verify whole-layer and ModelBuilder RustGIS routing and isolated plugin/raster namespaces.
- Native Rust adapter tests exercise RustGIS Clip, Intersect, Erase, Union, Identity, Dissolve, Convex Hull, Feature To Point, Spatial Join, and Calculate Geometry with overlapping polygon fixtures.
- The vector WASM workflow now also runs when the static WebGIS tests or plugin/raster bridge files change.

## Important behavior and limitations
- The RustGIS adapter exchanges GeoJSON in WGS84. The engine's buffer is geodesic; do not assume numerical equivalence to ArcMap planar Buffer on projected datasets.
- RustGIS tool-specific geometry and attribute semantics apply. ArcMap 10.8 parity has not been established.
- RustGIS WASM build/CI success does not prove that the generated package is deployed by cPanel Cron or works in the live browser.
- No live-host authenticated upload, two-browser raster persistence test, or ArcMap 10.8 reference comparison was performed in this change.

## Required acceptance tests
1. Wait for the latest `RustGIS vector geoprocessing WASM` workflow and confirm native tests, static tests, and WASM build all pass on the latest master commit.
2. Confirm `RustGIS Cron Publish` completes and commits generated vector/raster WASM browser assets. Confirm the cPanel Cron log reports the resulting master commit.
3. Verify live HTTP responses for both WASM packages and JavaScript bridges, including MIME type, no 404/CORS errors, and successful engine initialization.
4. In the live WebGIS, test Buffer with point/line/polygon inputs; Clip with point/line/polygon input; Intersect/Erase/Union/Identity with valid polygon overlays; Dissolve and Convex Hull; inspect output geometry, fields, counts, and save/reload.
5. Run ModelBuilder vector-only and mixed-node workflows, including dependencies, overlay-node references, cancellation, invalid parameters, and failed-node behavior.
6. Build a fixed EPSG:32638 fixture set and run identical cases in ArcMap 10.8. Compare feature counts, geometry types, attributes, area/length, coordinate deviation, and documented XY tolerance. Record algorithm differences rather than asserting unsupported parity.

## Current state
- Source changes are committed directly to `master` per the repository's continuation instructions.
- GitHub Actions: commit `c73d421` completed successfully (native tests, WebGIS static regression tests, and vector WASM build). Later commits add further native fixtures and Spatial Join/Calculate Geometry routing; their latest workflow is still pending/in progress and must pass before final sign-off.
- Live browser and ArcMap validation remain outstanding.