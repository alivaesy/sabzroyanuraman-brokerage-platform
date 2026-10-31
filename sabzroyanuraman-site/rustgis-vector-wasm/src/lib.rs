//! Real RustGIS geoprocessing bridge for the Sabz Royan WebGIS.
//!
//! Input and output geometries cross the boundary as GeoJSON (WGS 84 coordinates).
//! Algorithms are executed by rustgis-analysis; this crate does not reimplement them.

use std::collections::HashMap;
use std::io::Cursor;

use base64::Engine as _;
use rustgis_analysis::{Inputs, Output, OutputLayer, ParamKind, RasterInput, ToolResult};
use rustgis_data::{Dataset, raster::Raster};
use serde_json::{Value, json};
use wasm_bindgen::prelude::*;

const UPSTREAM_REVISION: &str = "38f24336140d59092a31f964e9ac86aab18fdecd";
const ADAPTER_PROTOCOL_VERSION: u32 = 3;
const MAX_INPUT_BYTES: usize = 128 * 1024 * 1024;

#[derive(Default)]
struct LayerInputs {
    datasets: HashMap<String, Dataset>,
    rasters: HashMap<String, Raster>,
}

impl Inputs for LayerInputs {
    fn dataset(&self, layer: &str) -> Option<&Dataset> {
        self.datasets
            .get(layer)
            .or_else(|| self.datasets.iter().find(|(name, _)| name.eq_ignore_ascii_case(layer)).map(|(_, ds)| ds))
    }

    fn selection(&self, _layer: &str) -> &[usize] { &[] }

    fn raster(&self, layer: &str) -> Option<RasterInput<'_>> {
        let raster = self.rasters.get(layer).or_else(|| self.rasters.iter().find(|(name, _)| name.eq_ignore_ascii_case(layer)).map(|(_, raster)| raster))?;
        Some(RasterInput { raster, path: None, embedded: None })
    }
}

fn js_error(message: impl ToString) -> JsValue {
    JsValue::from_str(&message.to_string())
}

fn kind_descriptor(kind: ParamKind) -> Value {
    match kind {
        ParamKind::Layer => json!({"type":"layer"}),
        ParamKind::PolygonLayer => json!({"type":"polygon-layer"}),
        ParamKind::PointLayer => json!({"type":"point-layer"}),
        ParamKind::VectorLayer => json!({"type":"vector-layer"}),
        ParamKind::RasterLayer => json!({"type":"raster-layer"}),
        ParamKind::Field => json!({"type":"field"}),
        ParamKind::NumericField => json!({"type":"numeric-field"}),
        ParamKind::CoordinateField => json!({"type":"coordinate-field"}),
        ParamKind::Number => json!({"type":"number"}),
        ParamKind::Text => json!({"type":"text"}),
        ParamKind::Bool => json!({"type":"boolean"}),
        ParamKind::Choice(choices) => json!({"type":"choice","choices":choices}),
        ParamKind::VectorLayers => json!({"type":"vector-layers"}),
        ParamKind::RasterLayers => json!({"type":"raster-layers"}),
        ParamKind::Path => json!({"type":"path"}),
        ParamKind::SaveFile { ext } => json!({"type":"save-file","extension":ext}),
    }
}

fn tool_catalogue() -> Value {
    let tools: Vec<Value> = rustgis_analysis::tools()
        .map(|tool| {
            let params: Vec<Value> = tool.params.iter().map(|param| {
                let default = if param.default.is_empty() {
                    Value::Null
                } else {
                    serde_json::from_str(param.default).unwrap_or(Value::Null)
                };
                json!({
                    "name": param.name,
                    "label": param.label,
                    "kind": kind_descriptor(param.kind),
                    "required": param.required,
                    "default": default,
                    "help": param.help
                })
            }).collect();
            json!({
                "id": tool.id,
                "name": tool.name,
                "toolbox": tool.toolbox,
                "summary": tool.summary,
                "params": params
            })
        })
        .collect();
    let raster_tools = vec![
        json!({"id":"raster.slope","name":"Slope","toolbox":"Raster Analysis › Surface","summary":"Calculates slope in degrees with RustGIS rasterops.","engine":"rasterops","params":[
            {"name":"raster","label":"Input Raster","kind":{"type":"raster-source"},"required":true,"default":null},
            {"name":"band","label":"Band (1-based)","kind":{"type":"number"},"required":false,"default":1},
            {"name":"zFactor","label":"Z Factor","kind":{"type":"number"},"required":false,"default":1},
            {"name":"output","label":"Output Name","kind":{"type":"text"},"required":false,"default":"Slope"}]}),
        json!({"id":"raster.aspect","name":"Aspect","toolbox":"Raster Analysis › Surface","summary":"Calculates compass aspect in degrees with RustGIS rasterops.","engine":"rasterops","params":[
            {"name":"raster","label":"Input Raster","kind":{"type":"raster-source"},"required":true,"default":null},
            {"name":"band","label":"Band (1-based)","kind":{"type":"number"},"required":false,"default":1},
            {"name":"output","label":"Output Name","kind":{"type":"text"},"required":false,"default":"Aspect"}]}),
        json!({"id":"raster.hillshade","name":"Hillshade","toolbox":"Raster Analysis › Surface","summary":"Calculates hillshade using RustGIS rasterops.","engine":"rasterops","params":[
            {"name":"raster","label":"Input Raster","kind":{"type":"raster-source"},"required":true,"default":null},
            {"name":"band","label":"Band (1-based)","kind":{"type":"number"},"required":false,"default":1},
            {"name":"azimuth","label":"Azimuth (degrees)","kind":{"type":"number"},"required":false,"default":315},
            {"name":"altitude","label":"Sun Altitude (degrees)","kind":{"type":"number"},"required":false,"default":45},
            {"name":"zFactor","label":"Z Factor","kind":{"type":"number"},"required":false,"default":1},
            {"name":"output","label":"Output Name","kind":{"type":"text"},"required":false,"default":"Hillshade"}]}),
        json!({"id":"raster.calculator","name":"Raster Calculator","toolbox":"Raster Analysis › Map Algebra","summary":"Evaluates a band expression (b1, b2, …) with RustGIS rasterops.","engine":"rasterops","params":[
            {"name":"raster","label":"Input Raster","kind":{"type":"raster-source"},"required":true,"default":null},
            {"name":"expression","label":"Expression","kind":{"type":"text"},"required":true,"default":"b1"},
            {"name":"output","label":"Output Name","kind":{"type":"text"},"required":false,"default":"Raster_Calculator"}]})
    ];
    json!({"protocol":ADAPTER_PROTOCOL_VERSION,"upstream":UPSTREAM_REVISION,"tools":tools,"rasterTools":raster_tools})
}

fn dataset_geojson(dataset: &Dataset) -> Result<Value, String> {
    let (bytes, _dropped) = rustgis_data::io::write_bytes(dataset, "geojson")
        .map_err(|error| error.to_string())?;
    serde_json::from_slice(&bytes).map_err(|error| format!("RustGIS produced invalid GeoJSON: {error}"))
}

fn result_output(result: ToolResult) -> Result<Value, String> {
    let mut outputs = Vec::<Value>::new();
    match result.output {
        Output::Layer(dataset) | Output::Table(dataset) => {
            outputs.push(json!({
                "kind":"vector",
                "name":dataset.name,
                "geojson":dataset_geojson(&dataset)?
            }));
        }
        Output::Update { layer, dataset } => {
            outputs.push(json!({
                "kind":"update",
                "name":layer,
                "geojson":dataset_geojson(&dataset)?
            }));
        }
        Output::Selection { layer, rows } => {
            outputs.push(json!({"kind":"selection","name":layer,"rows":rows}));
        }
        Output::File { file_name, bytes } => {
            outputs.push(json!({
                "kind":"file",
                "name":file_name,
                "base64":base64::engine::general_purpose::STANDARD.encode(bytes)
            }));
        }
        Output::Layers(layers) => {
            for layer in layers {
                match layer {
                    OutputLayer::Vector(dataset) | OutputLayer::Table(dataset) => {
                        outputs.push(json!({
                            "kind":"vector",
                            "name":dataset.name,
                            "geojson":dataset_geojson(&dataset)?
                        }));
                    }
                    OutputLayer::Raster { name, file_name, bytes } => {
                        outputs.push(json!({
                            "kind":"raster-file",
                            "name":name,
                            "fileName":file_name,
                            "base64":base64::engine::general_purpose::STANDARD.encode(bytes)
                        }));
                    }
                }
            }
        }
    }
    Ok(json!({"outputs":outputs,"messages":result.messages}))
}

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
    "ready: RustGIS vector and raster analysis tools are available".to_owned()
}

/// Returns the actual RustGIS analysis tool catalogue and parameter definitions.
#[wasm_bindgen]
pub fn list_tools() -> Result<String, JsValue> {
    serde_json::to_string(&tool_catalogue()).map_err(js_error)
}

/// Runs a real RustGIS analysis tool.
///
/// `layers_json` is an object mapping layer names to GeoJSON FeatureCollections.
/// `params_json` uses the parameter names from RustGIS's ToolSpec catalogue.
#[wasm_bindgen]
pub fn run_tool(tool_id: &str, layers_json: &str, params_json: &str) -> Result<String, JsValue> {
    if layers_json.len().saturating_add(params_json.len()) > MAX_INPUT_BYTES {
        return Err(js_error("Input data exceeds the 128 MiB WebAssembly adapter limit."));
    }

    let layer_values: Value = serde_json::from_str(layers_json)
        .map_err(|error| js_error(format!("Invalid layer collection: {error}")))?;
    let layer_object = layer_values.as_object()
        .ok_or_else(|| js_error("layers_json must be an object of layer names and GeoJSON FeatureCollections."))?;
    if layer_object.is_empty() { return Err(js_error("Select at least one input layer.")); }
    let mut inputs = LayerInputs::default();
    let mut read_messages = Vec::<String>::new();
    for (name, payload) in layer_object {
        if name.trim().is_empty() { return Err(js_error("Layer names cannot be empty.")); }
        if payload.get("__kind").and_then(Value::as_str) == Some("geotiff") {
            let encoded = payload.get("base64").and_then(Value::as_str).ok_or_else(|| js_error(format!("Raster layer '{name}' has no GeoTIFF bytes.")))?;
            if encoded.len() > MAX_INPUT_BYTES { return Err(js_error(format!("Raster layer '{name}' exceeds the adapter input limit."))); }
            let bytes = base64::engine::general_purpose::STANDARD.decode(encoded).map_err(|error| js_error(format!("Raster layer '{name}' has invalid base64: {error}")))?;
            let file_name = payload.get("fileName").and_then(Value::as_str).unwrap_or(name);
            let raster = rustgis_data::raster::geotiff::read(Cursor::new(bytes), file_name).map_err(|error| js_error(format!("Could not read GeoTIFF layer '{name}': {error}")))?;
            inputs.rasters.insert(name.clone(), raster);
            continue;
        }
        let text = match payload { Value::String(text) => text.clone(), value => serde_json::to_string(value).map_err(js_error)? };
        if text.len() > MAX_INPUT_BYTES { return Err(js_error(format!("Layer '{name}' exceeds the adapter input limit."))); }
        let (mut dataset, report) = rustgis_data::io::read_geojson(&text, name).map_err(|error| js_error(format!("Could not read vector layer '{name}': {error}")))?;
        dataset.name = name.clone();
        read_messages.extend(report.messages);
        inputs.datasets.insert(name.clone(), dataset);
    }

    let params: Value = serde_json::from_str(params_json)
        .map_err(|error| js_error(format!("Invalid tool parameters: {error}")))?;
    let result = rustgis_analysis::run(tool_id, &inputs, &params)
        .map_err(js_error)?;
    let mut output = result_output(result).map_err(js_error)?;
    if let Some(object) = output.as_object_mut() {
        object.insert("inputMessages".into(), json!(read_messages));
        object.insert("tool".into(), json!(tool_id));
        object.insert("upstream".into(), json!(UPSTREAM_REVISION));
        object.insert("protocol".into(), json!(ADAPTER_PROTOCOL_VERSION));
    }
    serde_json::to_string(&output).map_err(js_error)
}

/// Runs a RustGIS rasterops algorithm and returns a georeferenced GeoTIFF.
#[wasm_bindgen]
pub fn run_raster_tool(tool_id: &str, input_base64: &str, input_name: &str, params_json: &str) -> Result<String, JsValue> {
    if input_base64.len() > MAX_INPUT_BYTES { return Err(js_error("Encoded GeoTIFF exceeds the 128 MiB adapter input limit.")); }
    let bytes = base64::engine::general_purpose::STANDARD.decode(input_base64).map_err(js_error)?;
    let raster = rustgis_data::raster::geotiff::read(Cursor::new(bytes), input_name).map_err(js_error)?;
    let params: Value = serde_json::from_str(params_json).map_err(js_error)?;
    let output_name = params.get("output").and_then(Value::as_str).filter(|s| !s.trim().is_empty()).unwrap_or("RustGIS_Raster_Output");
    let band = params.get("band").and_then(Value::as_u64).unwrap_or(1) as usize;
    if band == 0 || band > raster.band_count() { return Err(js_error(format!("Band must be between 1 and {}.", raster.band_count()))); }
    let band = band - 1;
    let values = match tool_id {
        "raster.slope" => {
            let zf = params.get("zFactor").and_then(Value::as_f64).unwrap_or(1.0);
            if !zf.is_finite() || zf <= 0.0 { return Err(js_error("Z Factor must be positive.")); }
            rustgis_analysis::rasterops::slope(&raster, band, zf).map_err(js_error)?
        }
        "raster.aspect" => rustgis_analysis::rasterops::aspect(&raster, band).map_err(js_error)?,
        "raster.hillshade" => {
            let az = params.get("azimuth").and_then(Value::as_f64).unwrap_or(315.0);
            let alt = params.get("altitude").and_then(Value::as_f64).unwrap_or(45.0);
            let zf = params.get("zFactor").and_then(Value::as_f64).unwrap_or(1.0);
            if !az.is_finite() || !alt.is_finite() || !(0.0..=90.0).contains(&alt) || !zf.is_finite() || zf <= 0.0 {
                return Err(js_error("Hillshade requires a finite azimuth, altitude from 0 to 90 degrees, and positive Z Factor."));
            }
            rustgis_analysis::rasterops::hillshade(&raster, band, az, alt, zf).map_err(js_error)?
        }
        "raster.calculator" => {
            let expr = params.get("expression").and_then(Value::as_str).unwrap_or("b1").to_ascii_lowercase();
            rustgis_analysis::rasterops::band_math(&raster, &expr).map_err(js_error)?
        }
        _ => return Err(js_error(format!("Unknown RustGIS raster operation: {tool_id}"))),
    };
    let output = rustgis_analysis::rasterops::derive(&raster, output_name, vec![values]).map_err(js_error)?;
    let output_bytes = rustgis_data::raster::geotiff::write(&output, 0).map_err(js_error)?;
    let output_layer_name = output.name.clone();
    let output_file_name = format!("{}.tif", output_layer_name);
    let (width, height, band_count) = (output.width, output.height, output.band_count());
    let metadata = json!({"width":width,"height":height,"bands":band_count,"crs":format!("{:?}",output.crs),
        "originX":output.transform.origin_x,"originY":output.transform.origin_y,"pixelWidth":output.transform.pixel_width,
        "pixelHeight":output.transform.pixel_height,"noData":output.nodata});
    let response = json!({"outputs":[{"kind":"raster-file","name":output_layer_name,"fileName":output_file_name,
        "base64":base64::engine::general_purpose::STANDARD.encode(output_bytes),"metadata":metadata}],
        "messages":[format!("RustGIS Raster operation completed: {} × {} pixels, {} band(s).",width,height,band_count)],
        "tool":tool_id,"upstream":UPSTREAM_REVISION,"protocol":ADAPTER_PROTOCOL_VERSION});
    serde_json::to_string(&response).map_err(js_error)
}


fn prepare_web_plugin(bytes: &[u8]) -> Result<rustgis_plugins::Plugin, String> {
    if bytes.is_empty() || bytes.len() > 64 * 1024 * 1024 {
        return Err("Plugin package must be between 1 byte and 64 MiB.".into());
    }
    let package = rustgis_plugins::Package::from_zip(bytes)
        .map_err(|error| format!("Invalid .rgplugin package: {error}"))?;
    if package.manifest.runtime != rustgis_plugins::Runtime::Wasm {
        return Err("Only sandboxed WebAssembly plugins are supported in the browser; process and skills packages are not executable here.".into());
    }
    rustgis_plugins::prepare(package, "web-upload")
}

fn web_plugin_info(plugin: &rustgis_plugins::Plugin) -> Value {
    let mut info = plugin.info();
    if let Some(object) = info.as_object_mut() {
        object.insert("permissions".into(), json!(plugin.manifest.permissions.iter().map(|p| p.code()).collect::<Vec<_>>()));
        object.insert("commands".into(), json!(plugin.manifest.commands.iter().map(|command| json!({
            "id": rustgis_plugins::command_id(&plugin.manifest.id, &command.id),
            "label": command.label,
            "help": command.help
        })).collect::<Vec<_>>()));
    }
    info
}

/// Parses the official RustGIS .rgplugin ZIP format and validates its manifest and WASM ABI.
#[wasm_bindgen]
pub fn inspect_plugin_package(bytes: &[u8]) -> Result<String, JsValue> {
    let plugin = prepare_web_plugin(bytes).map_err(js_error)?;
    serde_json::to_string(&web_plugin_info(&plugin)).map_err(js_error)
}

/// Installs a validated plugin. This browser host currently exposes no project, file or network
/// capabilities, so any manifest permission is rejected rather than silently over-granted.
fn install_web_plugin(bytes: &[u8]) -> Result<Value, String> {
    let plugin = prepare_web_plugin(bytes)?;
    if !plugin.manifest.permissions.is_empty() {
        let denied: Vec<&str> = plugin.manifest.permissions.iter().map(|p| p.code()).collect();
        return Err(format!("Host permissions are not enabled in this WebGIS build: {}. Install a no-permission plugin.", denied.join(", ")));
    }
    let installed = rustgis_plugins::register(plugin);
    Ok(web_plugin_info(&installed))
}

#[wasm_bindgen]
pub fn install_plugin_package(bytes: &[u8]) -> Result<String, JsValue> {
    serde_json::to_string(&install_web_plugin(bytes).map_err(js_error)?).map_err(js_error)
}

/// Returns installed sandboxed plugins.
#[wasm_bindgen]
pub fn list_plugins() -> Result<String, JsValue> {
    let plugins: Vec<Value> = rustgis_plugins::list().iter().map(|plugin| web_plugin_info(plugin)).collect();
    serde_json::to_string(&plugins).map_err(js_error)
}

/// Enables or disables an installed plugin by its manifest id.
#[wasm_bindgen]
pub fn set_plugin_enabled(id: &str, enabled: bool) -> Result<(), JsValue> {
    rustgis_plugins::set_enabled(id, enabled).map_err(js_error)
}

/// Removes an installed plugin by its manifest id.
#[wasm_bindgen]
pub fn uninstall_plugin(id: &str) -> bool {
    rustgis_plugins::unload(id)
}

/// Executes a plugin command with NoHost. The sandbox has no ambient imports; all host calls
/// fail closed until a reviewed, permission-aware WebGIS Host implementation is provided.
#[wasm_bindgen]
pub fn run_plugin_command(command_id: &str, params_json: &str) -> Result<String, JsValue> {
    if params_json.len() > 1024 * 1024 {
        return Err(js_error("Plugin command parameters exceed the 1 MiB limit."));
    }
    let params: Value = serde_json::from_str(params_json).map_err(js_error)?;
    let command = rustgis_plugins::command(command_id)
        .ok_or_else(|| js_error(format!("No enabled plugin command named '{command_id}'.")))?;
    if !command.plugin.manifest.permissions.is_empty() {
        return Err(js_error("This WebGIS host does not grant plugin host permissions."));
    }
    let mut host = rustgis_plugins::NoHost;
    let result = rustgis_plugins::run_command(&command, &mut host, &params).map_err(js_error)?;
    serde_json::to_string(&result).map_err(js_error)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn raster_slope_roundtrips_as_georeferenced_geotiff() {
        use rustgis_data::raster::{ColorHint, Crs, GeoTransform, Raster};
        let values: Vec<f32> = (0..25).map(|i| (i % 5) as f32 + (i / 5) as f32).collect();
        let raster = Raster::new("dem",5,5,vec![values],
            GeoTransform {origin_x:47.0,origin_y:34.0,pixel_width:0.001,pixel_height:-0.001},
            Crs::LonLat,Some(-9999.0),ColorHint::Gray,"Float32").expect("test raster should be valid");
        let slope = rustgis_analysis::rasterops::slope(&raster,0,1.0).expect("RustGIS slope should execute");
        let aspect = rustgis_analysis::rasterops::aspect(&raster,0).expect("RustGIS aspect should execute");
        let hillshade = rustgis_analysis::rasterops::hillshade(&raster,0,315.0,45.0,1.0).expect("RustGIS hillshade should execute");
        let calculated = rustgis_analysis::rasterops::band_math(&raster,"b1 * 2 + 1").expect("RustGIS band math should execute");
        assert_eq!(aspect.len(),25);
        assert_eq!(hillshade.len(),25);
        assert_eq!(calculated.len(),25);
        assert!(calculated.iter().any(|v| raster.is_valid(*v)));
        let output = rustgis_analysis::rasterops::derive(&raster,"slope_test",vec![slope]).expect("derived raster should be valid");
        let bytes = rustgis_data::raster::geotiff::write(&output,0).expect("RustGIS should write GeoTIFF");
        let reopened = rustgis_data::raster::geotiff::read(Cursor::new(bytes),"slope_test.tif").expect("GeoTIFF should reopen");
        assert_eq!((reopened.width,reopened.height,reopened.band_count()),(5,5,1));
        assert_eq!(reopened.transform,raster.transform);
        assert_eq!(reopened.crs,raster.crs);
        assert!(reopened.bands[0].data.iter().any(|v| reopened.is_valid(*v)));
    }


    fn test_plugin_package(permissions: &[&str]) -> Vec<u8> {
        use std::io::Write;
        let manifest = json!({
            "id":"sabz-test-plugin",
            "name":"Sabz Test Plugin",
            "version":"1.0.0",
            "runtime":"wasm",
            "abi":1,
            "entry":"plugin.wasm",
            "permissions":permissions,
            "commands":[{"id":"hello","label":"Hello"}]
        }).to_string();
        let wasm = wat::parse_str(r#"(module
            (memory (export "memory") 1)
            (data (i32.const 64) "{\\"ok\\":null}")
            (func (export "rustgis_alloc") (param i32) (result i32) (i32.const 0))
            (func (export "rustgis_call") (param i32 i32) (result i64)
                (i64.const 274877906955))
        )"#).expect("test module must compile");
        let cursor = std::io::Cursor::new(Vec::new());
        let mut archive = zip::ZipWriter::new(cursor);
        let options = zip::write::SimpleFileOptions::default().compression_method(zip::CompressionMethod::Stored);
        archive.start_file("plugin.json", options).expect("manifest entry should open");
        archive.write_all(manifest.as_bytes()).expect("manifest should be written");
        archive.start_file("plugin.wasm", options).expect("module entry should open");
        archive.write_all(&wasm).expect("module should be written");
        archive.finish().expect("plugin archive should finish").into_inner()
    }

    #[test]
    fn plugin_manager_inspects_installs_lists_and_executes_real_wasmi_commands() {
        let bytes = test_plugin_package(&[]);
        let inspected = prepare_web_plugin(&bytes).expect("official RustGIS package should validate");
        assert_eq!(inspected.manifest.id, "sabz-test-plugin");
        assert_eq!(inspected.manifest.commands.len(), 1);
        let installed = install_web_plugin(&bytes).expect("no-permission plugin should install");
        assert_eq!(installed["id"], "sabz-test-plugin");
        assert_eq!(installed["enabled"], true);
        let listed: Vec<Value> = rustgis_plugins::list().iter().map(|plugin| web_plugin_info(plugin)).collect();
        assert!(listed.iter().any(|plugin| plugin["id"] == "sabz-test-plugin"));
        let command = rustgis_plugins::command("plugin:sabz-test-plugin/hello")
            .expect("installed command should be registered");
        let mut host = rustgis_plugins::NoHost;
        let result = rustgis_plugins::run_command(&command, &mut host, &json!({}))
            .expect("wasmi command should execute in the sandbox");
        assert_eq!(result, Value::Null);
        assert!(rustgis_plugins::set_enabled("sabz-test-plugin", false).is_ok());
        assert!(rustgis_plugins::command("plugin:sabz-test-plugin/hello").is_none());
        assert!(rustgis_plugins::unload("sabz-test-plugin"));
    }

    #[test]
    fn plugin_manager_rejects_host_permissions_and_malformed_archives() {
        let privileged = test_plugin_package(&["edit"]);
        let error = install_web_plugin(&privileged).expect_err("edit permission must fail closed");
        assert!(error.contains("Host permissions are not enabled"));
        assert!(prepare_web_plugin(b"not a zip archive").is_err());
        assert!(prepare_web_plugin(&[]).is_err());
    }

    #[test]
    fn buffer_runs_through_the_pinned_rustgis_analysis_engine() {
        let geojson = r#"{
            "type":"FeatureCollection",
            "features":[
                {"type":"Feature","id":1,"properties":{"site":"test"},"geometry":{"type":"Point","coordinates":[47.75,33.62]}}
            ]
        }"#;
        let (dataset, _) = rustgis_data::io::read_geojson(geojson, "test_points")
            .expect("test GeoJSON must be readable");
        let mut inputs = LayerInputs::default();
        inputs.datasets.insert("test_points".into(), dataset);

        let result = rustgis_analysis::run(
            "buffer",
            &inputs,
            &json!({
                "input":"test_points",
                "distance":250,
                "units":"Meters",
                "dissolve":"None",
                "output":"test_buffers"
            }),
        ).expect("RustGIS Buffer should execute");

        let Output::Layer(output) = result.output else {
            panic!("RustGIS Buffer should return a vector layer");
        };
        assert!(!output.features.is_empty(), "Buffer should produce at least one polygon");
        let encoded = dataset_geojson(&output).expect("Buffer output should serialize to GeoJSON");
        assert_eq!(encoded.get("type").and_then(Value::as_str), Some("FeatureCollection"));
        assert_eq!(encoded.get("features").and_then(Value::as_array).map(Vec::len), Some(output.features.len()));
    }
}
