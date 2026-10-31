/* Sabz Royan WebGIS — raster foundation.
 * Binary-first GeoTIFF handling; never use rendered map tiles as analysis input.
 * GeoTIFF.js API reference: https://github.com/geotiffjs/geotiff.js
 */
(function (root) {
  "use strict";
  const DB_NAME = "sabz-webgis-raster-assets";
  const DB_VERSION = 1;
  const STORE = "assets";

  function openDB() {
    return new Promise((resolve, reject) => {
      const req = indexedDB.open(DB_NAME, DB_VERSION);
      req.onupgradeneeded = () => {
        const db = req.result;
        if (!db.objectStoreNames.contains(STORE)) db.createObjectStore(STORE, { keyPath: "id" });
      };
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error || new Error("Raster asset database could not be opened."));
    });
  }

  function readTag(directory, name) {
    try {
      if (directory && typeof directory.getValue === "function") return directory.getValue(name);
      return directory && directory[name] !== undefined ? directory[name] : null;
    } catch (_) { return null; }
  }

  function getGeoTransform(directory, image) {
    const matrix = readTag(directory, "ModelTransformation");
    if (matrix && matrix.length >= 16) {
      return [Number(matrix[3]), Number(matrix[0]), Number(matrix[1]), Number(matrix[7]), Number(matrix[4]), Number(matrix[5])];
    }
    const scale = readTag(directory, "ModelPixelScale");
    const tie = readTag(directory, "ModelTiepoint");
    if (scale && tie && scale.length >= 2 && tie.length >= 6) {
      const sx = Number(scale[0]), sy = -Math.abs(Number(scale[1]));
      const px = Number(tie[0]), py = Number(tie[1]), gx = Number(tie[3]), gy = Number(tie[4]);
      return [gx - px * sx, sx, 0, gy - py * sy, 0, sy];
    }
    const origin = image.getOrigin(), resolution = image.getResolution();
    if (origin && resolution) return [Number(origin[0]), Number(resolution[0]), 0, Number(origin[1]), 0, -Math.abs(Number(resolution[1]))];
    throw new Error("GeoTIFF has no supported georeferencing transform.");
  }

  function crsFromKeys(keys) {
    const projected = Number(keys && keys.ProjectedCSTypeGeoKey);
    const geographic = Number(keys && keys.GeographicTypeGeoKey);
    const epsg = projected > 0 ? projected : geographic > 0 ? geographic : null;
    return { epsg, name: epsg ? "EPSG:" + epsg : null, modelType: Number(keys && keys.GTModelTypeGeoKey) || null };
  }

  async function read(file) {
    if (!root.GeoTIFF || typeof root.GeoTIFF.fromArrayBuffer !== "function") throw new Error("GeoTIFF reader is unavailable.");
    const bytes = await file.arrayBuffer();
    const tiff = await root.GeoTIFF.fromArrayBuffer(bytes);
    const image = await tiff.getImage();
    const directory = image.fileDirectory || {};
    const keys = typeof image.getGeoKeys === "function" ? image.getGeoKeys() : {};
    const rasters = await image.readRasters({ interleave: false });
    const bands = Array.from(rasters, band => band);
    const width = image.getWidth(), height = image.getHeight();
    if (!width || !height || !bands.length || bands.some(b => b.length !== width * height)) throw new Error("Raster dimensions or band data are invalid.");
    const rawNoData = typeof image.getGDALNoData === "function" ? image.getGDALNoData() : readTag(directory, "GDAL_NODATA");
    const noData = rawNoData === null || rawNoData === undefined || rawNoData === "" ? null : Number(rawNoData);
    const crs = crsFromKeys(keys);
    if (!crs.epsg) throw new Error("GeoTIFF CRS is missing or has no recognized EPSG code; refusing to assume EPSG:4326.");
    const transform = getGeoTransform(directory, image);
    const bounds = image.getBoundingBox();
    if (!bounds || bounds.length !== 4 || !bounds.every(Number.isFinite)) throw new Error("GeoTIFF extent is invalid.");
    const sampleFormat = readTag(directory, "SampleFormat");
    const bitsPerSample = readTag(directory, "BitsPerSample");
    const meta = {
      width, height, bandCount: bands.length,
      dataType: bands[0].constructor.name,
      sampleFormat: sampleFormat == null ? null : Array.from(sampleFormat),
      bitsPerSample: bitsPerSample == null ? null : Array.from(bitsPerSample),
      noData: Number.isFinite(noData) ? noData : null,
      crs, geoKeys: keys,
      geoTransform: transform,
      extent: { xmin: bounds[0], ymin: bounds[1], xmax: bounds[2], ymax: bounds[3] },
      pixelScale: [Math.hypot(transform[1], transform[4]), Math.hypot(transform[2], transform[5])],
      sourceName: file.name || "raster.tif",
      byteLength: bytes.byteLength
    };
    return { bytes, blob: file.slice(0, file.size, "image/tiff"), bands, metadata: meta };
  }

  function buildFloat32GeoTIFF(values, meta) {
    const width = Number(meta.width), height = Number(meta.height);
    if (!Number.isInteger(width) || !Number.isInteger(height) || width < 1 || height < 1 ||
        values.length !== width * height) throw new Error("Invalid Float32 GeoTIFF dimensions.");

    // Write a standards-compliant, little-endian, single-band IEEE Float32 TIFF.
    // geotiff.js 2.1.3's writeArrayBuffer path converts Float32Array through
    // Uint8Array(values), truncating samples instead of serializing their 4 bytes.
    const gt = meta.geoTransform;
    if (!Array.isArray(gt) || gt.length < 6 || !gt.every(Number.isFinite)) {
      throw new Error("A valid six-parameter geotransform is required.");
    }
    const epsg = Number(meta.crs && meta.crs.epsg);
    if (!Number.isInteger(epsg) || epsg <= 0) throw new Error("A valid EPSG code is required.");

    const entries = [];
    const add = (tag, type, data) => entries.push({ tag, type, data });

    add(256, 4, [width]);                         // ImageWidth
    add(257, 4, [height]);                        // ImageLength
    add(258, 3, [32]);                            // BitsPerSample
    add(259, 3, [1]);                             // Compression: none
    add(262, 3, [1]);                             // PhotometricInterpretation: BlackIsZero
    add(277, 3, [1]);                             // SamplesPerPixel
    add(278, 4, [height]);                        // RowsPerStrip
    add(284, 3, [1]);                             // PlanarConfiguration
    add(339, 3, [3]);                             // SampleFormat: IEEE floating point

    const geoKeys = [1, 1, 0, 3, 1024, 0, 1, meta.geoKeys && meta.geoKeys.ProjectedCSTypeGeoKey ? 1 : 2,
      1025, 0, 1, 1, meta.geoKeys && meta.geoKeys.ProjectedCSTypeGeoKey ? 3072 : 2048, 0, 1, epsg];
    add(34735, 3, geoKeys);                       // GeoKeyDirectory

    if (Math.abs(gt[2]) < 1e-12 && Math.abs(gt[4]) < 1e-12) {
      add(33550, 12, [Math.abs(gt[1]), Math.abs(gt[5]), 0]); // ModelPixelScale
      add(33922, 12, [0, 0, 0, gt[0], gt[3], 0]);            // ModelTiepoint
    } else {
      add(34264, 12, [gt[1], gt[2], 0, gt[0], gt[4], gt[5], 0, gt[3], 0, 0, 1, 0, 0, 0, 0, 1]);
    }
    if (meta.noData !== null && meta.noData !== undefined && Number.isFinite(Number(meta.noData))) {
      add(42113, 2, String(meta.noData) + "\0");          // GDAL_NODATA
    }

    const pixelBytes = width * height * 4;
    add(273, 4, [0]);                             // StripOffsets (filled after IFD sizing)
    add(279, 4, [pixelBytes]);                    // StripByteCounts
    entries.sort((a, b) => a.tag - b.tag);
    const typeBytes = { 2: 1, 3: 2, 4: 4, 12: 8 };
    const ifdEnd = 8 + 2 + entries.length * 12 + 4;
    let extraOffset = (ifdEnd + 1) & ~1;
    for (const entry of entries) {
      const size = typeBytes[entry.type] * entry.data.length;
      if (size > 4) {
        entry.offset = extraOffset;
        extraOffset = (extraOffset + size + 1) & ~1;
      }
    }
    const pixelOffset = extraOffset;
    entries.find(entry => entry.tag === 273).data[0] = pixelOffset;

    // StripOffsets is a single LONG and therefore inline; its value was calculated
    // after sizing all external IFD values, so it does not change IFD layout.
    const totalBytes = pixelOffset + pixelBytes;
    const buffer = new ArrayBuffer(totalBytes);
    const view = new DataView(buffer);
    const bytes = new Uint8Array(buffer);
    bytes[0] = 0x49; bytes[1] = 0x49;             // little endian
    view.setUint16(2, 42, true);
    view.setUint32(4, 8, true);
    view.setUint16(8, entries.length, true);

    let cursor = 10;
    for (const entry of entries) {
      view.setUint16(cursor, entry.tag, true);
      view.setUint16(cursor + 2, entry.type, true);
      view.setUint32(cursor + 4, entry.data.length, true);
      const size = typeBytes[entry.type] * entry.data.length;
      const target = size <= 4 ? cursor + 8 : entry.offset;
      if (size > 4) view.setUint32(cursor + 8, target, true);
      if (entry.type === 2) {
        for (let i = 0; i < entry.data.length; i++) bytes[target + i] = entry.data.charCodeAt(i) & 255;
      } else {
        for (let i = 0; i < entry.data.length; i++) {
          const p = target + i * typeBytes[entry.type];
          if (entry.type === 3) view.setUint16(p, entry.data[i], true);
          else if (entry.type === 4) view.setUint32(p, entry.data[i], true);
          else if (entry.type === 12) view.setFloat64(p, entry.data[i], true);
        }
      }
      cursor += 12;
    }
    view.setUint32(cursor, 0, true);               // no next IFD
    for (let i = 0; i < values.length; i++) view.setFloat32(pixelOffset + i * 4, values[i], true);
    return buffer;
  }

  async function writeFloat32(values, metadata) {
    if (!(values instanceof Float32Array) || values.length !== metadata.width * metadata.height) {
      throw new Error("Output pixels do not match the declared raster dimensions.");
    }
    const buffer = buildFloat32GeoTIFF(values, metadata);
    return new Blob([buffer], { type: "image/tiff" });
  }

  async function saveAsset(projectId, layerId, blob, metadata) {
    if (!projectId || !layerId || !(blob instanceof Blob)) throw new Error("Project, layer and raster bytes are required.");
    const db = await openDB();
    return new Promise((resolve, reject) => {
      const tx = db.transaction(STORE, "readwrite");
      const record = { id: String(projectId) + "::" + String(layerId), projectId: String(projectId), layerId: String(layerId), blob, metadata, updatedAt: new Date().toISOString() };
      tx.objectStore(STORE).put(record);
      tx.oncomplete = () => { db.close(); resolve(record.id); };
      tx.onerror = tx.onabort = () => { const e = tx.error || new Error("Raster bytes could not be saved."); db.close(); reject(e); };
    });
  }

  async function loadAsset(projectId, layerId) {
    const db = await openDB();
    return new Promise((resolve, reject) => {
      const tx = db.transaction(STORE, "readonly");
      const req = tx.objectStore(STORE).get(String(projectId) + "::" + String(layerId));
      req.onsuccess = () => { const value = req.result || null; db.close(); resolve(value); };
      req.onerror = () => { db.close(); reject(req.error || new Error("Raster asset could not be loaded.")); };
    });
  }

  async function removeProjectAssets(projectId) {
    const db = await openDB();
    return new Promise((resolve, reject) => {
      const tx = db.transaction(STORE, "readwrite"), store = tx.objectStore(STORE), req = store.getAll();
      req.onsuccess = () => (req.result || []).filter(x => x.projectId === String(projectId)).forEach(x => store.delete(x.id));
      tx.oncomplete = () => { db.close(); resolve(true); };
      tx.onerror = tx.onabort = () => { db.close(); reject(tx.error || new Error("Raster assets could not be removed.")); };
    });
  }

  async function verify(blob, expected) {
    const tiff = await root.GeoTIFF.fromBlob(blob), image = await tiff.getImage();
    const keys = typeof image.getGeoKeys === "function" ? image.getGeoKeys() : {};
    const actualBounds = image.getBoundingBox();
    const actualBands = await image.readRasters({ interleave: false });
    const first = actualBands[0] && actualBands[0][0];
    const checks = {
      dimensions: image.getWidth() === expected.width && image.getHeight() === expected.height,
      bands: actualBands.length === expected.bandCount,
      crs: !expected.crs.epsg || Number(keys.ProjectedCSTypeGeoKey || keys.GeographicTypeGeoKey) === expected.crs.epsg,
      extent: actualBounds && actualBounds.every((v, i) => Math.abs(v - [expected.extent.xmin, expected.extent.ymin, expected.extent.xmax, expected.extent.ymax][i]) <= Math.max(1e-8, Math.abs(v) * 1e-9)),
      firstPixelFinite: Number.isFinite(Number(first))
    };
    return { valid: Object.values(checks).every(Boolean), checks };
  }

  root.SabzRasterRuntime = Object.freeze({ read, writeFloat32, saveAsset, loadAsset, removeProjectAssets, verify });
})(window);
