// ../../_temp/web-gis-npm/node_modules/dwgdxf/dist/index.js
var V = new URL("./wasm", import.meta.url).href;
var k = "https://cdn.jsdelivr.net/npm/dwgdxf@2.0.1/dist/wasm";
var x = V;
var I = null;
function J(q) {
  if (!I) I = C(q?.wasmBase ?? x);
  return I;
}
async function C(q) {
  let z = q.replace(/\/+$/, ""), G = await import(`${z}/dwgdxf.js`);
  if ((z.startsWith("file:") || !z.startsWith("http:") && !z.startsWith("https:") && typeof window > "u") && typeof process < "u") {
    let Q = ["node", "fs"].join(":"), X = ["node", "path"].join(":"), Y = ["node", "url"].join(":"), Z = await import(Q), $ = await import(X), { fileURLToPath: y } = await import(Y), H = z;
    if (H.startsWith("file:")) H = y(H);
    let F = $.join(H, "dwgdxf_bg.wasm"), j = Z.readFileSync(F);
    await G.default({ module_or_path: new WebAssembly.Module(j) });
  } else await G.default({ module_or_path: `${z}/dwgdxf_bg.wasm` });
  return G;
}
async function E(q, z) {
  let G = await J(z), K = q instanceof Uint8Array ? q : new Uint8Array(q);
  return G.convertDwgToDxf(K);
}
async function N(q) {
  await J(q);
}
export {
  k as CDN_WASM_BASE,
  V as LOCAL_WASM_BASE,
  E as convertDwgToDxf,
  N as init
};
