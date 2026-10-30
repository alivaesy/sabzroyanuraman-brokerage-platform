//! Real RustGIS geoprocessing bridge for the Sabz Royan WebGIS.
//!
//! Input and output geometries cross the boundary as GeoJSON (WGS 84 coordinates).
//! Algorithms are executed by rustgis-analysis; this crate does not reimplement them.

use std::collections::HashMap;

use base64::Engine as _;
use rustgis_analysis::{Inputs, Output, OutputLayer, ParamKind, ToolResult};
use rustgis_data::Dataset;
use serde_json::{Value, json};
use wasm_bindgen::prelude::*;

const UPSTREAM_REVISION: &str = "38f24336140d59092a31f964e9ac86aab18fdecd";
const ADAPTER_PROTOCOL_VERSION: u32 = 2;
const MAX_INPUT_BYTES: usize = 128 * 1024 * 1024;

#[derive(Default)]
struct LayerInputs {
    datasets: HashMap<String, Dataset>,
}

impl Inputs for LayerInputs {
    fn dataset(&self, layer: &str) -> Option<&Dataset> {
        self.datasets
            .get(layer)
            .or_else(|| self.datasets.iter().find(|(name, _)| name.eq_ignore_ascii_case(layer)).map(|(_, ds)| ds))
    }

    fn selection(&self, _layer: &str) -> &[usize] {
        &[]
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
    json!({"protocol":ADAPTER_PROTOCOL_VERSION,"upstream":UPSTREAM_REVISION,"tools":tools})
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
    "ready: RustGIS analysis tools are available".to_owned()
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
    if layer_object.is_empty() {
        return Err(js_error("Select at least one vector input layer."));
    }

    let mut inputs = LayerInputs::default();
    let mut read_messages = Vec::<String>::new();
    for (name, geojson) in layer_object {
        if name.trim().is_empty() {
            return Err(js_error("Layer names cannot be empty."));
        }
        let text = match geojson {
            Value::String(text) => text.clone(),
            value => serde_json::to_string(value).map_err(js_error)?,
        };
        if text.len() > MAX_INPUT_BYTES {
            return Err(js_error(format!("Layer '{name}' exceeds the adapter input limit.")));
        }
        let (mut dataset, report) = rustgis_data::io::read_geojson(&text, name)
            .map_err(|error| js_error(format!("Could not read layer '{name}': {error}")))?;
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
