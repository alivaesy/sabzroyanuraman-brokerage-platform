# ArcMap 10.8 extraction worker

This is the first real extraction component for the WebGIS import pipeline. It must run **on a Windows machine with a licensed ArcMap Desktop 10.8 installation**, using ArcMap's own Python 2.7 interpreter. It is not a Linux/cPanel PHP script and must not be exposed as an unauthenticated web endpoint.

## Run

Open the ArcMap Python Command Prompt and run:

```bat
python tools\arcmap108-export\ExportArcMap108.py "D:\data\project.mxd" "D:\exports\project"
```

The worker creates `D:\exports\project.zip`, containing `manifest.json` and exported GeoJSON layers. Keep the source MXD and its referenced data accessible to the ArcMap account; repair broken layer sources before export.

## What is extracted

- Map/document metadata and data-frame extent, scale, rotation and spatial reference.
- Layer order, names, visibility, scale ranges, definition query, source path, geometry type, extent, spatial reference, feature count and field schema.
- A portable summary of common renderers and label-class expressions/settings.
- WGS84 GeoJSON exports and per-layer source/export feature-count checks.
- A manifest listing export errors, count mismatches, field-name differences and known limitations.

## Important limits

This is a first extraction pass, not a claim of perfect ArcMap reproduction. Exact symbol layers, picture markers, fonts, label placement, page layouts, map surrounds and cartographic effects need additional serialization and a WebGIS renderer. GeoJSON cannot preserve all FileGDB semantics. Raster/mosaic, annotation, topology rules/errors, network datasets, domains/subtypes, relationship classes, attachments, field defaults/nullability and coded/range-domain behavior need separate extraction/export paths. The manifest records these limitations rather than silently dropping them.

Counts are checked against the ArcMap layer as configured. Definition queries and selections can affect counts. Field presence is checked from exported GeoJSON properties; this is a diagnostic, not a full semantic-equivalence proof. Review `manifest.json` before accepting an import.

## WebGIS integration contract (next stage)

The WebGIS should accept a ZIP produced by this worker and:
1. Validate archive paths and require a valid `manifest.json`.
2. Display a preflight report before adding any layer.
3. Match each manifest layer to its GeoJSON by the manifest's relative `export.path`.
4. Rebuild supported simple/unique/graduated renderer summaries and label classes, clearly flagging unsupported styles.
5. Compare source/export feature counts and field names; let the user cancel or import with warnings.
6. Keep the manifest with the project so source metadata and limitations remain inspectable.

Do not let a browser upload execute scripts or commands on the Windows worker. If this is later exposed as a service, isolate it on a private network, authenticate requests, enforce size/time limits, use per-job temporary directories, disable arbitrary paths, and remove uploaded source files according to an explicit retention policy.
