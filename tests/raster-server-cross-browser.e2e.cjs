const { chromium } = require("playwright");
const crypto = require("node:crypto");

const required = [
  "SABZ_GIS_E2E_BASE_URL",
  "SABZ_GIS_E2E_USERNAME",
  "SABZ_GIS_E2E_PASSWORD",
  "SABZ_GIS_E2E_ORGANIZATION_ID",
  "SABZ_GIS_E2E_PROJECT_ID"
];
for (const key of required) {
  if (!process.env[key]) throw new Error("Missing required environment variable: " + key);
}
const baseUrl = new URL(process.env.SABZ_GIS_E2E_BASE_URL);
if (baseUrl.protocol !== "https:") throw new Error("The raster cross-browser E2E test requires HTTPS.");
const organizationId = process.env.SABZ_GIS_E2E_ORGANIZATION_ID;
const projectId = process.env.SABZ_GIS_E2E_PROJECT_ID;
const username = process.env.SABZ_GIS_E2E_USERNAME;
const password = process.env.SABZ_GIS_E2E_PASSWORD;
const assetId = "e2e-raster-" + crypto.randomUUID().replace(/-/g, "").slice(0, 24);
const fileName = assetId + ".tif";
const metadata = {
  width: 4, height: 3, bandCount: 1, dataType: "Float32Array", noData: -9999,
  crs: { epsg: 32638, name: "EPSG:32638" },
  geoKeys: { ProjectedCSTypeGeoKey: 32638 },
  geoTransform: [500000, 10, 0, 3800000, 0, -10],
  extent: { xmin: 500000, ymin: 3799970, xmax: 500040, ymax: 3800000 },
  pixelScale: [10, 10]
};
const pixels = [11, 12, 13, 14, 21, 22, 23, 24, 31, 32, 33, -9999];

async function openRuntimePage(context) {
  const page = await context.newPage();
  page.on("pageerror", error => process.stderr.write("PAGE ERROR: " + error.message + "\n"));
  await page.goto(new URL("/raster-runtime-tests.html", baseUrl).href, {
    waitUntil: "networkidle", timeout: 90000
  });
  await page.waitForFunction(() => !!window.SabzRasterRuntime, null, { timeout: 30000 });
  return page;
}

async function login(page) {
  return page.evaluate(async ({ username, password }) => {
    const csrfResponse = await fetch("/api/gis/auth.php?action=csrf", {
      credentials: "same-origin", cache: "no-store"
    });
    const csrfBody = await csrfResponse.json();
    if (!csrfResponse.ok || !csrfBody.csrf_token) throw new Error("csrf_unavailable");
    const response = await fetch("/api/gis/auth.php?action=login", {
      method: "POST", credentials: "same-origin", cache: "no-store",
      headers: { "Content-Type": "application/json", "X-CSRF-Token": csrfBody.csrf_token, Accept: "application/json" },
      body: JSON.stringify({ username, password })
    });
    const body = await response.json().catch(() => ({}));
    if (!response.ok || body.ok !== true) throw new Error(body.error || "gis_login_failed");
    const meResponse = await fetch("/api/gis/auth.php?action=me", {
      credentials: "same-origin", cache: "no-store"
    });
    const me = await meResponse.json();
    if (!meResponse.ok || me.ok !== true) throw new Error(me.error || "gis_session_check_failed");
    return { csrfToken: csrfBody.csrf_token, userId: String(me.user.id) };
  }, { username, password });
}

async function listAssets(page) {
  return page.evaluate(async ({ organizationId, projectId }) => {
    const url = "/api/gis/raster-assets.php?organization_id=" + encodeURIComponent(organizationId) +
      "&project_id=" + encodeURIComponent(projectId) + "&action=list";
    const response = await fetch(url, { credentials: "same-origin", cache: "no-store" });
    const body = await response.json().catch(() => ({}));
    if (!response.ok || body.ok !== true || !Array.isArray(body.assets))
      throw new Error(body.error || "raster_asset_list_failed");
    return body.assets;
  }, { organizationId, projectId });
}

(async () => {
  const browser = await chromium.launch({ headless: true, args: ["--no-sandbox"] });
  const contextA = await browser.newContext();
  const contextB = await browser.newContext();
  let pageA, pageB, csrfA, csrfB, uploaded = false;
  try {
    pageA = await openRuntimePage(contextA);
    const sessionA = await login(pageA);
    csrfA = await pageA.evaluate(async () => {
      const response = await fetch("/api/gis/auth.php?action=csrf", { credentials: "same-origin", cache: "no-store" });
      const body = await response.json();
      if (!response.ok || !body.csrf_token) throw new Error(body.error || "csrf_unavailable_after_login");
      return body.csrf_token;
    });
    // Upload using the authenticated first browser context. The test project must be dedicated to E2E.
    const uploadResult = await pageA.evaluate(async ({ organizationId, projectId, assetId, fileName, metadata, csrfToken }) => {
      const blob = await SabzRasterRuntime.writeFloat32(new Float32Array([11,12,13,14,21,22,23,24,31,32,33,-9999]), metadata);
      const form = new FormData();
      form.append("action", "upload");
      form.append("organization_id", organizationId);
      form.append("project_id", projectId);
      form.append("asset_id", assetId);
      form.append("file_name", fileName);
      form.append("metadata", JSON.stringify(metadata));
      form.append("file", blob, fileName);
      const response = await fetch("/api/gis/raster-assets.php", {
        method: "POST", credentials: "same-origin", cache: "no-store",
        headers: { "X-CSRF-Token": csrfToken, Accept: "application/json" }, body: form
      });
      const body = await response.json().catch(() => ({}));
      if (!response.ok || body.ok !== true) throw new Error(body.error || ("raster_upload_http_" + response.status));
      return { size: blob.size, asset: body.asset };
    }, { organizationId, projectId, assetId, fileName, metadata, csrfToken: csrfA });
    uploaded = true;
    if (!uploadResult.asset || !uploadResult.asset.sha256) throw new Error("Upload response omitted SHA-256 metadata.");
    const listedA = await listAssets(pageA);
    const listed = listedA.find(item => String(item.asset_id) === assetId);
    if (!listed || Number(listed.size) !== uploadResult.size) throw new Error("Browser A cannot read its server asset listing.");

    // A fresh browser context has an independent cookie jar and an empty IndexedDB.
    pageB = await openRuntimePage(contextB);
    const sessionB = await login(pageB);
    csrfB = await pageB.evaluate(async () => {
      const response = await fetch("/api/gis/auth.php?action=csrf", { credentials: "same-origin", cache: "no-store" });
      const body = await response.json();
      if (!response.ok || !body.csrf_token) throw new Error(body.error || "csrf_unavailable_after_login");
      return body.csrf_token;
    });
    if (sessionB.userId !== sessionA.userId) throw new Error("The two browser contexts authenticated as different GIS users.");
    const before = await pageB.evaluate(({ projectId, assetId }) =>
      SabzRasterRuntime.loadAsset(projectId, assetId), { projectId, assetId });
    if (before !== null) throw new Error("Browser B unexpectedly already has the raster in IndexedDB.");

    const resultB = await pageB.evaluate(async ({ organizationId, projectId, assetId }) => {
      const url = "/api/gis/raster-assets.php?organization_id=" + encodeURIComponent(organizationId) +
        "&project_id=" + encodeURIComponent(projectId) + "&action=download&asset_id=" + encodeURIComponent(assetId);
      const response = await fetch(url, { credentials: "same-origin", cache: "no-store" });
      if (!response.ok) throw new Error("Raster download failed (HTTP " + response.status + ").");
      const blob = await response.blob();
      const bytes = await blob.arrayBuffer();
      const digest = await crypto.subtle.digest("SHA-256", bytes);
      const sha256 = Array.from(new Uint8Array(digest), b => b.toString(16).padStart(2, "0")).join("");
      const parsed = await SabzRasterRuntime.read(new File([blob], "cross-browser.tif", { type: "image/tiff" }));
      await SabzRasterRuntime.saveAsset(projectId, assetId, blob, parsed.metadata);
      const cached = await SabzRasterRuntime.loadAsset(projectId, assetId);
      if (!cached || cached.blob.size !== blob.size) throw new Error("Browser B could not cache restored server bytes.");
      return { size: blob.size, sha256, epsg: parsed.metadata.crs.epsg,
        width: parsed.metadata.width, height: parsed.metadata.height,
        firstPixel: Number(parsed.bands[0][0]), cachedBytes: cached.blob.size };
    }, { organizationId, projectId, assetId });

    if (resultB.size !== uploadResult.size) throw new Error("Cross-browser byte length mismatch.");
    if (resultB.sha256 !== listed.sha256) throw new Error("Cross-browser SHA-256 mismatch.");
    if (resultB.epsg !== 32638 || resultB.width !== 4 || resultB.height !== 3 || resultB.firstPixel !== 11)
      throw new Error("Cross-browser GeoTIFF metadata or pixel verification failed.");

    process.stdout.write(JSON.stringify({
      status: "PASS",
      assetId,
      projectId,
      organizationId,
      browserContexts: 2,
      independentIndexedDB: true,
      bytes: resultB.size,
      sha256: resultB.sha256,
      epsg: resultB.epsg,
      dimensions: [resultB.width, resultB.height],
      firstPixel: resultB.firstPixel,
      secondBrowserCacheBytes: resultB.cachedBytes
    }, null, 2) + "\n");
  } finally {
    const cleanupPage = pageB || pageA;
    const cleanupCsrf = (pageB && csrfB) ? csrfB : csrfA;
    if (uploaded && cleanupPage && cleanupCsrf) {
      try {
        await cleanupPage.evaluate(async ({ organizationId, projectId, assetId, csrfToken }) => {
          const form = new FormData();
          form.append("action", "delete");
          form.append("organization_id", organizationId);
          form.append("project_id", projectId);
          form.append("asset_id", assetId);
          await fetch("/api/gis/raster-assets.php", {
            method: "POST", credentials: "same-origin", cache: "no-store",
            headers: { "X-CSRF-Token": csrfToken, Accept: "application/json" }, body: form
          });
        }, { organizationId, projectId, assetId, csrfToken: cleanupCsrf });
      } catch (error) {
        process.stderr.write("WARNING: E2E asset cleanup failed; remove test asset " + assetId + " manually. " +
          (error.message || error) + "\n");
      }
    }
    await contextA.close();
    await contextB.close();
    await browser.close();
  }
})().catch(error => {
  process.stderr.write((error.stack || error.message || String(error)) + "\n");
  process.exitCode = 1;
});
