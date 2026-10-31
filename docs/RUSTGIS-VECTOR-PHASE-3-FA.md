# Phase 3 — RustGIS Vector Geoprocessing

## Scope delivered in this branch

- Adds a separate RustGIS vector WASM adapter using the upstream analysis engine.
- Adds a WebGIS runtime panel which reads the real RustGIS tool catalogue and invokes
  `rustgis_analysis::run`; Buffer, Clip, Intersect, Erase and other tools are exposed
  only when present in the pinned upstream catalogue.
- RustGIS vector outputs are added as named Leaflet/WebGIS vector layers, making them
  available to the layer manager and attribute table.
- RustGIS selection output is mapped to the matching WebGIS feature layer and synced
  to map highlighting and the visible attribute table.
- Adds a dedicated native/WASM CI workflow.

## Existing GeoLibre and Plugin integration

The current branch already loads `geolibre-wasi-bridge.js` and `plugin-manager.js`.
GeoLibre outputs are routed through `addGeoLibreOutputToWebGIS`; Plugin Manager validates
WASM packages and runs them in the RustGIS `NoHost` sandbox. Their existing panels remain
available. This phase does not grant plugins edit/files/network capabilities, and it does
not claim arbitrary plugin outputs can mutate project layers.

## Validation still required

1. CI native tests and WASM build must pass on the final branch HEAD.
2. Build artifact must be deployed to the WebGIS host; feature-branch CI only uploads it.
3. Test Buffer against point, line and polygon samples in a projected CRS; check distance,
   area, topology and output CRS.
4. Test Clip, Intersect and Erase with polygon overlays, multipart geometries, holes and
   edge-touch cases.
5. Test selection tools with filtered/sorted attribute tables and multi-layer selection.
6. Compare representative outputs with ArcMap 10.8 using identical CRS, units, tolerance
   and geoprocessing parameters. Document expected algorithm differences.
7. Test GeoLibre and Plugin flows on the live site, including GeoTIFF/GeoJSON ingestion,
   sandbox rejection and reload persistence.

No live-host E2E or ArcMap numerical equivalence is asserted by this handoff.
