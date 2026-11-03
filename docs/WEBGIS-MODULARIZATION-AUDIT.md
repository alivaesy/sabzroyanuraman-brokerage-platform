# WebGIS modularization audit

Review date: 2026-11-03  
Source branch: `master`  
Primary file: `sabzroyanuraman-site/web-gis.html`  
Latest inspected HEAD before this audit: `bf0b8a41f9781a235b1298151c990a5f69293e68`

## 1. Verified baseline

- `web-gis.html`: 948,979 characters, 11,178 lines.
- Largest inline script: 736,929 characters (one classic-script IIFE).
- The page contains 35 script elements: 18 external script tags and 17 inline script blocks.
- The HTML contains 8 inline style blocks; the largest is 61,823 characters.
- Source scan found 110 `.addEventListener(...)` registrations and 188 inline event-handler attributes. These counts are syntactic inventory, not unique runtime listeners.
- There are already standalone WebGIS modules; this is not a clean single-file application.

## 2. Existing JavaScript modules (do not duplicate)

- `js/webgis-toc-enhancements.js` — TOC enhancements (14,339 chars).
- `webgis-gdb-catalog.js` — FileGDB picker and non-spatial table view (9,170 chars).
- `webgis-gdb-catalog-tree.js` — ArcCatalog-style geodatabase tree (25,165 chars).
- `webgis-arcmap-manifest.js` — ArcMap manifest import / Repair Data Source (12,659 chars).
- `webgis-rustgis-enhancements.js` — ModelBuilder and Layout enhancements (8,186 chars).
- Other separate runtimes already loaded include raster, RustGIS vector, plugin manager, GeoLibre bridge and related assets.

The Catalog/TOC scripts use an existing bridge/state owned by the main inline script. Their physical separation therefore does not mean they are independent modules.

## 3. Main inline script: initial functional inventory

The large IIFE currently owns or contains these functional areas:

- Basemap selection, Wayback imagery, CRS definitions and coordinate projection.
- Map drawing, GPS/XY, measurement and navigation.
- Editing lifecycle, edit targets, undo/redo, geometry operations and topology.
- Layer registration, layer list, selection, spatial selection and zoom-to-selection.
- Attribute table, attribute editing, field expressions and table persistence.
- File imports: GDB, DXF, KML, GPX, CSV/Excel and ArcMap document entry point.
- Project serialization, IndexedDB/local persistence, cloud project API and GIS authentication.
- Export: SHP, DXF, GDB, KML/KMZ, GPX and project downloads.
- Geoprocessing, partitioned overlay operations and RustGIS vector adapter.
- Symbology, layer properties, definition queries and labeling.
- ModelBuilder, raster processing and Layout-related code.

These areas are intertwined inside one IIFE. A function-name list alone is not a safe extraction plan; call relationships, captured lexical variables, initialization order and DOM dependencies must be mapped before moving code.

## 4. Existing automated tests and CI

The repository already has meaningful tests; the task is to expand and use them, not replace them:

- `tests/web-gis-static.test.cjs` — 42,288 chars; includes broad source/registration assertions.
- `tests/web-gis-browser-regression.cjs` — Playwright browser regression against a served WebGIS URL.
- `tests/web-gis-browser.spec.cjs` — Playwright Test browser smoke coverage.
- `tests/web-gis-gdb-catalog-tree-regression.cjs`
- `tests/web-gis-gdb-context-menu-regression.cjs`
- `tests/web-gis-arcmap-import-regression.cjs`
- `tests/web-gis-toc-enhancements.test.cjs`
- Raster/WASM tests are also present.

`.github/workflows/web-gis-check.yml` already has separate static-checks and browser-regression jobs. The browser job builds the vector WASM package, serves the checked-out site and runs Playwright regression scripts. Therefore, do not introduce a second competing test framework before auditing current coverage.

## 5. Important architectural constraints

1. The main script is a classic IIFE and publishes `window.__sabzWebGISSharedBridge` early. External scripts consume this bridge and other explicit globals. Converting the whole page to ES modules in one step would change execution timing and global visibility.
2. Several scripts are loaded with `defer`, while others are classic blocking scripts. Preserve and explicitly test ordering during extraction.
3. The main IIFE captures many lexical functions and state values. Extracting a function that closes over these values requires an explicit dependency interface; copying it into a new file will not preserve those bindings.
4. There are inline event-handler attributes in the HTML. These depend on global function visibility and must remain operational until deliberately migrated.
5. The current page includes external fallback URLs for DXF writer, GeoTIFF and Leaflet Google Mutant, plus Google Maps script. CDN/self-hosting is a separate issue and should not be mixed into the first modularization commits.

## 6. Proposed safe extraction sequence

This is a dependency-aware starting order, not a blind commitment to the original proposed folder order:

1. Establish a reproducible Playwright baseline and record current browser failures before moving code.
2. Document and formalize the existing shared bridge/API; add contract tests for bridge methods consumed by external modules.
3. Choose the smallest self-contained feature after call/dependency inspection. Candidate areas: isolated UI enhancement or a leaf utility with no captured state. Do not assume Symbology or Attribute Table is independent merely because it has a clear name.
4. Extract one feature behind a compatibility facade; preserve existing public/global entry points temporarily.
5. Run static tests, targeted feature regression, full browser regression, and compare console/page errors after each extraction.
6. Repeat with Catalog/TOC, attribute table, symbology/labeling, import/export, editing, geoprocessing and services according to measured coupling.
7. Extract map initialization and core state last.
8. Only after stable modularization, consider replacing the compatibility facade and reducing legacy inline handlers.

## 7. Immediate next actions

- Build a function/variable/call-reference inventory for the main IIFE, including reads/writes of shared state and the external bridge contract.
- Identify all inline script block purposes and exact execution order.
- Map current test coverage to user-visible workflows and record any uncovered functions.
- Select the first extraction only after these reports are complete. No production code was moved in this audit.

## 8. Verification status

This document is based on GitHub source inspection at the recorded HEAD. No code was changed in this audit, and no local Playwright/browser execution was performed in this session. Existing CI configuration was inspected, but no new test result is claimed here.
