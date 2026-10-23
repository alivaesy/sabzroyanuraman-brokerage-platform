# Web GIS vendored dependencies

Downloaded from pinned npm package versions for local hosting. The main
`web-gis.html` file is intentionally not modified; CDN references remain
active until local assets are tested and switched in a separate change.

See `manifest.txt` and `licenses/` for package versions and license files.
GDAL3.js runtime resources are kept together because its WASM/worker files
must remain available beside the JavaScript entry point.
