# WebGIS module dependency map

Review date: 2026-11-03  
Inspected source HEAD: `162b39fa6aaa0403296031d5037be35661f6eadf`

## Core boundary

The main application is a classic-script IIFE. It creates the Leaflet map and owns mutable state such as `map`, `namedLayers`, `selectedFeatures`, `currentCRS`, editing state, project persistence and renderer functions. It publishes `window.__sabzWebGISSharedBridge` before map initialization is complete so later scripts can detect startup state.

Do not convert this IIFE to `type="module"` during incremental extraction. Classic-script global bindings, inline event handlers, script order and IIFE-private lexical state are part of the current compatibility contract.

## External module consumers

| Existing file | Main dependencies | Current coupling |
|---|---|---|
| `js/webgis-toc-enhancements.js` | bridge: map, currentCRS, namedLayers, renderLayerList, tocViewMode, contextLayerItem, selectedFeatures, layer properties, selection, editor refresh and autosave methods | Medium; reads live map/layer state but does not reach into IIFE lexical variables |
| `webgis-gdb-catalog.js` | shared bridge, JSZip, SabzGdbCatalogTree | Low-to-medium; uses separate GDB APIs and DOM |
| `webgis-gdb-catalog-tree.js` | bridge: namedLayers, currentCRS, status, map, autosave, rename/delete layer and attribute-table methods; `__webGisGdbBridge`; Catalog state | Medium; relies on explicit shared bridge and GDB bridge |
| `webgis-arcmap-manifest.js` | bridge: map, saveLocalProject, namedLayers, importGDBFiles, addGeoJSONLayer; JSZip/shp | Medium; import operations mutate core layer/project state through bridge |
| `webgis-rustgis-vector-runtime.js` | bridge: currentCRS, namedLayers, status, addGeoJSONLayer, applyRustGISSelection; optional raster registration/overlay capabilities | Medium-to-high; processing adapter and raster compatibility hooks |
| `webgis-rustgis-enhancements.js` | `window.__sabzInstallModelBuilderRenderer` callback supplied by core | Low; enhancement decorator, loaded after renderer factory |
| `raster-runtime.js` | IndexedDB and GeoTIFF library | Low; storage/codec runtime, not the map UI |

## Bridge contract observed in core

The core bridge currently exposes:

- State/readers: `map`, `currentCRS`, `namedLayers`, `renderLayerList`, `tocViewMode`, `contextLayerItem`, `selectedFeatures`, `drawn`.
- Layer actions: `addNamedLayer`, `renameNamedLayer`, `removeNamedLayer`, `canRemoveNamedLayer`, `openAttributeTableForLayer`, `openLayerProperties`.
- Editing/selection: `refreshEditorTargets`, `refreshAttributeLayers`, `selectedRecords`, `applySelectionVisual`, `hideLayerContextMenu`, `zoomToSelection`, `applyRustGISSelection`.
- Persistence/import: `restoreProject`, `serializeProject`, `saveLocalProject`, `listLocalProjects`, `importGDBFiles`.
- Utilities: `status`, `layerPropertiesRecords`, `layerStyleRamp`, `scheduleAutoSave`, `addGeoJSONLayer`.

The vector runtime dynamically adds `registerRasterSource` to the bridge when it is absent, and conditionally wraps `addRasterOverlay` if a provider supplies it. Neither `rasterSources` nor `addRasterOverlay` is currently declared by the core bridge. Treat those as optional capabilities; do not assume the runtime hook itself creates a core raster overlay implementation.

## Main IIFE coupling samples

- `selectBasemap` captures `map`, `base`, `currentBase`, `currentBasemapKey`, Wayback panel state, `status` and `scheduleAutoSave`.
- `addNamedLayer` captures `namedLayers`, `map`, `layerControl`, undo state and editor/table refresh operations.
- `openLayerProperties` is a large UI workflow coupled to layer renderer state, map/layer controls, selected overlay and autosave.
- `runGisOperation` captures selection state, Turf/RustGIS adapters, map state and autosave.
- ModelBuilder spans UI, graph validation, layer discovery, geometry operations, map registration and cancellation. It is not a low-risk first extraction.
- Raster workspace UI currently appears after the core IIFE in the same inline script and references map/core helpers. Moving it requires an explicit injected API rather than copying the block as-is.

## First extraction in this stage: GPS tracking

The GPS tracking implementation is a bounded state object and small set of functions, without direct access to private editing, renderer or project internals.

New module: `sabzroyanuraman-site/webgis-gps-tracking.js`

Dependencies:

- `window.__sabzWebGISSharedBridge.map` (with `window.__sabzGISMap` fallback),
- global Leaflet `window.L`,
- browser Geolocation, Fetch, Blob, URL, Crypto and DOM APIs,
- same-origin endpoint `/api/location/track.php`,
- existing Go To XY dialog status and button elements.

Public surface: `window.SabzGpsTrack.start/stop/exportKml/clear/status`, plus read-only `active` and `pointCount`. The Go To XY dialog delegates to this API and no longer owns GPS state or implementation details.

The module is loaded immediately after the core script, so the bridge owner executes first. It remains a classic script to preserve compatibility with the existing page.

## Remaining extraction order

1. GPS Tracking — extracted in this change; verify via browser geolocation and mocked same-origin endpoint.
2. Formalize and test shared bridge contracts for TOC, Catalog, ArcMap manifest and RustGIS.
3. Extract other low-coupling UI helpers only after identifying all captured state and external entry points.
4. Decompose ModelBuilder and Raster Workspace only after defining explicit service interfaces.
5. Attribute Table, Symbology/Labeling, Editing, Geoprocessing and Project persistence later; these are highly coupled.
6. Map initialization and core state last.

## Verification policy

Static tests verify module loading and API contracts. The Playwright browser regression exercises actual geolocation, the Start/Point/Stop request sequence, and Go To XY controls with a mocked endpoint. Passing syntax checks alone is not evidence that the browser workflow works.
