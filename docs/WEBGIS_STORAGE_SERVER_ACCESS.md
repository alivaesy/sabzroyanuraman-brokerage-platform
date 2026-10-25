# WebGIS — Architecture, Persistence, Deployment, Server Operations, and Handoff

**Last reviewed:** 2026-10-25  
**Repository:** `alivaesy/sabzroyanuraman-brokerage-platform`  
**Default branch:** `master`  
**WebGIS entry point:** `sabzroyanuraman-site/web-gis.html`  
**Public entry URL:** https://sabzroyanuraman.com/web-gis.html

> Purpose: this is the durable handoff document for future ChatGPT sessions, coding agents, and human developers. Read it before investigating or changing WebGIS, persistence, deployment, or server configuration. It distinguishes verified facts from assumptions and unfinished work. Never add passwords, tokens, private keys, session cookies, or database connection strings here.

## 0. Rules to avoid repeating work or causing regressions

1. Read this document first, then inspect the current GitHub file/branch and live deployment before editing.
2. Do not assume `master`, a pull-request branch, the cPanel copy, and the public site contain the same code. Compare commit/file hashes or timestamps.
3. Do not redo a change until checking the relevant commit/PR and this document's status notes.
4. Preserve a backup before deploying to the server. Download to a temporary file, validate it, then replace the live file atomically where possible.
5. Keep changes focused; run syntax checks and the targeted regression checklist. Never claim browser-tested behavior based only on source inspection.
6. Keep server secrets server-side. No database credentials in browser JavaScript, HTML, GitHub, client logs, or this document.
7. Do not merge unfinished WebGIS work merely because the page loads or the tool appears in a menu. Confirm actual behavior in a browser.

## 1. Source of truth, branches, and deployment locations

- Main WebGIS source on master: [`web-gis.html`](https://github.com/alivaesy/sabzroyanuraman-brokerage-platform/blob/master/sabzroyanuraman-site/web-gis.html)
- Public website: https://sabzroyanuraman.com/
- Expected WebGIS URL: https://sabzroyanuraman.com/web-gis.html
- Server document root used during recent maintenance: `/home/sabzroyanuraman/public_html`
- Active improvement PR: [PR #31](https://github.com/alivaesy/sabzroyanuraman-brokerage-platform/pull/31), branch `fix/webgis-stability-bundle`. It was recorded as a **Draft, not merged into master** at the last review. Recheck current status before relying on it.

### How to determine what users are actually running

Before making a fix, record:
- GitHub branch and commit SHA containing the proposed change.
- Current `master` file version.
- Current server file version and modification time.
- Whether the public page reflects the server file (hard refresh/cache bypass if needed).
- Which commit/branch the deployment process actually deploys.

If the public page is old or returns 404, inspect cPanel document root, filename/case, deployment process, and cache. Do not create another duplicate WebGIS file as a workaround without first locating the deployed entry point.

### Safe deployment pattern

Use only after verifying the correct path and intended source. Example shell pattern (adapt after inspecting the environment; do not run blindly):

```bash
cd /home/sabzroyanuraman/public_html || exit 1
T=$(mktemp) || exit 1
curl -fLsS --retry 3 'https://raw.githubusercontent.com/alivaesy/sabzroyanuraman-brokerage-platform/master/sabzroyanuraman-site/web-gis.html' -o "$T" || { rm -f "$T"; echo DOWNLOAD_FAILED; exit 1; }
test -s "$T" || { rm -f "$T"; echo EMPTY_DOWNLOAD; exit 1; }
grep -q '<html' "$T" || { rm -f "$T"; echo UNEXPECTED_FILE; exit 1; }
# Back up the live file before replacing it:
cp -p web-gis.html "web-gis.html.bak.$(date +%Y%m%d-%H%M%S)" || { rm -f "$T"; exit 1; }
# Review/validate the temporary file before the final replacement:
# diff -u web-gis.html "$T" | less
# Only after review:
# install -m 0644 "$T" web-gis.html
rm -f "$T"
```

This is a template, not evidence that deployment is automatic or that the current server matches GitHub. Avoid replacing the live file until the downloaded source and diff are reviewed. If using a deployment script, inspect its target branch and failure handling first.

## 2. WebGIS architecture — current understanding

The application is primarily a browser-based GIS interface implemented in `sabzroyanuraman-site/web-gis.html`. It provides a map, layer/tool interfaces, editing and geoprocessing-related controls, and connections to remote geospatial services. Its browser storage and remote service connections are distinct systems.

### Main layers of responsibility

1. **UI / map:** map canvas, layer table of contents, toolbars, attribute table, identify/find interactions, dialogs and toolbox menus.
2. **In-browser GIS operations:** drawing/editing and geometry or feature operations supported by the JavaScript libraries and application code.
3. **Project persistence:** project/session state stored in the current browser's IndexedDB; some preferences use localStorage.
4. **Remote data access:** supported service URLs such as GeoJSON, WFS/GeoServer and ArcGIS FeatureServer.
5. **Server-side persistence (not yet proven end-to-end):** a possible `window.__webGisGdbBridge` integration and/or protected backend API would be required to persist edits to a shared SQL/PostGIS database. Their live wiring and successful database writes are not confirmed by the source review recorded here.

A tool appearing in a menu proves only that a UI entry exists. It does not prove the algorithm is implemented, handles errors, respects selection/filters, or persists results.

## 3. What gets saved, where, and who can see it

### 3.1 IndexedDB — browser-local project state

The current master source includes browser-side project persistence/autosave and code that reports saving/verification in **IndexedDB**.

- Data belongs to that browser profile and site origin.
- It is not automatically shared with other users, computers, or browser profiles.
- Clearing site data, using a different browser/profile, or changing the origin can make the local project unavailable.
- Browser-local save is not a server backup and does not publish edits to WFS, GeoServer, FeatureServer, PostgreSQL, or PostGIS.
- Important projects should be exported/backed up using a supported export path before clearing browser data or changing storage code.

### 3.2 localStorage — preferences, not a shared database

Some UI settings/preferences are stored in `localStorage`. This is also scoped to the browser profile and origin. Do not treat it as authoritative shared project storage or a database.

### 3.3 Remote layer loading versus saving edits

The application can load online data using service endpoints described below. A successful layer load means that data was read. It does **not** mean the application has permission or code to write edits back to that service.

Before enabling an editing workflow against a remote service, verify:
- the service supports editing;
- authentication and authorization are correctly enforced;
- the application sends the appropriate transaction/write request;
- server responses and conflicts are handled;
- a read-after-write check confirms the saved result;
- failed writes are visible to the user and do not silently disappear.

### 3.4 `window.__webGisGdbBridge` — conditional integration point

The source contains `flushGeodatabaseEdits()`, which calls `window.__webGisGdbBridge.save()` if that bridge exists. This is conditional. Do not infer that a bridge exists in production or is connected to PostGIS just because the function exists.

To verify it, inspect:
1. all references to `__webGisGdbBridge` in the repository;
2. any backend/API implementation and deployment configuration that injects the bridge;
3. browser console/network requests during save;
4. server logs and database state;
5. a controlled write/read-back test using non-sensitive test data.

If there is no bridge/API implementation, document it as a missing feature rather than manufacturing a fake “database connected” status.

## 4. SQL / PostgreSQL / PostGIS — status and target design

### Current status

**Not confirmed:** live database host/connection, schema and table names, database role/permissions, connection pool, API routes, migrations, production credentials/configuration, or successful WebGIS-to-PostGIS write/read-back.

Therefore, do not describe the application as having working SQL/PostGIS persistence until these items are inspected and a test result is recorded.

### Recommended secure architecture

```text
Browser WebGIS
   | HTTPS + authenticated request
   v
Protected server API
   | server-side credentials; validation; authorization; transaction
   v
PostgreSQL + PostGIS
   | geometry + attributes + constraints + indexes
   v
API response / read-after-write verification
```

- Never connect directly from browser JavaScript to PostgreSQL.
- Store secrets in server environment/configuration with restricted permissions, not in Git.
- Authenticate users on the server; enforce authorization per project/layer/operation.
- Validate geometry type, SRID, coordinate ranges, attribute schema and request size.
- Use parameterized SQL, transactions, rate limits, CSRF protections where cookie sessions are used, and audit logs for edits.
- Use stable feature IDs and an explicit conflict/version strategy for concurrent editing.
- Do not claim multi-user synchronization until concurrent updates and conflict behavior are tested.
- Backups and restore tests are separate from application-level save tests.

### Minimum end-to-end proof for PostGIS

Record all of the following with timestamps and sanitized output:
1. Identify the API endpoint and the server-side database connection configuration location (never publish the secret).
2. Confirm the database/schema/table and expected geometry SRID/type.
3. Create a test feature via WebGIS/API.
4. Receive a successful server response with stable feature ID.
5. Read the same feature back through the API.
6. Confirm its geometry and attributes in PostGIS using a trusted server-side/admin tool.
7. Update and delete the test feature; verify authorization and expected results.
8. Test an invalid geometry, unauthorized request, and concurrent/conflicting update.
9. Remove test data if appropriate and record the test outcome.
10. Confirm backups/restore process independently.

## 5. Supported remote data connections

The source review recorded these loading paths:

- **GeoJSON:** a reachable GeoJSON URL.
- **ArcGIS Feature Service:** a FeatureServer layer URL.
- **WFS / GeoServer:** a WFS endpoint; the application attempts GetCapabilities layer discovery and paged GetFeature requests where supported.

Common reasons a remote layer may fail: CORS, invalid URL, TLS/HTTPS mismatch, unsupported service version, authentication, permission, schema differences, paging limits, invalid geometry, or network errors. Capture the exact URL type, response status and browser-console/network error (redact credentials and tokens).

Do not test with private service credentials in public screenshots, commits, or issue comments. A layer loaded from WFS does not automatically support WFS-T writes; read and transaction capabilities must be checked separately.

## 6. Libraries and offline/local dependency policy

The project has been worked on with Leaflet, Leaflet.draw, Proj4js, Turf.js and other GIS/file-format libraries. Earlier work notes said library files had been downloaded locally, but the current deployed HTML and its actual runtime requests must be checked before asserting that every dependency is local.

For an offline-capable deployment:
1. Inventory every `script src`, `link href`, dynamic import, worker, font, tile and other network dependency in `web-gis.html`.
2. Distinguish required runtime libraries from optional services (basemaps, geocoding, external layers).
3. Verify local asset paths, exact versions, license files and integrity.
4. Test with network access blocked; inspect DevTools Network for unexpected CDN requests.
5. Make basemap failure non-fatal: the map and local layers/tools should remain usable when an external tile source is unavailable.
6. Do not remove a dependency or change library versions without running the regression tests below.

“Libraries are local” does not mean basemaps or remote data services are local; those are separate dependencies.

## 7. Recent server-side work recorded: staging authentication and ModSecurity

This section summarizes previous session results; it is **historical evidence, not a live re-audit**. Inspect current configuration before making another change.

### 7.1 ModSecurity exception for staging logout

- File recorded as edited: `/etc/apache2/conf.d/modsec/modsec2.user.conf`
- Intended scope: allow HTTP DELETE only for `/api/gis/staging-auth/auth.php?action=logout`, scoped to the specific endpoint and logout action.
- Validation: `apachectl -t` returned `Syntax OK`.
- Apache graceful restart was recorded as successful.
- Security rule: do not broaden this exception to all API routes or all DELETE requests. Check the exact rule and rule IDs before editing to avoid duplicate/conflicting exceptions.

### 7.2 Staging session smoke test

Command recorded: `bash /root/test-gis-session.sh`

```text
LOGIN_HTTP=200
ME_HTTP=200
ME_RESPONSE=USER_PRESENT
LOGOUT_HTTP=200
ME_AFTER_LOGOUT_HTTP=401
```

Interpretation: at the time of the test, the staging login → session check → logout → post-logout rejection cycle behaved as expected.

This does not prove:
- production identity-provider integration;
- user/organization onboarding or regulatory identity checks;
- database persistence;
- CSRF/session-fixation protection or a full security audit;
- that the server configuration remains unchanged today.

### 7.3 Other hardening context previously recorded

- `rpcbind` was reported disabled.
- SSH settings were reported as `PermitRootLogin prohibit-password`, `PasswordAuthentication no`, `PubkeyAuthentication yes`.
- Apache headers for `sabzroyanuraman.com` were reported configured/reviewed: HSTS, `X-Content-Type-Options: nosniff`, and `Referrer-Policy`.

These are historical notes only. Verify current service status and active configuration before relying on them. Do not weaken SSH, WAF or security headers to make a test pass without understanding the cause.

## 8. Current WebGIS enhancement work and known risks

### PR #31

- PR: https://github.com/alivaesy/sabzroyanuraman-brokerage-platform/pull/31
- Branch: `fix/webgis-stability-bundle`
- Last recorded state: Draft, not merged into `master`; recheck current state before continuing.
- Last recorded head in prior notes: `04ce95f793f5ecb4b978eed670893320ddbf4ce4` (check live head; it may have changed).

Changes noted in that branch:
- Definition Query serialization/restoration and reapplication after layers load.
- Numeric comparison handling in Definition Query.
- Tool catalog wiring for Field Calculator, Definition Query and Layout View.
- Basemap state tracking and selector rollback if `selectBasemap()` fails.
- Layout View overlay with the live Leaflet map moved into a layout slot, title, legend, north arrow, scale annotation, print/PDF action, and restoration of the map element's original parent/style.
- Print CSS hides other body siblings while layout is active.

These notes describe intended/source-level changes, not verified browser behavior. Inspect the current PR diff before repeating them.

### Geoprocessing catalog

A source-level inspection previously counted **23 catalog entries** categorized as Geoprocessing. This is only a count of listed tools; it is not proof that all 23 algorithms execute correctly or produce valid output.

### Priority order requested

1. Stabilize and test the Geoprocessing menu/submenus and basemap switching.
2. Complete Definition Query, including save/restore and filtering behavior.
3. Field Calculator.
4. Spatial selection.
5. Layout View and print/PDF behavior.
6. Continue toward ArcMap 10.8-style workflow without falsely claiming full parity.

### Known regression history

Previous changes have caused tools, basemap switching, and geoprocessing submenus to disappear or stop working. One recorded JavaScript failure was `Cannot access 'autoSaveTimer' before initialization`, caused by `recordMapExtent()` running before the `let autoSaveTimer` declaration. When touching initialization order or shared state, inspect declaration/use order and check the browser console immediately.

Other issue to recheck: coordinate display/UTM zone logic. UTM zones are numbered 1–60 and have north/south hemispheres; zone boundaries matter. The user previously reported the zone display stuck or incorrect around Iran (including a 38/39 transition near Tehran). Do not hardcode a single zone for every coordinate; test multiple known points and hemisphere handling.

## 9. Browser regression checklist

Run in a real browser after any change to `web-gis.html`. Record browser/version, URL, commit SHA, test date, pass/fail and console errors. A menu rendering is not an execution test.

| Area | Minimum test | Expected result |
|---|---|---|
| Startup | Open public URL and reload | No uncaught JS errors; map and toolbars render |
| Dependencies | Inspect Network requests | Required assets load; no unexpected CDN dependency if offline mode is claimed |
| Basemap | Switch between each available basemap; simulate failed tile source | Selector and map state remain consistent; failure does not break tools |
| TOC/layers | Add, remove, reorder, toggle visibility | Correct layer state and redraw |
| Identify/Find | Query a known feature and search known attribute | Correct result and stable UI |
| Attribute table | Open, sort/filter if supported, select row/feature | Table and map selection remain synchronized |
| Definition Query | Create, apply, clear, save project, reload | Only matching features display; query survives project restore as intended |
| Geoprocessing | Open every category/submenu; run a small controlled example for each implemented tool | Menu does not lose entries; output/error is truthful; geometry and attributes are correct |
| Field Calculator | Calculate a field on a test layer/selection | Correct values, null/error handling and undo/backup behavior |
| Spatial selection | Select by location with known geometries | Correct selected set; projection/CRS handled correctly |
| Editing | Add/update/delete test feature | User sees success/failure; unsaved state is clear |
| Persistence | Save, refresh, reopen project in same browser | Expected local project state restores |
| Browser isolation | Open another profile/device | Confirm local storage is not incorrectly presented as shared |
| Remote data | Load GeoJSON, WFS, FeatureServer test sources | Correct read behavior and clear errors for unsupported/auth failures |
| PostGIS | Perform controlled API write/read-back | Only mark passed after server and DB verification |
| Layout View | Enter/exit; inspect map restoration | Original map parent/style and controls restore without duplication |
| Print/PDF | Preview and print/export | Layout content is complete and map remains usable afterwards |
| Coordinate/CRS | Test known points across zones and hemispheres | Correct zone/hemisphere and coordinate conversion |
| Accessibility/responsiveness | Resize viewport and keyboard through dialogs/menus | No inaccessible or clipped critical controls |

When a test fails, save the exact reproduction steps, console error and relevant commit. Fix the root cause rather than adding a second competing handler or duplicate menu entry.

## 10. Change and test log template

Append a dated entry when meaningful work is done; update older claims if disproven.

```text
Date/time (timezone):
Agent/person:
Branch + commit:
Files changed:
Reason:
Change summary:
Checks run:
Browser/URL:
Console/network errors:
Tests passed:
Tests failed / not run:
Deployment status:
Database write/read-back status:
Backup/rollback location (no secrets):
Next exact step:
```

Keep status labels explicit: **verified**, **source-inspected only**, **not tested**, **blocked**, or **not implemented**.

## 11. Safe continuation checklist

- [ ] Read this document and inspect current PR #31 state/diff.
- [ ] Compare master, PR branch and live server copy before editing.
- [ ] Reproduce the reported bug and record console/network evidence.
- [ ] Make the smallest root-cause fix; do not replace unrelated working features.
- [ ] Run static/syntax checks and the relevant browser regression tests.
- [ ] Verify save/restore separately from remote-service read/write.
- [ ] Verify SQL/PostGIS only through a protected backend and a controlled read-after-write test.
- [ ] Back up before deployment; validate the exact artifact and deployment target.
- [ ] Update this document with commit, test evidence and remaining gaps.

## 12. Explicit non-claims

Until new evidence is recorded, do **not** claim that:
- all 23 geoprocessing tools work;
- WebGIS has full ArcMap 10.8 parity;
- every JavaScript dependency is local/offline-ready;
- project state is shared across users/devices;
- edits are being persisted to PostgreSQL/PostGIS;
- PR #31 has been merged or deployed;
- the historical server hardening configuration is still active and correct.

The goal of this file is to preserve verified knowledge, make missing pieces visible, and prevent duplicate work—not to make the project appear more complete than it is.
