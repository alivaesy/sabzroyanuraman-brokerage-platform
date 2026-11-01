# WebGIS local vendor assets

The WebGIS page now prefers same-origin files under this directory. Leaflet,
Leaflet Draw, Turf, Proj4, shpjs, shp-write, JSZip and GDAL.js/WASM are already
stored here and are loaded locally.

Three browser distributions are generated from pinned npm versions because the
repository contains GeoTIFF's distribution only as a CDN dependency and the
DXF Writer package source but not its browserified output, and the GoogleMutant plugin distribution.

## Prepare the two missing bundles

From this directory or any other directory, run:

```bash
bash vendor/web-gis/prepare-browser-assets.sh
```

The script requires Node.js/npm, network access to npm for this one-time
preparation, and writes:

- `geotiff/geotiff.js` — GeoTIFF 2.1.3 browser bundle
- `dxf-writer/dxf-writer.js` — DXF Writer 1.18.4 browser bundle, exposing
  the `Drawing` global used by the existing DXF export code
- `leaflet-google-mutant/Leaflet.GoogleMutant.js` — Leaflet Google basemap plugin 0.15.0
- `browser-assets-manifest.txt` — versions and SHA-256 checksums

Until these generated files exist, the page retains a CDN fallback for
GeoTIFF and DXF Writer. Once prepared and deployed, both load locally first
and no CDN request is needed for these libraries.

## GDAL.js

GDAL.js 2.8.1, `gdal3WebAssembly.wasm`, and
`gdal3WebAssembly.data` are already present in `gdal3.js/`. The page now
passes explicit same-origin WASM/data paths. The existing `useWorker:false`
mode is intentionally preserved to avoid changing the raster execution model
without a browser regression test. Enabling the worker is a separate test and
must include a same-origin worker URL.

## External services

Basemap tiles, WMS/XYZ services, Google Maps, and other user-configured
network services remain network-dependent by design. Local vendor assets remove
third-party CDN dependencies from the GIS runtime; they do not make remote map
services available offline.
