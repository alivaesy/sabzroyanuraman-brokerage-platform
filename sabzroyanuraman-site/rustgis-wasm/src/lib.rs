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
const ADAPTER_PROTOCOL_VERSION: u32 = 2;
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
        let output = rustgis_analysis::rasterops::derive(&raster,"slope_test",vec![slope]).expect("derived raster should be valid");
        let bytes = rustgis_data::raster::geotiff::write(&output,0).expect("RustGIS should write GeoTIFF");
        let reopened = rustgis_data::raster::geotiff::read(Cursor::new(bytes),"slope_test.tif").expect("GeoTIFF should reopen");
        assert_eq!((reopened.width,reopened.height,reopened.band_count()),(5,5,1));
        assert_eq!(reopened.transform,raster.transform);
        assert_eq!(reopened.crs,raster.crs);
        assert!(reopened.bands[0].data.iter().any(|v| reopened.is_valid(*v)));
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
