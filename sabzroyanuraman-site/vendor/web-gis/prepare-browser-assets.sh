#!/usr/bin/env bash
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

for cmd in curl tar python3 webpack sha256sum; do
  command -v "$cmd" >/dev/null 2>&1 || {
    echo "Required command not found: $cmd" >&2
    exit 1
  }
done

fetch_npm() {
  local name="$1" version="$2" prefix="$3"
  local meta="$TMP/${prefix}-metadata.json"
  local archive="$TMP/${prefix}.tgz"
  local url

  echo "Fetching ${name}@${version}..." >&2
  curl -fLsS --connect-timeout 20 --max-time 120 \
    --retry 3 --retry-delay 3 \
    "https://registry.npmjs.org/${name}/${version}" -o "$meta"

  url="$(python3 -c \
    'import json,sys; print(json.load(open(sys.argv[1]))["dist"]["tarball"])' \
    "$meta")"

  test -n "$url"
  curl -fLsS --connect-timeout 20 --max-time 300 \
    --retry 3 --retry-delay 5 "$url" -o "$archive"

  tar -tzf "$archive" >/dev/null
  echo "$archive"
}

echo "[1/3] GeoTIFF"
GEOTIFF_TGZ="$(fetch_npm geotiff 2.1.3 geotiff)"
mkdir -p "$TMP/geotiff"
tar -xzf "$GEOTIFF_TGZ" -C "$TMP/geotiff"
test -s "$TMP/geotiff/package/dist-browser/geotiff.js"

rm -rf "$HERE/geotiff"
mkdir -p "$HERE/geotiff"
cp -a "$TMP/geotiff/package/dist-browser/." "$HERE/geotiff/"

echo "[2/3] DXF Writer"
DXF_TGZ="$(fetch_npm dxf-writer 1.18.4 dxf-writer)"
mkdir -p "$TMP/dxf"
tar -xzf "$DXF_TGZ" -C "$TMP/dxf"
DXF_ENTRY="$TMP/dxf/package/src/Drawing.js"
test -s "$DXF_ENTRY"

cat > "$TMP/webpack.config.cjs" <<'WEBPACK'
const path = require("path");

module.exports = {
  mode: "production",
  target: "web",
  entry: process.env.WEBGIS_DXF_ENTRY,
  devtool: false,
  output: {
    path: process.env.WEBGIS_VENDOR_DIR + "/dxf-writer",
    filename: "dxf-writer.js",
    library: { name: "Drawing", type: "umd" },
    globalObject: "this",
    clean: false
  },
  optimization: { minimize: true }
};
WEBPACK

mkdir -p "$HERE/dxf-writer"
WEBGIS_VENDOR_DIR="$HERE" \
WEBGIS_DXF_ENTRY="$DXF_ENTRY" \
webpack --config "$TMP/webpack.config.cjs"

test -s "$HERE/dxf-writer/dxf-writer.js"

echo "[3/3] Leaflet GoogleMutant"
GM_TGZ="$(fetch_npm leaflet.gridlayer.googlemutant 0.15.0 google-mutant)"
mkdir -p "$TMP/google-mutant"
tar -xzf "$GM_TGZ" -C "$TMP/google-mutant"
GM_SRC="$TMP/google-mutant/package/dist/Leaflet.GoogleMutant.js"
test -s "$GM_SRC"

mkdir -p "$HERE/leaflet-google-mutant"
install -m 0644 "$GM_SRC" \
  "$HERE/leaflet-google-mutant/Leaflet.GoogleMutant.js"

(
  cd "$HERE"
  {
    echo "geotiff 2.1.3 dist-browser"
    sha256sum geotiff/geotiff.js
    echo "dxf-writer 1.18.4 webpack browser bundle (Drawing global)"
    sha256sum dxf-writer/dxf-writer.js
    echo "leaflet.gridlayer.googlemutant 0.15.0"
    sha256sum leaflet-google-mutant/Leaflet.GoogleMutant.js
  }
) > "$HERE/browser-assets-manifest.txt"

echo "Browser assets prepared successfully:"
ls -lh \
  "$HERE/geotiff/geotiff.js" \
  "$HERE/dxf-writer/dxf-writer.js" \
  "$HERE/leaflet-google-mutant/Leaflet.GoogleMutant.js"

echo "Manifest:"
cat "$HERE/browser-assets-manifest.txt"
