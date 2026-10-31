//! Sabz Royan WebGIS adapter for RustGIS raster analysis.
//! The adapter accepts original full-resolution pixels and georeferencing metadata;
//! rendered map images and reduced-resolution previews are never analysis inputs.

use js_sys::{Array, Float32Array};
use rustgis_analysis::rasterops;
use rustgis_data::raster::{ColorHint, Crs, GeoTransform, Raster};
use wasm_bindgen::prelude::*;

const MAX_PIXELS: usize = 16_000_000;
const OUTPUT_NODATA: f32 = rasterops::NODATA;

#[wasm_bindgen]
pub fn output_nodata_value() -> f32 { OUTPUT_NODATA }

fn run(
    operation: &str,
    width: u32,
    height: u32,
    transform: GeoTransform,
    epsg: u32,
    no_data: Option<f64>,
    input_bands: Vec<Vec<f32>>,
    expression: &str,
    azimuth: f64,
    altitude: f64,
    z_factor: f64,
) -> Result<Vec<f32>, String> {
    let count = (width as usize).checked_mul(height as usize).ok_or("Raster dimensions overflow.")?;
    if count == 0 || count > MAX_PIXELS {
        return Err(format!("Raster has {count} pixels; this WASM adapter currently permits at most {MAX_PIXELS}."));
    }
    if !transform.is_valid() {
        return Err("Raster geotransform is invalid.".into());
    }
    if !transform.pixel_width.is_finite() || !transform.pixel_height.is_finite() {
        return Err("Raster cell size is invalid.".into());
    }
    if !transform.pixel_width.is_sign_positive() || transform.pixel_height == 0.0 {
        return Err("RustGIS raster analysis currently requires a north-up grid with positive X and non-zero Y cell size.".into());
    }
    if input_bands.is_empty() || input_bands.iter().any(|b| b.len() != count) {
        return Err("Input bands do not match the raster dimensions.".into());
    }
    let crs = Crs::from_epsg(epsg).ok_or_else(|| format!("RustGIS does not support EPSG:{epsg} in this build."))?;
    let raster = Raster::new(
        "Sabz Royan input",
        width,
        height,
        input_bands,
        transform,
        crs,
        no_data,
        ColorHint::Gray,
        "Float32",
    ).map_err(|e| e.to_string())?;

    match operation {
        "slope" => rasterops::slope(&raster, 0, z_factor),
        "aspect" => rasterops::aspect(&raster, 0),
        "hillshade" => rasterops::hillshade(&raster, 0, azimuth, altitude, z_factor),
        "calculator" => rasterops::band_math(&raster, expression),
        other => Err(format!("Unsupported RustGIS raster operation: {other}")),
    }
}

#[wasm_bindgen]
pub fn run_raster_operation(
    operation: &str,
    width: u32,
    height: u32,
    origin_x: f64,
    pixel_width: f64,
    origin_y: f64,
    pixel_height: f64,
    epsg: u32,
    no_data: f64,
    bands: Array,
    expression: &str,
    azimuth: f64,
    altitude: f64,
    z_factor: f64,
) -> Result<Float32Array, JsValue> {
    let mut input = Vec::with_capacity(bands.length() as usize);
    for value in bands.iter() {
        let band = Float32Array::new(&value);
        if band.length() != width.saturating_mul(height) {
            return Err(JsValue::from_str("A raster band has an unexpected pixel count."));
        }
        let mut pixels = vec![0.0_f32; band.length() as usize];
        band.copy_to(&mut pixels);
        input.push(pixels);
    }
    let nodata = no_data.is_finite().then_some(no_data);
    let result = run(
        operation,
        width,
        height,
        GeoTransform { origin_x, origin_y, pixel_width, pixel_height },
        epsg,
        nodata,
        input,
        expression,
        azimuth,
        altitude,
        z_factor,
    ).map_err(|e| JsValue::from_str(&e))?;
    Ok(Float32Array::from(result.as_slice()))
}


/// Parse and validate a .rgplugin package using RustGIS' own package and manifest validator.
#[wasm_bindgen]
pub fn inspect_plugin_package(bytes: js_sys::Uint8Array) -> Result<String, JsValue> {
    use rustgis_plugins::Package;
    let mut data = vec![0u8; bytes.length() as usize];
    bytes.copy_to(&mut data);
    let pkg = Package::from_zip(&data).map_err(|e| JsValue::from_str(&e))?;
    if pkg.manifest.runtime != rustgis_plugins::Runtime::Wasm {
        return Err(JsValue::from_str("Only sandboxed WebAssembly .rgplugin packages are supported in the browser; process plugins are rejected."));
    }
    let permissions: Vec<&str> = pkg.manifest.permissions.iter().map(|p| p.code()).collect();
    let commands: Vec<serde_json::Value> = pkg.manifest.commands.iter().map(|c| serde_json::json!({
        "id": c.id, "label": c.label, "params": c.params
    })).collect();
    let tools: Vec<serde_json::Value> = pkg.manifest.tools.iter().map(|t| serde_json::json!({
        "id": t.id, "name": t.name, "summary": t.summary
    })).collect();
    Ok(serde_json::json!({
        "id": pkg.manifest.id, "name": pkg.manifest.name, "version": pkg.manifest.version,
        "description": pkg.manifest.description, "author": pkg.manifest.author,
        "license": pkg.manifest.license, "runtime": "wasm", "sha256": pkg.sha256,
        "permissions": permissions, "commands": commands, "tools": tools
    }).to_string())
}

/// Install only after the UI has explicitly approved every permission declared in the manifest.
#[wasm_bindgen]
pub fn install_plugin_package(bytes: js_sys::Uint8Array, approved_permissions_json: &str) -> Result<String, JsValue> {
    use rustgis_plugins::{self as plugins, Package, Runtime};
    let approved: Vec<String> = serde_json::from_str(approved_permissions_json)
        .map_err(|_| JsValue::from_str("Invalid plugin permission approval payload."))?;
    let mut data = vec![0u8; bytes.length() as usize];
    bytes.copy_to(&mut data);
    let pkg = Package::from_zip(&data).map_err(|e| JsValue::from_str(&e))?;
    if pkg.manifest.runtime != Runtime::Wasm {
        return Err(JsValue::from_str("Process plugins are not allowed in the browser."));
    }
    for permission in &pkg.manifest.permissions {
        if !approved.iter().any(|p| p == permission.code()) {
            return Err(JsValue::from_str("Installation cancelled: not every declared permission was approved."));
        }
    }
    let plugin = plugins::load(pkg, "user-uploaded .rgplugin").map_err(|e| JsValue::from_str(&e))?;
    Ok(plugin.info().to_string())
}

#[wasm_bindgen]
pub fn list_installed_plugins() -> String {
    let list: Vec<serde_json::Value> = rustgis_plugins::list().iter().map(|p| p.info()).collect();
    serde_json::Value::Array(list).to_string()
}

#[wasm_bindgen]
pub fn set_installed_plugin_enabled(id: &str, enabled: bool) -> Result<(), JsValue> {
    rustgis_plugins::set_enabled(id, enabled).map_err(|e| JsValue::from_str(&e))
}

#[wasm_bindgen]
pub fn uninstall_plugin(id: &str) -> Result<(), JsValue> {
    if rustgis_plugins::get(id).is_none() {
        return Err(JsValue::from_str("Plugin is not installed."));
    }
    rustgis_plugins::unload(id);
    Ok(())
}

/// Run a plugin command inside RustGIS' wasmi sandbox, with fuel and memory limits.
/// NoHost intentionally exposes no project editing, file-system or network capabilities.
#[wasm_bindgen]
pub fn run_installed_plugin_command(id: &str, params_json: &str) -> Result<String, JsValue> {
    use rustgis_plugins::{self as plugins, Host, NoHost, Permission};
    let cmd = plugins::command(id).ok_or_else(|| JsValue::from_str("Plugin command is unavailable or disabled."))?;
    for permission in [Permission::Edit, Permission::Files, Permission::Network] {
        if cmd.plugin.manifest.has(permission) {
            return Err(JsValue::from_str("This command requests elevated host permissions. It remains blocked until the WebGIS capability adapter is reviewed and enabled."));
        }
    }
    let params: serde_json::Value = serde_json::from_str(params_json)
        .map_err(|_| JsValue::from_str("Plugin parameters must be valid JSON."))?;
    let mut host = NoHost;
    let result = plugins::run_command(&cmd, &mut host as &mut dyn Host, &params)
        .map_err(|e| JsValue::from_str(&e))?;
    Ok(result.to_string())
}

#[cfg(test)]
mod tests {
    use super::*;

    fn plane() -> Vec<Vec<f32>> {
        // A 5×5 north-up plane rising 10 m east per 10 m cell.
        vec![(0..25).map(|i| ((i % 5) * 10) as f32).collect()]
    }

    #[test]
    fn slope_uses_rustgis_horn_algorithm_on_full_resolution_pixels() {
        let result = run("slope", 5, 5, GeoTransform {
            origin_x: 500_000.0, origin_y: 3_800_000.0, pixel_width: 10.0, pixel_height: -10.0
        }, 32638, Some(-9999.0), plane(), "B1", 315.0, 45.0, 1.0).expect("slope");
        assert_eq!(result.len(), 25);
        assert!((result[12] - 45.0).abs() < 0.01);
    }

    #[test]
    fn calculator_preserves_nodata_as_rustgis_nodata() {
        let result = run("calculator", 2, 2, GeoTransform {
            origin_x: 500_000.0, origin_y: 3_800_000.0, pixel_width: 10.0, pixel_height: -10.0
        }, 32638, Some(-9999.0), vec![vec![1.0, 2.0, -9999.0, 4.0]], "B1 * 2 + 1", 315.0, 45.0, 1.0).expect("calculator");
        assert_eq!(result, vec![3.0, 5.0, OUTPUT_NODATA, 9.0]);
    }
}
