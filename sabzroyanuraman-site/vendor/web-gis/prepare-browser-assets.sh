#!/usr/bin/env bash
set -euo pipefail

# Build the two browser distributions missing from the checked-in vendor tree.
# Run from any directory; output paths are resolved relative to this script.
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
mkdir -p "$HERE/geotiff" "$HERE/dxf-writer" "$HERE/leaflet-google-mutant"

command -v npm >/dev/null 2>&1 || { echo "npm is required (Node.js LTS)." >&2; exit 1; }
command -v tar >/dev/null 2>&1 || { echo "tar is required." >&2; exit 1; }

echo "[1/2] Fetching GeoTIFF 2.1.3 browser distribution..."
(
  cd "$TMP"
  npm pack geotiff@2.1.3 --silent
)
GEOTIFF_TGZ="$(find "$TMP" -maxdepth 1 -name 'geotiff-2.1.3.tgz' -print -quit)"
test -n "$GEOTIFF_TGZ"
mkdir -p "$TMP/geotiff"
tar -xzf "$GEOTIFF_TGZ" -C "$TMP/geotiff"
GEOTIFF_SRC="$TMP/geotiff/package/dist-browser/geotiff.js"
test -s "$GEOTIFF_SRC" || { echo "GeoTIFF browser bundle not found in npm package." >&2; exit 1; }
cp -a "$TMP/geotiff/package/dist-browser/." "$HERE/geotiff/"
test -s "$HERE/geotiff/geotiff.js"

echo "[2/2] Building dxf-writer 1.18.4 browser bundle..."
(
  cd "$TMP"
  npm pack dxf-writer@1.18.4 --silent
)
DXF_TGZ="$(find "$TMP" -maxdepth 1 -name 'dxf-writer-1.18.4.tgz' -print -quit)"
test -n "$DXF_TGZ"
mkdir -p "$TMP/dxf-writer"
tar -xzf "$DXF_TGZ" -C "$TMP/dxf-writer"
DXF_ENTRY="$TMP/dxf-writer/package/src/Drawing.js"
test -s "$DXF_ENTRY" || { echo "DXF Writer Drawing.js entry not found." >&2; exit 1; }
npx --yes browserify@16.2.3 "$DXF_ENTRY" --standalone Drawing -o "$HERE/dxf-writer/dxf-writer.js"
test -s "$HERE/dxf-writer/dxf-writer.js"

{
  echo "geotiff 2.1.3 dist-browser/geotiff.js"
  echo "dxf-writer 1.18.4 browserify@16.2.3 (Drawing global)"
  sha256sum "$HERE/geotiff/geotiff.js" "$HERE/dxf-writer/dxf-writer.js"
} > "$HERE/browser-assets-manifest.txt"

echo "[3/3] Fetching Leaflet.GoogleMutant 0.15.0..."
(
  cd "$TMP"
  npm pack leaflet.gridlayer.googlemutant@0.15.0 --silent
)
GM_TGZ="$(find "$TMP" -maxdepth 1 -name 'leaflet.gridlayer.googlemutant-0.15.0.tgz' -print -quit)"
test -n "$GM_TGZ"
mkdir -p "$TMP/google-mutant"
tar -xzf "$GM_TGZ" -C "$TMP/google-mutant"
GM_SRC="$TMP/google-mutant/package/dist/Leaflet.GoogleMutant.js"
test -s "$GM_SRC" || { echo "Leaflet.GoogleMutant distribution not found." >&2; exit 1; }
install -m 0644 "$GM_SRC" "$HERE/leaflet-google-mutant/Leaflet.GoogleMutant.js"

{
  echo "leaflet.gridlayer.googlemutant 0.15.0"
  sha256sum "$HERE/leaflet-google-mutant/Leaflet.GoogleMutant.js"
} >> "$HERE/browser-assets-manifest.txt"

echo "Browser assets prepared:"
ls -lh "$HERE/geotiff/geotiff.js" "$HERE/dxf-writer/dxf-writer.js" "$HERE/leaflet-google-mutant/Leaflet.GoogleMutant.js"
