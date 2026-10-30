/* Sabz Royan WebGIS -> RustGIS WebAssembly runtime bridge.
 * The UI stays in the existing ArcMap-like JavaScript application. Vector analysis
 * is delegated to the pinned RustGIS analysis crate compiled to WebAssembly.
 */
(() => {
  "use strict";

  const WASM_MODULE = "./rustgis-wasm-pkg/sabzroyan_rustgis_wasm.js";
  let engine = null;
  let catalogue = [];
  let loading = null;

  const $ = (tag, attrs = {}, text = "") => {
    const node = document.createElement(tag);
    for (const [key, value] of Object.entries(attrs)) {
      if (key === "class") node.className = value;
      else if (key === "style") node.style.cssText = value;
      else if (key.startsWith("on") && typeof value === "function") node.addEventListener(key.slice(2), value);
      else if (value !== undefined && value !== null) node.setAttribute(key, String(value));
    }
    if (text !== "") node.textContent = text;
    return node;
  };

  function bridge() {
    return window.__sabzWebGISSharedBridge || null;
  }

  function availableLayers() {
    const entries = bridge()?.namedLayers || [];
    return entries.filter(item => item?.name && item?.layer && typeof item.layer.toGeoJSON === "function");
  }

  function layerGeoJSON(item) {
    const geo = item.layer.toGeoJSON();
    if (geo?.type === "FeatureCollection") return geo;
    if (geo?.type === "Feature") return {type:"FeatureCollection",features:[geo]};
    throw new Error("Layer '" + item.name + "' did not produce a GeoJSON FeatureCollection.");
  }

  function uniqueName(name) {
    const used = new Set(availableLayers().map(item => String(item.name).toLowerCase()));
    const base = String(name || "RustGIS output").trim() || "RustGIS output";
    if (!used.has(base.toLowerCase())) return base;
    let i = 2;
    while (used.has((base + " (" + i + ")").toLowerCase())) i++;
    return base + " (" + i + ")";
  }

  function setStatus(message) {
    const text = String(message || "");
    bridge()?.status?.(text);
    const node = document.getElementById("rgEngineStatus");
    if (node) node.textContent = text;
  }

  async function loadEngine() {
    if (engine) return engine;
    if (loading) return loading;
    loading = (async () => {
      const module = await import(WASM_MODULE);
      await module.default();
      engine = module;
      const raw = JSON.parse(engine.list_tools());
      catalogue = Array.isArray(raw.tools) ? raw.tools : [];
      window.SabzRoyanRustGIS = {
        status: engine.adapter_status(),
        protocol: engine.adapter_protocol_version(),
        upstream: engine.upstream_revision(),
        listTools: () => catalogue.slice(),
        runTool: (id, layers, params) => JSON.parse(engine.run_tool(id, JSON.stringify(layers), JSON.stringify(params)))
      };
      setStatus("RustGIS آماده است؛ " + catalogue.length + " ابزار پردازشی شناسایی شد.");
      return engine;
    })().catch(error => {
      loading = null;
      engine = null;
      window.SabzRoyanRustGIS = {status:"unavailable", error:String(error?.message || error)};
      throw error;
    });
    return loading;
  }

  const style = `
#rgEngineButton{font-weight:800;letter-spacing:-.4px}
#rgEngineDialog{position:fixed;inset:0;z-index:100000;background:#102a3b88;display:none;align-items:center;justify-content:center;padding:16px}
#rgEngineDialog.open{display:flex}
#rgEngineDialog .rg-panel{width:min(620px,96vw);max-height:88vh;overflow:auto;background:#fff;color:#173b57;border:1px solid #bdcbd3;border-radius:12px;box-shadow:0 18px 60px #102a3b55;padding:18px;direction:rtl}
#rgEngineDialog h2{font-size:18px;margin:0 0 5px}
#rgEngineDialog .rg-muted{font-size:12px;color:#647989;line-height:1.7}
#rgEngineDialog .rg-row{display:grid;grid-template-columns:145px minmax(0,1fr);gap:10px;align-items:center;margin:10px 0}
#rgEngineDialog label{font-size:13px;font-weight:700}
#rgEngineDialog input,#rgEngineDialog select{min-width:0;width:100%;padding:8px;border:1px solid #c5d2d9;border-radius:6px;background:#fff;color:#173b57}
#rgEngineDialog select[multiple]{min-height:100px}
#rgEngineDialog .rg-actions{display:flex;gap:8px;justify-content:flex-start;margin-top:16px}
#rgEngineDialog button{border:1px solid #bdcbd3;background:#f5f8fa;border-radius:7px;padding:8px 13px;cursor:pointer}
#rgEngineDialog button.primary{background:#08734c;color:#fff;border-color:#08734c}
#rgEngineDialog button:disabled{opacity:.5;cursor:not-allowed}
#rgEngineDialog .rg-message{white-space:pre-wrap;overflow-wrap:anywhere;background:#f3f7f8;padding:9px;border-radius:6px;margin-top:10px;font-size:12px;max-height:130px;overflow:auto}
@media(max-width:520px){#rgEngineDialog .rg-row{grid-template-columns:1fr;gap:4px}}
  `;

  function createDialog() {
    if (document.getElementById("rgEngineDialog")) return;
    document.head.appendChild($("style", {id:"rgEngineStyles"}, style));

    const overlay = $("div", {id:"rgEngineDialog", role:"dialog", "aria-modal":"true", "aria-labelledby":"rgEngineTitle"});
    const panel = $("div", {class:"rg-panel"});
    const heading = $("div");
    heading.appendChild($("h2", {id:"rgEngineTitle"}, "RustGIS Geoprocessing Engine"));
    heading.appendChild($("div", {class:"rg-muted"}, "پردازش هندسی با هسته RustGIS و WebAssembly؛ نقشه و لایه‌های فعلی پروژه حفظ می‌شوند."));
    panel.appendChild(heading);

    const toolRow = $("div", {class:"rg-row"});
    toolRow.appendChild($("label", {for:"rgEngineTool"}, "ابزار پردازش"));
    toolRow.appendChild($("select", {id:"rgEngineTool"}));
    panel.appendChild(toolRow);

    const params = $("div", {id:"rgEngineParams"});
    panel.appendChild(params);

    const status = $("div", {id:"rgEngineStatus", class:"rg-muted"}, "در انتظار بارگذاری موتور...");
    panel.appendChild(status);
    const messages = $("div", {id:"rgEngineMessages", class:"rg-message", hidden:"hidden"});
    panel.appendChild(messages);

    const actions = $("div", {class:"rg-actions"});
    const run = $("button", {id:"rgEngineRun", class:"primary", type:"button"}, "اجرای پردازش");
    const close = $("button", {type:"button"}, "بستن");
    actions.append(run, close);
    panel.appendChild(actions);
    overlay.appendChild(panel);
    document.body.appendChild(overlay);

    close.addEventListener("click", () => overlay.classList.remove("open"));
    overlay.addEventListener("click", event => { if (event.target === overlay) overlay.classList.remove("open"); });
    document.getElementById("rgEngineTool").addEventListener("change", renderParams);
    run.addEventListener("click", executeTool);
  }

  function selectedTool() {
    const id = document.getElementById("rgEngineTool")?.value;
    return catalogue.find(tool => tool.id === id) || null;
  }

  function renderParams() {
    const container = document.getElementById("rgEngineParams");
    const tool = selectedTool();
    container.replaceChildren();
    if (!tool) return;
    const layers = availableLayers();
    for (const param of tool.params || []) {
      const row = $("div", {class:"rg-row"});
      const id = "rgp-" + param.name;
      row.appendChild($("label", {for:id}, param.label + (param.required ? " *" : "")));
      const kind = param.kind?.type;
      let control;
      if (["layer","polygon-layer","point-layer","vector-layer","raster-layer"].includes(kind)) {
        control = $("select", {id, "data-param":param.name, "data-kind":kind});
        if (!param.required) control.appendChild($("option", {value:""}, "— انتخاب لایه —"));
        for (const item of layers) control.appendChild($("option", {value:item.name}, item.name));
      } else if (kind === "vector-layers" || kind === "raster-layers") {
        control = $("select", {id, multiple:"multiple", "data-param":param.name, "data-kind":kind});
        for (const item of layers) control.appendChild($("option", {value:item.name}, item.name));
      } else if (kind === "choice") {
        control = $("select", {id, "data-param":param.name, "data-kind":kind});
        for (const choice of (param.kind.choices || [])) control.appendChild($("option", {value:choice}, choice));
      } else if (kind === "boolean") {
        control = $("input", {id, type:"checkbox", "data-param":param.name, "data-kind":kind});
        control.checked = Boolean(param.default);
      } else if (kind === "number") {
        control = $("input", {id, type:"number", step:"any", "data-param":param.name, "data-kind":kind});
        if (param.default !== null && param.default !== "") control.value = param.default;
      } else {
        control = $("input", {id, type:"text", "data-param":param.name, "data-kind":kind});
        if (param.default !== null && param.default !== "") control.value = param.default;
        if (["path","save-file"].includes(kind)) {
          control.disabled = true;
          control.placeholder = "این نوع ورودی فعلاً از رابط WebGIS پشتیبانی نمی‌شود";
        } else if (param.default !== null && param.default !== "") {
          control.value = param.default;
        }
      }
      control.required = Boolean(param.required);
      row.appendChild(control);
      container.appendChild(row);
      if (param.help) container.appendChild($("div", {class:"rg-muted"}, param.help));
    }
  }

  function collectParams(tool) {
    const params = {};
    const layerPayload = {};
    for (const param of tool.params || []) {
      const control = document.querySelector('[data-param="' + CSS.escape(param.name) + '"]');
      if (!control) continue;
      const kind = param.kind?.type;
      let value;
      if (kind === "boolean") value = control.checked;
      else if (kind === "number") {
        if (control.value.trim() === "") {
          if (param.required) throw new Error("پارامتر «" + param.label + "» الزامی است.");
          continue;
        }
        value = Number(control.value);
        if (!Number.isFinite(value)) throw new Error("مقدار عددی «" + param.label + "» معتبر نیست.");
      } else if (kind === "vector-layers" || kind === "raster-layers") {
        value = Array.from(control.selectedOptions).map(option => option.value).filter(Boolean);
        if (param.required && !value.length) throw new Error("برای «" + param.label + "» حداقل یک لایه انتخاب کنید.");
      } else {
        value = control.value;
        if (param.required && !value) throw new Error("پارامتر «" + param.label + "» الزامی است.");
        if (!param.required && value === "") continue;
      }

      if (["layer","polygon-layer","point-layer","vector-layer","raster-layer"].includes(kind)) {
        if (value) {
          const item = availableLayers().find(layer => layer.name === value);
          if (!item) throw new Error("لایه «" + value + "» دیگر در پروژه وجود ندارد.");
          layerPayload[value] = layerGeoJSON(item);
        }
      } else if (kind === "vector-layers" || kind === "raster-layers") {
        for (const name of value) {
          const item = availableLayers().find(layer => layer.name === name);
          if (!item) throw new Error("لایه «" + name + "» دیگر در پروژه وجود ندارد.");
          layerPayload[name] = layerGeoJSON(item);
        }
      }
      if (value !== "" && value !== undefined) params[param.name] = value;
    }
    return {params, layerPayload};
  }

  async function executeTool() {
    const run = document.getElementById("rgEngineRun");
    const messages = document.getElementById("rgEngineMessages");
    const status = document.getElementById("rgEngineStatus");
    const tool = selectedTool();
    if (!tool) return;
    run.disabled = true;
    messages.hidden = true;
    try {
      await loadEngine();
      const {params, layerPayload} = collectParams(tool);
      status.textContent = "در حال اجرای " + tool.name + " با RustGIS...";
      setStatus(status.textContent);
      const result = window.SabzRoyanRustGIS.runTool(tool.id, layerPayload, params);
      const outputMessages = [];
      for (const output of result.outputs || []) {
        if (output.kind === "vector" || output.kind === "update") {
          const name = uniqueName(output.name || (tool.name + " Output"));
          const added = bridge()?.addGeoJSONLayer?.(name, output.geojson);
          if (!added) throw new Error("خروجی محاسبه شد، اما رابط نقشه امکان افزودن لایه را فراهم نکرد.");
          outputMessages.push("لایه خروجی افزوده شد: " + name);
        } else if (output.kind === "selection") {
          outputMessages.push("ابزار " + output.name + " تعداد " + (output.rows || []).length + " عارضه را انتخاب کرد. اعمال انتخاب به جدول در مرحله بعدی اتصال انجام می‌شود.");
        } else if (output.base64) {
          const binary = atob(output.base64);
          const bytes = Uint8Array.from(binary, char => char.charCodeAt(0));
          const blob = new Blob([bytes], {type:"application/octet-stream"});
          const url = URL.createObjectURL(blob);
          const a = $("a", {href:url, download:output.fileName || output.name || "rustgis-output.bin"});
          document.body.appendChild(a); a.click(); a.remove(); URL.revokeObjectURL(url);
          outputMessages.push("فایل خروجی دانلود شد: " + (output.fileName || output.name));
        }
      }
      const allMessages = [...outputMessages, ...(result.messages || []), ...(result.inputMessages || [])];
      messages.textContent = allMessages.join("\n") || "پردازش با موفقیت انجام شد.";
      messages.hidden = false;
      status.textContent = "پردازش " + tool.name + " پایان یافت.";
      setStatus(status.textContent);
    } catch (error) {
      status.textContent = "خطا در اجرای پردازش";
      messages.textContent = String(error?.message || error);
      messages.hidden = false;
      setStatus("خطای RustGIS: " + (error?.message || error));
    } finally {
      run.disabled = false;
    }
  }

  async function openDialog() {
    const overlay = document.getElementById("rgEngineDialog");
    overlay.classList.add("open");
    const select = document.getElementById("rgEngineTool");
    const status = document.getElementById("rgEngineStatus");
    try {
      await loadEngine();
      select.replaceChildren();
      for (const tool of catalogue) {
        select.appendChild($("option", {value:tool.id}, tool.toolbox + " — " + tool.name));
      }
      renderParams();
      status.textContent = "موتور RustGIS آماده است؛ ابزارها از کاتالوگ واقعی RustGIS بارگذاری شده‌اند.";
    } catch (error) {
      status.textContent = "موتور WebAssembly بارگذاری نشد. ابتدا بسته rustgis-wasm را build کنید.";
      document.getElementById("rgEngineMessages").textContent = String(error?.message || error);
      document.getElementById("rgEngineMessages").hidden = false;
    }
  }

  function start() {
    createDialog();
    const toolbox = document.getElementById("toolboxToggleBtn");
    if (toolbox && !document.getElementById("rgEngineButton")) {
      const button = $("button", {type:"button", class:"icon-tool", id:"rgEngineButton", title:"RustGIS Processing Engine", "aria-label":"RustGIS Processing Engine"}, "RG");
      button.addEventListener("click", openDialog);
      toolbox.after(button);
    }
  }

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", start, {once:true});
  else start();
})();
