#!/usr/bin/env node
import{readFileSync as S,writeFileSync as X,existsSync as i}from"node:fs";import{resolve as y,extname as q}from"node:path";var d=new URL("./wasm",import.meta.url).href;var C=d,v=null;function p(c){if(!v)v=I(c?.wasmBase??C);return v}async function I(c){let e=c.replace(/\/+$/,""),o=await import(`${e}/dwgdxf.js`);if((e.startsWith("file:")||!e.startsWith("http:")&&!e.startsWith("https:")&&typeof window>"u")&&typeof process<"u"){let l=["node","fs"].join(":"),r=["node","path"].join(":"),f=["node","url"].join(":"),s=await import(l),g=await import(r),{fileURLToPath:F}=await import(f),x=e;if(x.startsWith("file:"))x=F(x);let $=g.join(x,"dwgdxf_bg.wasm"),t=s.readFileSync($);await o.default({module_or_path:new WebAssembly.Module(t)})}else await o.default({module_or_path:`${e}/dwgdxf_bg.wasm`});return o}async function h(c,e){let o=await p(e),n=c instanceof Uint8Array?c:new Uint8Array(c);return o.convertDwgToDxf(n)}async function z(){let c=process.argv.slice(2);if(c.includes("--help")||c.includes("-h")||c.length===0)console.log(`
Usage: npx dwgdxf <input-file.dwg> [output-file.dxf]

Options:
  -h, --help     Show this help message
  -v, --version  Show version number
    `),process.exit(0);if(c.includes("--version")||c.includes("-v"))console.log("dwgdxf CLI v1.0.0"),process.exit(0);let e=c[0];if(!e)console.error("Error: Missing input file path."),process.exit(1);let o=y(e);if(!i(o))console.error(`Error: File not found: ${e}`),process.exit(1);let n=c[1];if(!n){let r=q(e);n=e.slice(0,e.length-r.length)+".dxf"}let l=y(n);try{console.log(`Reading: ${e}...`);let r=S(o);console.log("Converting to DXF...");let f=performance.now(),s=await h(r),g=((performance.now()-f)/1000).toFixed(2);console.log(`Writing: ${n}...`),X(l,s),console.log(`✓ Success! Converted in ${g}s.`)}catch(r){console.error("Conversion failed:",r.message||r),process.exit(1)}}z().catch((c)=>{console.error("Unexpected error:",c),process.exit(1)});

//# debugId=EB52C22D6E6251DD64756E2164756E21
