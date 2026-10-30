# RustGIS Raster / GeoTIFF implementation notes

## Scope

This document supplements `RUSTGIS-WEBGIS-HANDOFF-FA.md` with the Raster integration added after the initial Vector-only adapter.

Repository: `alivaesy/sabzroyanuraman-brokerage-platform`  
Working branch: `feat/webgis-rustgis-integrated-package`  
PR: [#41](https://github.com/alivaesy/sabzroyanuraman-brokerage-platform/pull/41)  
Pinned RustGIS revision: `38f24336140d59092a31f964e9ac86aab18fdecd`

## Actual upstream APIs used

- Read input GeoTIFF: `rustgis_data::raster::geotiff::read(Cursor::new(bytes), name)`
- Terrain and band operations: `rustgis_analysis::rasterops::{slope, aspect, hillshade, band_math}`
- Build output raster on the source grid: `rustgis_analysis::rasterops::derive`
- Write output GeoTIFF: `rustgis_data::raster::geotiff::write(&raster, 0)`

These are upstream RustGIS APIs at the pinned commit, not JavaScript reimplementations.

## Adapter API

File: `sabzroyanuraman-site/rustgis-wasm/src/lib.rs`

- `run_raster_tool(tool_id, input_base64, input_name, params_json)`
- `run_tool` also accepts a layer payload object with `__kind: "geotiff"`, `fileName`, and `base64`; it reads the raster and exposes it through `Inputs::raster`.

The adapter protocol version is 3.

## Raster operations exposed in the RG toolbox

| Tool ID | Operation | Main parameters |
|---|---|---|
| `raster.slope` | Slope in degrees | raster, band, zFactor, output |
| `raster.aspect` | Aspect in degrees | raster, band, output |
| `raster.hillshade` | Hillshade | raster, band, azimuth, altitude, zFactor, output |
| `raster.calculator` | Band arithmetic | raster, expression (e.g. `b1 + b2 * 2`), output |

The custom catalogue entries call the RustGIS `rasterops` functions through the Adapter. They are not presented as upstream `ToolSpec` entries.

The actual upstream tool catalogue also includes Raster-aware tools such as Zonal Statistics. The new `Inputs::raster` implementation allows a selected GeoTIFF to be supplied alongside the required polygon layer. Tools requiring a file destination or a map-style-aware tile provider remain disabled or unavailable.

## Browser integration

- Existing Raster Import workspace reads the original GeoTIFF and registers its bytes in `window.__sabzGISRasterSources`.
- `webgis-rustgis-runtime.js` adds Raster selectors to the RG panel and sends the source bytes to WASM.
- Raster outputs are returned as `kind: "raster-file"` with base64-encoded TIFF bytes.
- The runtime downloads the TIFF and uses GeoTIFF.js to build a lightweight Leaflet image preview.
- The bridge adds the preview to the map/TOC, but marks it as a session preview so the vector-only project serializer does not silently persist it as an empty vector layer.

The TIFF download is the real output. The Leaflet preview is only a display representation.

## Current limitations

- Input sources and output previews are session-scoped; raster persistence in saved WebGIS projects is not implemented.
- The output preview is limited to GeoTIFFs whose CRS can be transformed by the browser's available Proj4 definitions.
- Raster-to-PMTiles needs a map/style-aware tile provider and is not connected.
- Selection output is still reported, not applied to the WebGIS selection model.
- Browser end-to-end verification remains outstanding.
- Respect the 128 MiB encoded adapter input limit and the upstream RustGIS WASM sample limit.

## Validation status

The native Buffer test passed before Raster was added. A native Slope-to-GeoTIFF round-trip test was added for Raster. The latest run must be inspected before declaring success:

- Verify Native Test.
- Verify optimized WASM build.
- Verify Runtime JavaScript syntax and artifact upload.
- Verify the Web GIS checks workflow.
- Perform browser testing with a real GeoTIFF and verify CRS, dimensions, transform, NoData and pixel values after reopening the output.

Do not merge PR #41 or deploy to the live site without the repository owner's explicit approval.


## CI evidence

Workflow run `41071256533` on commit `f19fea52dbae89be0defbda257ec27e117236078` completed successfully. The following steps all passed:

- Native adapter tests, including the Raster slope-to-GeoTIFF round-trip.
- Optimized WebAssembly package build.
- Browser runtime JavaScript syntax check.
- Upload of the `sabzroyan-rustgis-wasm` artifact (about 1.6 MB).

The artifact is available in GitHub Actions for 14 days from 2026-10-30. Later commits bumped the adapter protocol to version 3 and expanded the Raster test to exercise Aspect, Hillshade and band math; their newer CI runs were still in progress at the time of this note. Therefore the successful run proves the Raster implementation builds, but not yet the final protocol-v3/test-expanded commit.
