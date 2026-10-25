# WebGIS — Storage, Server Changes, and Online Access

Last updated: 2026-10-25  
Repository: `alivaesy/sabzroyanuraman-brokerage-platform`  
WebGIS entry file: `sabzroyanuraman-site/web-gis.html`

> This note records verified implementation details and recent server work so later sessions do not repeat investigation. It intentionally does not contain credentials, tokens, private keys, or passwords.

## 1. Online access and source of truth

- Expected public entry URL: **https://sabzroyanuraman.com/web-gis.html**
- Main site: **https://sabzroyanuraman.com/**
- GitHub source: [web-gis.html on master](https://github.com/alivaesy/sabzroyanuraman-brokerage-platform/blob/master/sabzroyanuraman-site/web-gis.html)
- Active WebGIS enhancement work is in [PR #31](https://github.com/alivaesy/sabzroyanuraman-brokerage-platform/pull/31), branch `fix/webgis-stability-bundle`. It is a **Draft** and is **not merged to master**. Check that branch before repeating or overwriting those changes.
- Server document root used during the recent maintenance: `/home/sabzroyanuraman/public_html`. The deployment workflow used the public GitHub raw file for `master` and downloaded it to a temporary file before replacing anything. Do not assume a branch is deployed merely because it exists on GitHub.
- If the expected public URL returns 404 or shows old content, verify the cPanel document root, deployment state, and file timestamp before making a second copy.

## 2. How WebGIS data is saved today

### Browser-side project storage

The current `master` implementation includes browser-local project persistence and autosave. Editing code explicitly reports that changes are saved and verified in **IndexedDB**. Some preferences/settings are stored in `localStorage`.

Important implications:
- A saved project in IndexedDB is local to that browser profile/origin. It is not automatically shared with another computer, browser, or user.
- Clearing browser site data can remove local projects/settings. Export/backup important projects before clearing browser data.
- Saving the browser project does **not** mean data has been written to PostgreSQL/PostGIS or back to WFS/GeoServer. The code explicitly warns that local IndexedDB saving does not publish edits to those services.
- `flushGeodatabaseEdits()` calls `window.__webGisGdbBridge.save()` only if that bridge has been provided by the hosting integration. Its presence and connection to a real server database must be verified in the live deployment; do not infer it from the function name.

### SQL / PostgreSQL / PostGIS status

**Verified from the current WebGIS source:** the application can load online data using public GeoJSON URLs, ArcGIS FeatureServer layer URLs, and WFS/GeoServer endpoints. The WFS path supports GetCapabilities layer discovery and paged GetFeature requests when the remote service supports them.

**Not verified by the source review available for this note:** a live PostgreSQL connection, PostGIS schema/table names, connection pool, server-side write API, database credentials/configuration, or a confirmed write from WebGIS into PostGIS. No database password or connection string should ever be placed in `web-gis.html` or committed to GitHub.

Therefore, treat SQL/PostGIS persistence as **not yet confirmed end-to-end** until a server-side API or geodatabase bridge is inspected and a controlled create/update/read-back test is recorded. The browser should call a protected server API; database credentials must remain server-side.

## 3. WebGIS online data connection

Use the WebGIS **Add Online Data** workflow and provide the appropriate service URL:
- GeoJSON: a public or authorized GeoJSON endpoint.
- ArcGIS Feature Service: the layer URL ending in a FeatureServer layer path.
- WFS/GeoServer: the WFS service endpoint; the application attempts GetCapabilities discovery and paged GetFeature requests where supported.

Cross-origin/CORS policy, HTTPS requirements, service authentication, and remote server permissions can prevent a valid URL from loading. A layer appearing on the map proves that it was fetched, not that edits can be saved back to its source.

## 4. Recent server work recorded (authentication / ModSecurity)

The following is the verified server work available from the recent session notes. Do not repeat it without first inspecting the current configuration and test result.

### ModSecurity DELETE exception for staging logout

- File edited: `/etc/apache2/conf.d/modsec/modsec2.user.conf`
- Purpose: allow the HTTP DELETE method only for the staging authentication logout endpoint `/api/gis/staging-auth/auth.php?action=logout`, with the rule scoped to both the endpoint and the `action=logout` query parameter.
- Validation command: `apachectl -t` → **Syntax OK**.
- Apache graceful restart completed successfully.
- Do not broaden this exception to all API endpoints or all DELETE requests.

### Authentication smoke test

Command used: `bash /root/test-gis-session.sh`

Recorded results:
- `LOGIN_HTTP=200`
- `ME_HTTP=200`
- `ME_RESPONSE=USER_PRESENT`
- `LOGOUT_HTTP=200`
- `ME_AFTER_LOGOUT_HTTP=401`

Interpretation: the tested staging login → authenticated session check → logout → rejected post-logout session cycle succeeded at that time. This is not a full security audit, does not prove production identity-provider integration, and does not prove SQL/PostGIS persistence.

### Other server configuration context already recorded

- `rpcbind` was reported disabled.
- SSH hardening was reported as `PermitRootLogin prohibit-password`, `PasswordAuthentication no`, and `PubkeyAuthentication yes`.
- Apache security headers for `sabzroyanuraman.com` were previously configured/reviewed (HSTS, `X-Content-Type-Options: nosniff`, and `Referrer-Policy`).
- These are historical notes, not a fresh verification of the server's current state. Inspect the live files and service status before changing them.

## 5. Current WebGIS development state — avoid duplicate work

- PR #31 / branch `fix/webgis-stability-bundle` contains ongoing work related to Definition Query persistence, basemap state, toolbox wiring, and Layout View. It remains a Draft and needs live browser regression testing before merge.
- The latest source-level review found **23 entries** categorized as Geoprocessing in the tool catalog; this is a catalog count, not proof that all algorithms work correctly.
- Browser tests still needed: open/close projects, Definition Query save/restore, geoprocessing submenu rendering and execution, basemap switching and failure fallback, Field Calculator, spatial selection, Layout View/print PDF, and persistence across refresh/new browser sessions.
- Do not report the full ArcMap-parity bundle as finished until these checks have been performed.

## 6. Safe continuation checklist

1. Read this document and inspect PR #31 before coding.
2. Confirm which branch is actually deployed; GitHub `master`, the PR branch, and cPanel `public_html` may differ.
3. Back up the current server file before deployment; download to a temporary file and validate it before replacement.
4. Never put secrets or database credentials in browser JavaScript or Git.
5. To confirm PostGIS persistence, identify the server-side API/bridge and perform a controlled write/read-back test with non-sensitive test data.
6. After server changes, run syntax/config validation and the relevant smoke test; record the exact result and timestamp here.
