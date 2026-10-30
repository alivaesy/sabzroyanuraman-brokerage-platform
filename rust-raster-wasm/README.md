# Sabz Royan RustGIS Raster WASM Adapter

This adapter calls the **real RustGIS** raster algorithms from WebAssembly. It does not use
the WebGIS preview canvas or its former JavaScript terrain calculations as analysis input.

## Build

Requirements: Rust 1.90+, the `wasm32-unknown-unknown` target, and wasm-pack.

```sh
rustup target add wasm32-unknown-unknown
cargo test --manifest-path rust-raster-wasm/Cargo.toml
wasm-pack build rust-raster-wasm --target web --out-dir ../sabzroyanuraman-site/wasm/rust-raster --out-name sabz_raster_wasm
```

The generated `wasm/` directory must be deployed beside `web-gis.html`; it is generated build
output and is intentionally not checked in.

## Supported operations in this adapter

- `slope`: RustGIS Horn terrain algorithm, degrees, configurable Z factor.
- `aspect`: RustGIS compass aspect, degrees (flat cells = -1).
- `hillshade`: RustGIS hillshade with configurable azimuth, altitude and Z factor.
- `calculator`: RustGIS band arithmetic expression parser (`B1`, `B2`, arithmetic and parentheses).

Input bands are full-resolution Float32 buffers with EPSG, affine north-up GeoTransform and NoData.
The adapter rejects unknown EPSG codes, invalid dimensions and rotated/skewed grids instead of
silently assigning a coordinate system or producing misleading output. Current WASM cap: 16 million
pixels per operation. Output pixels are Float32 and use RustGIS' NoData sentinel.

## Upstream

RustGIS source is pinned to commit
`8b322b290448e468c8972df9b1f601d3a72f7d53` (Apache-2.0). See
https://github.com/opengeos/RustGIS and its LICENSE before changing the pinned revision.

## Plugin runtime

The adapter also exposes RustGIS' own `rustgis-plugins` package and manifest validator for
`.rgplugin` archives. The browser manager accepts WebAssembly packages only, verifies the
package hash and declared permissions, and installs them through the upstream loader. Plugin
commands execute in RustGIS' `wasmi` interpreter with its fuel and memory limits.

The current WebGIS host is deliberately `NoHost`: commands that request project edit, file or
network capabilities are blocked. A later capability bridge must map each permission to narrowly
scoped WebGIS operations before those capabilities are granted.

## Browser verification

`sabzroyanuraman-site/raster-runtime-tests.html` exercises the four raster operations against a
synthetic 5×5 GeoTIFF and verifies the generated GeoTIFF read-back. CI runs this page in headless
Chromium after building the WASM module.
