# RustGIS integration package for Sabz Royan WebGIS

## Package contents

- `webgis-rustgis-enhancements.js`: ModelBuilder node movement, dependency-edge redraw, workspace panning/zoom and Layout element selection, keyboard nudging and resize affordances.
- `webgis-rustgis-runtime.js`: loads the compiled WASM package, reads the RustGIS tool catalogue, creates parameter forms, passes vector layers as GeoJSON and raster layers as GeoTIFF bytes, executes tools and returns vector/raster outputs to the WebGIS.
- `rustgis-wasm/`: Rust adapter using the upstream RustGIS analysis and data crates.
- `rustgis-wasm-pkg/`: generated browser package; built and published by GitHub Actions after changes reach master.
- `.github/workflows/webgis-rustgis-wasm.yml`: native Buffer test, WASM build, JavaScript syntax check and package publication.
- `web-gis.html`: connects the runtime to the existing WebGIS and exposes a safe layer-add bridge.

Leaflet remains the map display engine. The ArcMap-like user interface and existing project/layer model remain in JavaScript. Geoprocessing calls are delegated to RustGIS's Rust implementation compiled to WebAssembly.

## Upstream source and license

- Repository: https://github.com/opengeos/RustGIS
- Pinned commit: `38f24336140d59092a31f964e9ac86aab18fdecd`
- License: Apache-2.0
- Rust edition 2024; minimum Rust version 1.90.

The adapter uses `rustgis-analysis` and `rustgis-data` from the same immutable revision. It calls the upstream public `rustgis_analysis::run` API, not a JavaScript reimplementation. Upstream source is referenced through Cargo's Git dependency; it is not copied into this repository.

## Actual execution flow

1. The browser loads `webgis-rustgis-runtime.js`.
2. The runtime imports and initializes `rustgis-wasm-pkg/sabzroyan_rustgis_wasm.js`.
3. `list_tools()` returns the actual RustGIS analysis tool catalogue and parameter schemas.
4. The **RG** toolbar button opens a parameter form generated from those schemas.
5. Selected vector layers are converted through Leaflet's `toGeoJSON()`; selected Raster sources are transferred as the original GeoTIFF bytes.
6. The Rust adapter parses vectors into RustGIS `Dataset` objects and rasters into RustGIS `Raster` objects.
7. Vector tools call `rustgis_analysis::run(tool_id, inputs, params)`; the dedicated Raster tools call the pinned `rustgis_analysis::rasterops` functions.
8. Vector results are encoded as GeoJSON. Raster results are written as real GeoTIFF bytes with the source grid's CRS and transform, downloaded, and previewed on the map.

## Supported and deferred capabilities

Currently supported:
- GeoJSON vector inputs in WGS 84 longitude/latitude and built-in RustGIS vector analysis.
- GeoTIFF raster inputs from the WebGIS Raster Import workspace, passed as original file bytes.
- Dedicated RustGIS Raster operations: Slope, Aspect, Hillshade and Raster Calculator.
- Mixed vector/raster input through the `Inputs::raster` contract for compatible built-in tools such as Zonal Statistics.
- Real GeoTIFF output written by RustGIS, preserving the input grid's CRS and transform; output bytes are downloaded and registered for subsequent Raster processing.
- Native tests for RustGIS Buffer and a Raster Slope → GeoTIFF write/read round trip.

Still deferred:
- Persisting raster sources and Raster map layers inside saved WebGIS project files; the current source registry is browser-session scoped.
- Applying RustGIS selection results to the WebGIS selection model.
- Direct path/save-file tools and external GeoLibre/plugin suites.
- Large-data streaming/windowed transfer; the current adapter has a 128 MiB encoded-input cap and RustGIS applies its WebAssembly sample limit.
- Browser end-to-end tests and production hosting verification.

The independent browser Raster preview tools remain separate from RustGIS. The RG toolbox runs the actual RustGIS algorithms; its downloaded TIFF is the analytical output, while the Leaflet overlay is only a display preview.

## Build and deployment

The workflow:
- runs the adapter's native Rust test;
- builds the WebAssembly module with `wasm-pack`;
- checks the JavaScript runtime syntax;
- uploads the package as a PR artifact;
- after a push to `master`, commits generated files into `sabzroyanuraman-site/rustgis-wasm-pkg/` for same-origin static hosting.

The generated WASM package is not available to the live site until the PR is merged and the master build workflow completes. The PR branch itself is not deployed.

## Important architecture boundary

This is a real first-stage RustGIS processing integration for vector data, not a complete ArcMap replacement. The UI, editing, layer management, Layout View and ModelBuilder remain in the existing Sabz Royan application. The next substantial integration is the raster data bridge, followed by applying selection/update outputs and adding end-to-end tests with representative GeoJSON and GeoTIFF fixtures.


## Raster / GeoTIFF integration

The adapter now has a second execution path for raster data:

- GeoTIFF input is read from browser-provided bytes using `rustgis_data::raster::geotiff::read`.
- `run_raster_tool` dispatches to the pinned RustGIS `rasterops` algorithms for Slope, Aspect, Hillshade and Raster Calculator.
- `rustgis_analysis::rasterops::derive` builds the output on the input raster grid.
- `rustgis_data::raster::geotiff::write` produces the actual output GeoTIFF, preserving the grid transform and CRS.
- The runtime downloads the TIFF and attempts a lightweight Leaflet preview using GeoTIFF.js.
- The standard `run_tool` adapter path can now receive a GeoTIFF payload in addition to GeoJSON and exposes it through `Inputs::raster`, for mixed workflows such as Zonal Statistics.

The existing Raster Import workspace registers original GeoTIFF bytes in a browser-session source registry. The RG toolbox reads from that registry. Output previews are deliberately excluded from the current vector-only project serializer so they are not silently saved as empty vector layers. Persisting raster sources and raster layers inside saved WebGIS projects is a later task.

This stage does not yet connect every upstream raster tool. In particular, Raster-to-PMTiles requires a map/style-aware tile provider implementation, and other file/path tools remain unavailable in the browser UI.
