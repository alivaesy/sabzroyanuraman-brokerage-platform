# RustGIS integration package for Sabz Royan WebGIS

## Package contents

- webgis-rustgis-enhancements.js: compatibility layer loaded after the existing WebGIS application. It adds free node movement to the existing ModelBuilder graph, redraws dependency edges after movement, adds viewport panning and Ctrl+wheel zoom, and adds selection, keyboard nudging and native resize affordances for Layout elements.
- The existing Leaflet map, layer registry, vector processing functions, project persistence and current Layout/ModelBuilder execution pipeline remain the source of truth.

## RustGIS source review

Reference: https://github.com/opengeos/RustGIS

The upstream project is a Rust workspace (version 0.4.0 in its workspace manifest), with internal crates including rustgis-geom, rustgis-data, rustgis-analysis, rustgis-map, rustgis-render, rustgis-engine, rustgis-geolibre and rustgis-ui-egui.

The upstream browser app is a Rust/WASM application built as its own app (Trunk + wasm-bindgen/egui), not a drop-in JavaScript library. Embedding its entire UI into this Leaflet application would create two competing map/UI runtimes. This package does not pretend that upstream WASM is already linked, and it does not vendor unverified upstream implementation code.

## Recommended engine integration boundary

Keep Leaflet as the map display and the existing Sabz Royan project/layer model as application state. Add a separately compiled WASM adapter with a stable command interface:

1. read_dataset(bytes, format, options) -> dataset_handle + metadata
2. run_tool(tool_id, input_handles, parameters) -> output_handle + metadata
3. read_features(handle, offset, limit) -> GeoJSON/typed attributes
4. read_raster_window(handle, window, bands) -> typed pixel buffer
5. write_dataset(handle, format, crs, options) -> bytes
6. list_tools() -> tool descriptors

Map upstream crates to adapter responsibilities: data/geom for formats and spatial types, analysis/geolibre for algorithms, engine for command execution, and render for layout export. Pin an upstream commit and comply with its Apache-2.0 license and NOTICE requirements before compiling or copying upstream source.

## Functional boundaries

This package improves graph interaction and Layout element selection. It does NOT claim to complete:

- georeferenced GeoTIFF output from Raster Calculator/Slope/Aspect/Hillshade
- arbitrary EPSG/CRS support in the current browser raster loader
- a full RustGIS geoprocessing catalogue or WASM runtime
- production-grade print composition with true map-frame scale/extent locking

Those require the compiled Rust/WASM adapter and integration tests with representative GeoTIFF, vector and project files. Current raster output remains PNG until that writer is connected.

## Loading

The main web-gis.html loads this file after its existing GIS scripts:

    <script src="webgis-rustgis-enhancements.js"></script>

No external runtime or CDN dependency is introduced by this compatibility layer.

## Validation

The enhancement file is syntax-checked as JavaScript. A real browser interaction test and end-to-end GIS dataset tests must still be run in the deployed application before merging this branch.
