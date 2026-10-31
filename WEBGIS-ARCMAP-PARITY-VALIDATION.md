# WebGIS ArcMap-parity completion and validation

This checklist tracks work that must not be treated as complete merely because the UI or API code exists.

## Fixed in this branch
- The plugin manager now resolves `window.SabzRustGISPluginBridge`.
- The vector/WASM runtime publishes its plugin ABI on that dedicated namespace.
- The raster adapter keeps `window.SabzRustRasterBridge` for raster operations, preventing the two adapters from overwriting one another.
- The plugin runtime remains WASM/wasmi-based; this change does not grant JavaScript plugins filesystem, process, or unrestricted network access.

## Required validation before production sign-off

### 1. Plugin Manager
- Open deployed WebGIS with a clean browser profile.
- Confirm the plugin manager loads vector WASM and lists installed plugins.
- Inspect and install a known-good `.rgplugin`; verify manifest and permission approval.
- Run a harmless fixture command; verify output, enable/disable, uninstall, and reload persistence.
- Confirm raster loading and processing still work after plugin initialization.
- Check console/network logs for module-load, WASM MIME-type, CORS, and 404 errors.

### 2. Server-backed GeoTIFF persistence
- Use an authenticated account and a valid georeferenced GeoTIFF (not a plain TIFF or preview-only image).
- Upload through the production raster asset API; verify HTTP status, asset identifier, byte length, and SHA-256.
- Download the same asset in a second independent browser/profile and compare SHA-256.
- Open the restored raster and verify dimensions, band count, CRS/EPSG, geotransform, NoData, and pixel read-back.
- Test unauthenticated access, invalid/truncated TIFF, CRS mismatch, oversized upload, and failed commit.
- Do not claim cross-browser persistence based only on IndexedDB tests; IndexedDB is browser-local.

### 3. ArcMap 10.8 geometry parity
Create a versioned fixture set in EPSG:32638 and run identical inputs/parameters in ArcMap 10.8 and WebGIS:
- Buffer: planar distance, dissolve modes, multipart behavior, joins, negative polygon buffers, and field preservation.
- Clip: point, line, polygon, multipart, boundary-touching, holes, and sliver cases.
- Intersect: supported geometry combinations, coincident edges, overlaps, multipart output, and attribute rules.
- Erase: polygon erase mask, multipart masks, holes, touching boundaries, and supported input geometry types.
- Compare geometry type, feature count, attributes, area/length, and coordinates with documented XY tolerance.
- Record expected differences caused by projection, precision, or algorithm semantics. Do not label parity verified without ArcMap reference outputs.

### 4. ModelBuilder to RustGIS
- Keep the current model JSON/graph format backward compatible.
- Route supported processing nodes through RustGIS WASM vector/raster runtime rather than silently falling back to Turf.
- Add capability checks and actionable errors for unsupported nodes.
- Preserve dependency ordering, cycle detection, cancellation, progress, and intermediate outputs.
- Add integration tests for vector-only, raster-only, mixed workflows, invalid parameters, missing inputs, and failed nodes.

## Current status
- Plugin bridge namespace collision: corrected in this branch; runtime verification still required.
- Raster server API and project sync: code exists, but authenticated production upload/download across two independent browsers has not been demonstrated here.
- Buffer/Clip/Intersect/Erase: current WebGIS paths still include Turf-based processing; ArcMap parity is not established.
- ModelBuilder: graph execution exists, but full RustGIS-backed node execution is not established.
- Deployment: hosting cron is expected to deploy master. Do not manually overwrite production files; verify the deployed commit after merge.

## Acceptance rule
A code path, static test, synthetic fixture, or successful CI run is not a substitute for an authenticated production test or an ArcMap 10.8 reference run. Record test environment, fixture hash, software versions, result, and evidence for each sign-off item.