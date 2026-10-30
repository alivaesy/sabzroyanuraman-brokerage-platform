# RustGIS Vector WASM adapter

This adapter ports the already-tested RustGIS vector integration from the earlier
WebGIS integration branch into the current Raster/Plugin workstream without replacing
the current raster adapter.

- Upstream RustGIS is pinned to `38f24336140d59092a31f964e9ac86aab18fdecd`.
- Vector operations execute through `rustgis_analysis::run`; no Turf.js fallback is used
  by this runtime.
- The catalogue is read from RustGIS `ToolSpec` definitions. File/path tools remain
  disabled in the browser runtime.
- GeoJSON is transferred in WGS84 coordinates. The current WASM payload limit is 128 MiB.
- Selection outputs are mapped back to the owning WebGIS layer and synchronized with
  the map selection and visible attribute-table rows.
- The separate Phase 2 raster adapter remains authoritative for raster processing.

## Build

```sh
rustup target add wasm32-unknown-unknown
cargo test --locked
wasm-pack build --target web --release --out-dir ../rustgis-vector-wasm-pkg --out-name sabz_royan_vector_wasm
```

The generated package is not committed by this workflow on feature branches; CI uploads
it as an artifact. Deployment to the live host still requires a reviewed deployment step.

## Important validation boundary

Native tests cover a real RustGIS Buffer invocation and GeoJSON output. The browser UI
is connected to the WASM package, but live-host browser execution and ArcMap 10.8 numerical
comparison are not claimed until the workflow passes and a representative dataset is
tested in the deployed site.
