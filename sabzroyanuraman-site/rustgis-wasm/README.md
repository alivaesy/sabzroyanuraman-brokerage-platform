# Sabz Royan RustGIS WASM adapter

This Cargo package is the processing bridge between the existing JavaScript/Leaflet WebGIS and the upstream RustGIS algorithms. It does not replace the ArcMap-like interface or the Leaflet map renderer.

## Pinned upstream

- Repository: https://github.com/opengeos/RustGIS
- Immutable commit: `38f24336140d59092a31f964e9ac86aab18fdecd`
- Rust edition: 2024; minimum Rust version: 1.90
- License: Apache-2.0
- Direct crates: `rustgis-analysis` and `rustgis-data`

The adapter calls RustGIS's public `rustgis_analysis::run` API. It does not reimplement Buffer, Clip or other algorithms in JavaScript.

## Exported WebAssembly API

- `adapter_protocol_version()`: bridge protocol version.
- `upstream_revision()`: immutable upstream commit.
- `adapter_status()`: runtime status.
- `list_tools()`: actual RustGIS built-in analysis catalogue, including tool ids, parameter definitions, defaults, choices and help.
- `run_tool(tool_id, layers_json, params_json)`: reads input GeoJSON into RustGIS datasets, executes the named RustGIS analysis tool, and serializes vector outputs back to GeoJSON.

The browser panel is opened with the **RG** button beside ArcToolbox. It builds parameter forms from the RustGIS tool catalogue, takes vector layers from the current WebGIS project, executes the tool in WASM, and adds returned vector layers to the map and layer list.

## Current supported boundary

- Vector GeoJSON inputs in WGS 84 longitude/latitude.
- Built-in RustGIS vector tools whose parameters can be supplied as layer names, numbers, choices, booleans or text.
- Vector outputs are added as new map layers; file outputs are downloaded.

The adapter currently does not bridge the browser's existing raster-layer storage into RustGIS RasterInput. Tools requiring raster inputs or direct file/path parameters are visibly disabled in the RustGIS panel rather than falsely offered as executable. Selection outputs are reported but are not yet applied to the WebGIS selection model. GeoLibre and plugin tool suites are not registered in this adapter.

A native Rust unit test exercises RustGIS Buffer against a GeoJSON point and checks that the generated polygon output can be serialized back to GeoJSON.

## Build

Install Rust 1.90+, the `wasm32-unknown-unknown` target, Clang and wasm-pack.

From this directory:

```sh
rustup target add wasm32-unknown-unknown
wasm-pack build --target web --release --out-dir ../rustgis-wasm-pkg --out-name sabzroyan_rustgis_wasm
```

The GitHub Actions workflow runs the native test, builds the WebAssembly package and checks the browser bridge JavaScript. For pull requests it uploads the package as an artifact. After changes reach `master`, it commits the generated package under `sabzroyanuraman-site/rustgis-wasm-pkg/` so the static website can load it from the same origin.

## Data and coordinate handling

RustGIS stores feature geometries in WGS 84 longitude/latitude and its analysis algorithms perform their own projected/geodesic calculations as appropriate. The adapter deliberately transfers GeoJSON rather than Leaflet internals. The existing map stays responsible for display, styling and interaction.
