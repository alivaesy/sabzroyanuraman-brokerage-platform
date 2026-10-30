//! WebAssembly boundary for the Sabz Royan WebGIS.
//!
//! This bootstrap intentionally exposes build metadata only. It does not report
//! geoprocessing tools as available until their RustGIS engine calls and data
//! conversions have passed integration tests.

use wasm_bindgen::prelude::*;

const UPSTREAM_REVISION: &str = "38f24336140d59092a31f964e9ac86aab18fdecd";
const ADAPTER_PROTOCOL_VERSION: u32 = 1;

#[wasm_bindgen]
pub fn adapter_protocol_version() -> u32 {
    ADAPTER_PROTOCOL_VERSION
}

#[wasm_bindgen]
pub fn upstream_revision() -> String {
    UPSTREAM_REVISION.to_owned()
}

#[wasm_bindgen]
pub fn adapter_status() -> String {
    "bootstrap_only: RustGIS crates are pinned; processing commands are not yet exposed"
        .to_owned()
}
