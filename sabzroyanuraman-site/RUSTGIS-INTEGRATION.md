# RustGIS integration package for Sabz Royan WebGIS

## Package contents

- `webgis-rustgis-enhancements.js`: compatibility layer loaded after the existing WebGIS application. It adds free node movement to the existing ModelBuilder graph, redraws dependency edges after movement, adds viewport panning and Ctrl+wheel zoom, and adds selection, keyboard nudging and resize affordances for Layout elements.
- `rustgis-wasm/`: a separate Cargo package that pins upstream RustGIS crates and exposes the initial WASM bridge handshake.
- `.github/workflows/webgis-rustgis-wasm.yml`: checks and builds the adapter for `wasm32-unknown-unknown`.
- The existing Leaflet map, layer registry, project persistence and current Layout/ModelBuilder execution pipeline remain intact.

## RustGIS source review and pin

Upstream: https://github.com/opengeos/RustGIS

Pinned upstream commit: `38f24336140d59092a31f964e9ac86aab18fdecd`.

At this revision, the upstream workspace declares Rust 1.90+, edition 2024 and Apache-2.0. Its layered crates include `rustgis-geom`, `rustgis-data`, `rustgis-analysis`, `rustgis-map`, `rustgis-render`, `rustgis-engine`, `rustgis-geolibre` and `rustgis-ui-egui`.

The upstream browser app is a Rust/WASM application built as its own app (Trunk + wasm-bindgen/egui), not a drop-in JavaScript library. We therefore keep its UI separate and integrate its engine/data layers behind a WASM boundary. The upstream source is referenced by immutable Git revision rather than copied into this repository.

## Current WASM adapter status

The new `rustgis-wasm/` package currently has a **bootstrap-only** implementation:
- Cargo dependencies pin `rustgis-engine`, `rustgis-analysis` and `rustgis-data` to the same upstream commit.
- The WASM exports report the adapter protocol version, pinned upstream revision and explicit bootstrap-only status.
- A GitHub Actions workflow runs `cargo check` and a release build for `wasm32-unknown-unknown`.

This is not yet a connected geoprocessing engine. It does not execute Buffer, Clip, raster analysis, data I/O or GeoTIFF writing. The web UI must not label any operation as RustGIS-backed until its actual engine command is wired and covered by tests.

## Next integration boundary

The adapter will expose a stable, byte/JSON-oriented interface while retaining Leaflet as the map display and the Sabz Royan project/layer model as application state:

1. `read_dataset(bytes, format, options)` -> dataset handle + metadata
2. `run_tool(tool_id, input_handles, parameters)` -> output handle + metadata
3. `read_features(handle, offset, limit)` -> GeoJSON / typed attributes
4. `read_raster_window(handle, window, bands)` -> typed pixel buffer
5. `write_dataset(handle, format, crs, options)` -> bytes
6. `list_tools()` -> tool descriptors

Map upstream crates to adapter responsibilities: data/geom for formats and spatial types, analysis/geolibre for algorithms, engine for command execution, and render for layout export. The exact upstream session/command APIs must be verified against the pinned source before implementation; do not guess function names or signatures.

## Functional boundaries still open

- georeferenced GeoTIFF output from Raster Calculator/Slope/Aspect/Hillshade
- broad EPSG/CRS support in the current browser raster loader
- a RustGIS-backed geoprocessing catalogue and execution pipeline
- production-grade print composition with true map-frame scale/extent locking

Current raster output remains PNG until a tested GeoTIFF writer is connected.

## Loading and validation

The main `web-gis.html` currently loads the JavaScript enhancement layer after its existing GIS scripts:

    <script src="webgis-rustgis-enhancements.js"></script>

The WASM artifact is not loaded by the page yet because the adapter does not expose processing commands. The JavaScript enhancement file has passed syntax parsing and static hook-order checks. Browser interaction tests and real vector/raster end-to-end tests remain outstanding.
