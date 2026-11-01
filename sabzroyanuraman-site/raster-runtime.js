/* Sabz Royan WebGIS — robust GeoTIFF runtime.
 * Reads plain TIFFs as preview-only and GeoTIFFs with validated CRS/affine metadata.
 */
(function (root) {
  "use strict";
  const DB_NAME = "sabz-webgis-raster-assets", DB_VERSION = 1, STORE = "assets";
  const TAGS = {
    ModelPixelScale: 33550, ModelTiepoint: 33922, ModelTransformation: 34264,
    GeoKeyDirectory: 34735, GeoDoubleParams: 34736, GeoAsciiParams: 34737,
    GDAL_NODATA: 42113, SampleFormat: 339, BitsPerSample: 258
  };

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
    if (!directory) return null;
    try {
      const direct = directory[name];
      if (direct !== undefined && direct !== null) return direct;
      const id = TAGS[name];
      if (id && typeof directory.getValue === "function") {
        const value = directory.getValue(id);
        return value === undefined ? null : value;
      }
    } catch (_) {}
    return null;
  }

  function decodeGeoKeys(directory) {
    const raw = readTag(directory, "GeoKeyDirectory");
    if (!raw || raw.length < 4) return {};
    const doubles = readTag(directory, "GeoDoubleParams") || [];
    const ascii = readTag(directory, "GeoAsciiParams") || "";
    const keys = {};
    const names = {
      1024: "GTModelTypeGeoKey", 1025: "GTRasterTypeGeoKey",
      2048: "GeographicTypeGeoKey", 2054: "GeogAngularUnitsGeoKey",
      3072: "ProjectedCSTypeGeoKey", 3076: "ProjLinearUnitsGeoKey"
    };
    const count = Math.min(Number(raw[3]) || 0, Math.floor((raw.length - 4) / 4));
    for (let i = 0; i < count; i++) {
      const p = 4 + i * 4, keyId = Number(raw[p]), location = Number(raw[p + 1]);
      const n = Number(raw[p + 2]), offset = Number(raw[p + 3]);
      if (!names[keyId] || !n) continue;
      let value = null;
      if (location === 0) value = offset;
      else if (location === 34735) value = n === 1 ? raw[offset] : Array.from(raw).slice(offset, offset + n);
      else if (location === 34736) value = n === 1 ? doubles[offset] : Array.from(doubles).slice(offset, offset + n);
      else if (location === 34737) value = String(ascii).slice(offset, offset + n).replace(/[|\\0]+$/g, "");
      if (value !== null && value !== undefined) keys[names[keyId]] = value;
    }
    return keys;
  }

  function crsFromKeys(keys) {
    keys = keys && typeof keys === "object" ? keys : {};
    const projected = Number(keys.ProjectedCSTypeGeoKey);
    const geographic = Number(keys.GeographicTypeGeoKey);
    const valid = value => Number.isInteger(value) && value > 0 && value < 32767;
    const epsg = valid(projected) ? projected : valid(geographic) ? geographic : null;
    return { epsg, name: epsg ? "EPSG:" + epsg : null, modelType: Number(keys.GTModelTypeGeoKey) || null };
  }

  function getGeoTransform(directory) {
    const matrix = readTag(directory, "ModelTransformation");
    if (matrix && matrix.length >= 16) {
      const t = [Number(matrix[3]), Number(matrix[0]), Number(matrix[1]), Number(matrix[7]), Number(matrix[4]), Number(matrix[5])];
      return t.every(Number.isFinite) ? t : null;
    }
    const scale = readTag(directory, "ModelPixelScale"), tie = readTag(directory, "ModelTiepoint");
    if (scale && tie && scale.length >= 2 && tie.length >= 6) {
      const sx = Number(scale[0]), sy = -Math.abs(Number(scale[1]));
      const px = Number(tie[0]), py = Number(tie[1]), gx = Number(tie[3]), gy = Number(tie[4]);
      const t = [gx - px * sx, sx, 0, gy - py * sy, 0, sy];
      return t.every(Number.isFinite) ? t : null;
    }
    return null;
  }

  function extentFromTransform(t, width, height) {
    if (!t) return null;
    const [x0, a, b, y0, d, e] = t;
    const points = [[0,0],[width,0],[0,height],[width,height]].map(([x,y]) => [x0+a*x+b*y, y0+d*x+e*y]);
    const xs = points.map(p=>p[0]), ys = points.map(p=>p[1]);
    const result = { xmin:Math.min(...xs), ymin:Math.min(...ys), xmax:Math.max(...xs), ymax:Math.max(...ys) };
    return Object.values(result).every(Number.isFinite) ? result : null;
  }

  async function read(file) {
    if (!root.GeoTIFF || typeof root.GeoTIFF.fromArrayBuffer !== "function") throw new Error("GeoTIFF reader is unavailable.");
    if (!file || typeof file.arrayBuffer !== "function") throw new Error("A readable TIFF file or Blob is required.");
    const bytes = await file.arrayBuffer();
    const tiff = await root.GeoTIFF.fromArrayBuffer(bytes);
    const image = await tiff.getImage();
    const directory = image.fileDirectory || {};
    // Decode the raw GeoKeyDirectory ourselves. Some TIFFs have no GeoKeys at all;
    // calling image.getGeoKeys() on those files can dereference null internally.
    const keys = decodeGeoKeys(directory);
    const rasters = await image.readRasters({ interleave: false });
    const bands = Array.from(rasters || []);
    const width = Number(image.getWidth()), height = Number(image.getHeight());
    if (!Number.isInteger(width) || !Number.isInteger(height) || width < 1 || height < 1 ||
        !bands.length || bands.some(b => !b || b.length !== width * height)) {
      throw new Error("Raster dimensions or band data are invalid.");
    }
    let rawNoData = null;
    try { rawNoData = typeof image.getGDALNoData === "function" ? image.getGDALNoData() : null; } catch (_) {}
    if (rawNoData === null || rawNoData === undefined || rawNoData === "") rawNoData = readTag(directory, "GDAL_NODATA");
    const noData = rawNoData === null || rawNoData === undefined || rawNoData === "" ? null : Number(rawNoData);
    const crs = crsFromKeys(keys), transform = getGeoTransform(directory);
    const extent = extentFromTransform(transform, width, height);
    const georeferenced = !!transform && !!extent && !!crs.epsg;
    const sampleFormat = readTag(directory, "SampleFormat"), bitsPerSample = readTag(directory, "BitsPerSample");
    const meta = {
      width, height, bandCount: bands.length, dataType: bands[0].constructor.name,
      sampleFormat: sampleFormat == null ? null : Array.from(sampleFormat),
      bitsPerSample: bitsPerSample == null ? null : Array.from(bitsPerSample),
      noData: Number.isFinite(noData) ? noData : null, crs, geoKeys: keys,
      georeferenced, previewOnly: !georeferenced, geoTransform: transform, extent,
      pixelScale: transform ? [Math.hypot(transform[1], transform[4]), Math.hypot(transform[2], transform[5])] : [1,1],
      sourceName: file.name || "raster.tif", byteLength: bytes.byteLength
    };
    return { bytes, blob: file.slice(0, file.size, "image/tiff"), bands, metadata: meta };
  }

  function buildFloat32GeoTIFF(values, meta) {
    const width = Number(meta.width), height = Number(meta.height);
    if (!Number.isInteger(width) || !Number.isInteger(height) || width < 1 || height < 1 || values.length !== width * height) {
      throw new Error("Invalid Float32 GeoTIFF dimensions.");
    }
    const gt = meta.geoTransform;
    if (!Array.isArray(gt) || gt.length < 6 || !gt.every(Number.isFinite)) throw new Error("A valid six-parameter geotransform is required.");
    const epsg = Number(meta.crs && meta.crs.epsg);
    if (!Number.isInteger(epsg) || epsg <= 0 || epsg >= 32767) throw new Error("A valid EPSG code is required.");
    const entries = [], add = (tag,type,data) => entries.push({tag,type,data});
    add(256,4,[width]); add(257,4,[height]); add(258,3,[32]); add(259,3,[1]); add(262,3,[1]);
    add(277,3,[1]); add(278,4,[height]); add(284,3,[1]); add(339,3,[3]);
    const projected = !!(meta.geoKeys && meta.geoKeys.ProjectedCSTypeGeoKey);
    add(34735,3,[1,1,0,3,1024,0,1,projected?1:2,1025,0,1,1,projected?3072:2048,0,1,epsg]);
    if (Math.abs(gt[2]) < 1e-12 && Math.abs(gt[4]) < 1e-12) {
      add(33550,12,[Math.abs(gt[1]),Math.abs(gt[5]),0]); add(33922,12,[0,0,0,gt[0],gt[3],0]);
    } else add(34264,12,[gt[1],gt[2],0,gt[0],gt[4],gt[5],0,gt[3],0,0,1,0,0,0,0,1]);
    if (meta.noData !== null && meta.noData !== undefined && Number.isFinite(Number(meta.noData))) add(42113,2,String(meta.noData)+"\\0");
    const pixelBytes=width*height*4; add(273,4,[0]); add(279,4,[pixelBytes]); entries.sort((a,b)=>a.tag-b.tag);
    const typeBytes={2:1,3:2,4:4,12:8}, ifdEnd=8+2+entries.length*12+4; let extraOffset=(ifdEnd+1)&~1;
    for(const entry of entries){const size=typeBytes[entry.type]*entry.data.length;if(size>4){entry.offset=extraOffset;extraOffset=(extraOffset+size+1)&~1;}}
    const pixelOffset=extraOffset; entries.find(e=>e.tag===273).data[0]=pixelOffset;
    const buffer=new ArrayBuffer(pixelOffset+pixelBytes),view=new DataView(buffer),bytes=new Uint8Array(buffer);
    bytes[0]=0x49;bytes[1]=0x49;view.setUint16(2,42,true);view.setUint32(4,8,true);view.setUint16(8,entries.length,true);
    let cursor=10;
    for(const entry of entries){
      view.setUint16(cursor,entry.tag,true);view.setUint16(cursor+2,entry.type,true);view.setUint32(cursor+4,entry.data.length,true);
      const size=typeBytes[entry.type]*entry.data.length,target=size<=4?cursor+8:entry.offset;
      if(size>4)view.setUint32(cursor+8,target,true);
      if(entry.type===2){for(let i=0;i<entry.data.length;i++)bytes[target+i]=entry.data.charCodeAt(i)&255;}
      else for(let i=0;i<entry.data.length;i++){const p=target+i*typeBytes[entry.type];if(entry.type===3)view.setUint16(p,entry.data[i],true);else if(entry.type===4)view.setUint32(p,entry.data[i],true);else if(entry.type===12)view.setFloat64(p,entry.data[i],true);}
      cursor+=12;
    }
    view.setUint32(cursor,0,true);
    for(let i=0;i<values.length;i++)view.setFloat32(pixelOffset+i*4,values[i],true);
    return buffer;
  }

  async function writeFloat32(values, metadata) {
    if (!(values instanceof Float32Array) || values.length !== metadata.width * metadata.height) throw new Error("Output pixels do not match the declared raster dimensions.");
    return new Blob([buildFloat32GeoTIFF(values,metadata)],{type:"image/tiff"});
  }

  async function verify(blob, expected) {
    const parsed = await read(blob), m = parsed.metadata;
    const actual = parsed.bands[0] && Number(parsed.bands[0][0]);
    const close = (a,b) => Number.isFinite(Number(a)) && Number.isFinite(Number(b)) &&
      Math.abs(Number(a)-Number(b)) <= Math.max(1e-8,Math.abs(Number(b))*1e-9);
    const checks = {
      dimensions: m.width === Number(expected.width) && m.height === Number(expected.height),
      bands: m.bandCount === Number(expected.bandCount || 1),
      crs: !expected.crs?.epsg || m.crs.epsg === Number(expected.crs.epsg),
      extent: !!m.extent && !!expected.extent &&
        close(m.extent.xmin,expected.extent.xmin) && close(m.extent.ymin,expected.extent.ymin) &&
        close(m.extent.xmax,expected.extent.xmax) && close(m.extent.ymax,expected.extent.ymax),
      firstPixel: Number.isFinite(actual) && (!Number.isFinite(Number(expected.firstPixel)) || close(actual,expected.firstPixel))
    };
    return { valid:Object.values(checks).every(Boolean), checks, metadata:m, firstPixel:actual };
  }

  async function selfTest() {
    // A tiny baseline TIFF with no GeoKeyDirectory must load as preview-only,
    // never throw while trying to dereference a missing ProjectedCSTypeGeoKey.
    const tags = [
      [256,4,2],[257,4,2],[258,3,8],[259,3,1],[262,3,1],
      [273,4,0],[277,3,1],[278,4,2],[279,4,4],[284,3,1]
    ].sort((a,b)=>a[0]-b[0]);
    const plainOffset = 8 + 2 + tags.length * 12 + 4;
    const plainBuffer = new ArrayBuffer(plainOffset + 4), plainView = new DataView(plainBuffer);
    const plainBytes = new Uint8Array(plainBuffer);
    plainBytes[0]=0x49;plainBytes[1]=0x49;plainView.setUint16(2,42,true);plainView.setUint32(4,8,true);
    plainView.setUint16(8,tags.length,true);
    let p=10;
    for(const tag of tags){
      plainView.setUint16(p,tag[0],true);plainView.setUint16(p+2,tag[1],true);plainView.setUint32(p+4,1,true);
      plainView.setUint32(p+8,tag[0]===273?plainOffset:tag[2],true);p+=12;
    }
    plainView.setUint32(p,0,true);plainBytes.set([10,80,160,240],plainOffset);
    const plain = await read(new Blob([plainBuffer],{type:"image/tiff"}));
    const plainOk = plain.metadata.width===2 && plain.metadata.height===2 &&
      plain.metadata.previewOnly===true && plain.metadata.crs.epsg===null &&
      Number(plain.bands[0][3])===240;
    if(!plainOk)throw new Error("Plain TIFF preview self-test failed.");

    const values = new Float32Array([1.25,2.5,-3.75,4.125]);
    const metadata = {
      width:2,height:2,bandCount:1,crs:{epsg:32638},
      geoKeys:{ProjectedCSTypeGeoKey:32638},
      geoTransform:[500000,10,0,4000000,0,-10],
      extent:{xmin:500000,ymin:3999980,xmax:500020,ymax:4000000},noData:-9999
    };
    const blob = await writeFloat32(values,metadata);
    const result = await verify(blob,{...metadata,firstPixel:1.25});
    if (!result.valid) throw new Error("GeoTIFF self-test failed: "+JSON.stringify(result.checks));
    return {ok:true,plainTiff:{ok:plainOk,width:plain.metadata.width,height:plain.metadata.height,previewOnly:plain.metadata.previewOnly},
      geoTiff:{bytes:blob.size,checks:result.checks,crs:result.metadata.crs,extent:result.metadata.extent}};
  }

  async function saveAsset(projectId, layerId, blob, metadata) {
    if (!projectId || !layerId || !(blob instanceof Blob)) throw new Error("Project, layer and raster bytes are required.");
    const db=await openDB();
    return new Promise((resolve,reject)=>{
      const tx=db.transaction(STORE,"readwrite"),record={id:String(projectId)+"::"+String(layerId),projectId:String(projectId),layerId:String(layerId),blob,metadata,updatedAt:new Date().toISOString()};
      tx.objectStore(STORE).put(record);tx.oncomplete=()=>{db.close();resolve(record.id);};
      tx.onerror=tx.onabort=()=>{const e=tx.error||new Error("Raster bytes could not be saved.");db.close();reject(e);};
    });
  }
  async function loadAsset(projectId,layerId) {
    const db=await openDB();
    return new Promise((resolve,reject)=>{
      const tx=db.transaction(STORE,"readonly"),req=tx.objectStore(STORE).get(String(projectId)+"::"+String(layerId));
      req.onsuccess=()=>{const value=req.result||null;db.close();resolve(value);};
      req.onerror=()=>{db.close();reject(req.error||new Error("Raster asset could not be loaded."));};
    });
  }
  async function removeProjectAssets(projectId) {
    const db=await openDB();
    return new Promise((resolve,reject)=>{
      const tx=db.transaction(STORE,"readwrite"),store=tx.objectStore(STORE),req=store.getAll();
      req.onsuccess=()=>(req.result||[]).filter(x=>x.projectId===String(projectId)).forEach(x=>store.delete(x.id));
      tx.oncomplete=()=>{db.close();resolve(true);};
      tx.onerror=tx.onabort=()=>{db.close();reject(tx.error||new Error("Raster assets could not be removed."));};
    });
  }
  root.SabzRasterRuntime=Object.freeze({read,writeFloat32,saveAsset,loadAsset,removeProjectAssets,verify,selfTest});
})(window);
