/* RustGIS .rgplugin manager for Sabz Royan WebGIS.
 * Packages are parsed/validated by RustGIS WASM; execution stays inside RustGIS' wasmi sandbox.
 * No arbitrary JavaScript plugins, process plugins, direct network or filesystem APIs.
 */
(function(root){
 "use strict";
 const DB="sabz-webgis-plugins", STORE="packages";
 function openDb(){return new Promise((resolve,reject)=>{const r=indexedDB.open(DB,1);r.onupgradeneeded=()=>{if(!r.result.objectStoreNames.contains(STORE))r.result.createObjectStore(STORE,{keyPath:"id"});};r.onsuccess=()=>resolve(r.result);r.onerror=()=>reject(r.error);});}
 async function all(){const db=await openDb();return new Promise((resolve,reject)=>{const tx=db.transaction(STORE,"readonly"),r=tx.objectStore(STORE).getAll();r.onsuccess=()=>{db.close();resolve(r.result||[]);};r.onerror=()=>{db.close();reject(r.error);};});}
 async function put(item){const db=await openDb();return new Promise((resolve,reject)=>{const tx=db.transaction(STORE,"readwrite");tx.objectStore(STORE).put(item);tx.oncomplete=()=>{db.close();resolve();};tx.onerror=()=>{db.close();reject(tx.error);};});}
 async function remove(id){const db=await openDb();return new Promise((resolve,reject)=>{const tx=db.transaction(STORE,"readwrite");tx.objectStore(STORE).delete(id);tx.oncomplete=()=>{db.close();resolve();};tx.onerror=()=>{db.close();reject(tx.error);};});}
 const labels={edit:"Change the current GIS project and its layers",files:"Read or write user-selected files",network:"Access network addresses chosen by the plugin"};
 function attach(panel){
  if(panel.querySelector("[data-r-plugin-manager]"))return;
  const section=document.createElement("section");section.dataset.rPluginManager="1";
  section.style.cssText="margin-top:12px;border-top:1px solid #cbd5dc;padding-top:10px";
  section.innerHTML='<strong>RustGIS Plugin Manager</strong><p style="font-size:10px;color:#586d79">Install signed-off .rgplugin packages. WebAssembly only; no process or JavaScript plugins.</p><input data-r-plugin-file type="file" accept=".rgplugin,.zip" style="width:100%;margin:5px 0"><button type="button" data-r-plugin-install>Inspect &amp; Install</button><div data-r-plugin-status style="font-size:10px;white-space:pre-wrap;margin:7px 0">No plugin package selected.</div><div data-r-plugin-list></div><label style="display:block;font-size:10px;margin-top:8px">Plugin command<select data-r-plugin-command style="width:100%"><option value="">No command selected</option></select></label><textarea data-r-plugin-params rows="3" style="width:100%;margin-top:5px" placeholder="Command parameters as JSON, e.g. {}"></textarea><button type="button" data-r-plugin-run>Run sandboxed command</button><pre data-r-plugin-result style="white-space:pre-wrap;background:#e8eef2;padding:8px;font-size:10px">Plugin command output appears here.</pre>';
  const gp=panel.querySelector("[data-r-gp-status]")?.parentElement;
  (gp||panel).insertAdjacentElement("afterend",section);
  const status=section.querySelector("[data-r-plugin-status]"),list=section.querySelector("[data-r-plugin-list]"),commands=section.querySelector("[data-r-plugin-command]");
  const bridge=()=>{if(!root.SabzRustRasterBridge)throw new Error("RustGIS WASM bridge is unavailable.");return root.SabzRustRasterBridge;};
  async function refresh(){
   const installed=await bridge().listPlugins();list.replaceChildren();commands.replaceChildren();
   const none=document.createElement("option");none.value="";none.textContent="Select command";commands.appendChild(none);
   installed.forEach(plugin=>{
    const row=document.createElement("div");row.style.cssText="border:1px solid #cbd5dc;padding:6px;margin:4px 0;font-size:10px";
    const title=document.createElement("strong");title.textContent=plugin.name+" "+plugin.version;row.appendChild(title);
    const info=document.createElement("div");info.textContent=plugin.id+" · "+(plugin.enabled?"Enabled":"Disabled")+" · SHA-256 "+plugin.sha256;row.appendChild(info);
    const toggle=document.createElement("button");toggle.type="button";toggle.textContent=plugin.enabled?"Disable":"Enable";toggle.onclick=async()=>{try{await bridge().setPluginEnabled(plugin.id,!plugin.enabled);const records=await all(),saved=records.find(r=>r.id===plugin.id);if(saved){saved.enabled=!plugin.enabled;await put(saved);}await refresh();}catch(e){status.textContent=e.message;}};row.appendChild(toggle);
    const uninstall=document.createElement("button");uninstall.type="button";uninstall.textContent="Uninstall";uninstall.style.marginLeft="5px";uninstall.onclick=async()=>{if(!confirm("Uninstall "+plugin.name+"?"))return;try{await bridge().uninstallPlugin(plugin.id);await remove(plugin.id);await refresh();}catch(e){status.textContent=e.message;}};row.appendChild(uninstall);
    list.appendChild(row);
    (plugin.commands||[]).forEach(cmd=>{const o=document.createElement("option");o.value=cmd.id;o.textContent=plugin.name+" — "+cmd.label;commands.appendChild(o);});
   });
   if(!installed.length)list.textContent="No plugins installed.";
  }
  async function restoreApproved(){
   const records=await all(),loaded=await bridge().listPlugins(),have=new Set(loaded.map(p=>p.id));
   for(const record of records){
    if(have.has(record.id))continue;
    try{
     const info=const info=await bridge().installPluginPackage(new Uint8Array(record.bytes),record.approvedPermissions||[]);
     if(info.sha256!==record.sha256||info.id!==record.id)throw new Error("Saved plugin package hash or identity changed; reinstallation blocked.");
     if(record.enabled===false)await bridge().setPluginEnabled(record.id,false);
    }catch(e){console.warn("Saved plugin not restored:",record.id,e);}
   }
  }
  section.querySelector("[data-r-plugin-install]").onclick=async()=>{
   const file=section.querySelector("[data-r-plugin-file]").files[0];if(!file){status.textContent="Choose a .rgplugin package first.";return;}
   try{
    status.textContent="Validating package and manifest with RustGIS…";
    const bytes=new Uint8Array(await file.arrayBuffer()),info=await bridge().inspectPluginPackage(bytes);
    const permissions=info.permissions||[];
    const detail=permissions.length?permissions.map(p=>"• "+p+": "+(labels[p]||p)).join("\n"):"No elevated permissions declared.";
    const prompt="Install "+info.name+" "+info.version+"?\nAuthor: "+(info.author||"not specified")+"\nLicense: "+(info.license||"not specified")+"\nSHA-256: "+info.sha256+"\n\nDeclared permissions:\n"+detail+"\n\nOnly WASM is accepted. Commands requiring edit/files/network host capabilities remain blocked in this WebGIS build.";
    if(!confirm(prompt)){status.textContent="Installation cancelled; no permission was granted.";return;}
    const installed=await bridge().installPluginPackage(bytes,permissions);
    await put({id:installed.id,bytes:bytes,sha256:installed.sha256,approvedPermissions:permissions,installedAt:new Date().toISOString()});
    status.textContent="Installed and validated: "+installed.name+" · "+installed.sha256;
    await refresh();
   }catch(e){status.textContent="Plugin installation failed: "+(e.message||e);}
  };
  section.querySelector("[data-r-plugin-run]").onclick=async()=>{
   const id=commands.value;if(!id){section.querySelector("[data-r-plugin-result]").textContent="Select a command first.";return;}
   try{
    const raw=section.querySelector("[data-r-plugin-params]").value.trim()||"{}";
    const params=JSON.parse(raw);
    section.querySelector("[data-r-plugin-result]").textContent="Running in RustGIS WASM sandbox…";
    const result=await bridge().runPluginCommand(id,params);
    section.querySelector("[data-r-plugin-result]").textContent=JSON.stringify(result,null,2);
   }catch(e){section.querySelector("[data-r-plugin-result]").textContent="Plugin command blocked or failed: "+(e.message||e);}
  };
  (async()=>{try{await bridge().load();await restoreApproved();await refresh();}catch(e){status.textContent="Plugin manager unavailable until the RustGIS WASM build is deployed: "+e.message;}})();
 }
 root.SabzPluginManager=Object.freeze({attach});
})(window);
