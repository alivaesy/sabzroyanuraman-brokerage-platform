# Sabz Royan RustGIS WASM adapter

This is the Rust/WASM integration boundary for the existing JavaScript/Leaflet WebGIS.
It is intentionally a separate Cargo package: it does not replace the map renderer or
the existing ArcMap-like interface.

## Pinned upstream

- Repository: https://github.com/opengeos/RustGIS
- Commit: `38f24336140d59092a31f964e9ac86aab18fdecd`
- Workspace baseline at that revision: Rust 1.90+, edition 2024, Apache-2.0.
- Direct upstream crates: `rustgis-engine`, `rustgis-analysis`, `rustgis-data`.

The adapter depends on the upstream crates at an immutable commit rather than copying
upstream source into this repository. Preserve upstream license and attribution notices
if any upstream source/assets are ever vendored.

## Current implementation boundary

The crate currently provides a WASM handshake only:
- adapter protocol version;
- exact upstream revision;
- explicit `bootstrap_only` status.

It does **not** yet execute Buffer, Clip, raster analysis, dataset I/O, or GeoTIFF
writing. The UI must not advertise these operations as RustGIS-backed until those
commands are implemented and tested.

## Build prerequisites

Install Rust 1.90 or newer and the `wasm32-unknown-unknown` target. RustGIS's data
crate includes a Zstandard decoder that may require a working Clang toolchain for
the browser target.

From this directory:

```sh
rustup target add wasm32-unknown-unknown
cargo check --target wasm32-unknown-unknown
cargo build --release --target wasm32-unknown-unknown
```

The generated `.wasm` and JavaScript glue are build artifacts; they are not committed.
The next stage is to connect the upstream session/command registry and typed data
conversion behind a narrow JSON/byte-oriented API, then add vector and raster fixtures
for end-to-end tests.
