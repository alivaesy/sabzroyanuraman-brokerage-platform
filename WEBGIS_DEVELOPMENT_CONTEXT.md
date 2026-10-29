# Web GIS Operational Context and Handoff

Last reviewed: 2026-10-25
Repository: `alivaesy/sabzroyanuraman-brokerage-platform`
Working branch/source of truth: `master`
Primary application: `sabzroyanuraman-site/web-gis.html`
Purpose: let a future ChatGPT conversation continue without asking the user to repeat project setup, account roles, validated behavior, or next steps.

## Non-negotiable workflow
1. Read this file and inspect the latest `master` HEAD before changing code.
2. Work directly on `master`. **Do not create branches or PRs** unless the user explicitly changes this instruction.
3. Check the existing implementation and recent commits before rewriting. Make grouped, targeted edits.
4. Report what was actually tested. Static inspection, CI, browser testing, and live-server testing are different levels of evidence.
5. Do not reopen features the user has confirmed working unless a regression is reported or evidence contradicts that status.
6. Keep responses concise and execution-focused; the user prefers multiple useful actions over long explanations.
7. Never commit passwords, password hashes, DB connection values, API secrets, or other credentials.

## Repository and deployment locations
- Main WebGIS source: `sabzroyanuraman-site/web-gis.html`
- GIS API and SQL migrations: `sabzroyanuraman-site/api/gis/`
- GIS auth/account schema: `sabzroyanuraman-site/api/gis/sql/002_accounts_orgs_projects.sql`
- Fixed test-workspace provisioning script: `scripts/provision-gis-test-organizations.php`
- Local GIS vendor assets: `sabzroyanuraman-site/vendor/web-gis/`
- Server repository clone: `/home/sabzroyanuraman/repositories/brokerage-platform`
- Public webroot: `/home/sabzroyanuraman/public_html`
- Private configuration directory: `/home/sabzroyanuraman/private/` (outside public webroot)
- Server's installed copy of fixed provisioning script: `/home/sabzroyanuraman/private/provision-gis-test-organizations.php`

The server has been reported as Ubuntu 24.04.2, with cPanel PHP 8.2/8.3. The web service previously observed was `httpd` active (`apache2` and `nginx` inactive). Recheck live server state before relying on this.

## GIS online login accounts: roles and count
The user confirmed on 2026-10-25 that there are **three known login accounts**:
1. `GISADMIN` — the user's own **test account**. Do not count it as an external customer organization account. Its organization membership/mapping has not been confirmed in this document.
2. `kadaster_sabtasnad` — owner account for organization `kadaster_Sabtasnad`, slug `kadaster-sabtasnad` (last reported IDs: user 3, organization 4).
3. `test_gis` — owner account for organization `test`, slug `test` (last reported IDs: user 4, organization 5).

Therefore: 3 known login accounts total, of which 1 is the user's test/admin account and 2 are organization-owner accounts. This is the last user-confirmed inventory, **not a fresh live database count**. For a current count, query the live database; never infer it from this note alone. Do not record passwords or password hashes here.

## GIS authentication database and schema
- GIS account/auth database: PostgreSQL database `gis_staging`.
- This is distinct from the separate spatial-data database `sabzroyan_gis` (PostGIS).
- Core auth/org tables: `gis_users`, `gis_organizations`, `gis_organization_members`, `gis_subscriptions`.
- Related session, login-attempt, project, feature, and audit tables also exist; inspect the live schema before making assumptions.
- Schema file: `sabzroyanuraman-site/api/gis/sql/002_accounts_orgs_projects.sql`. It is a migration/schema file, not an account-creation command. Do not rerun it casually against a live DB.
- Private DB config files (paths only; never expose their contents): `/home/sabzroyanuraman/private/gis-auth-db.php` and `/home/sabzroyanuraman/private/gis-admin-db.php`.
- Never place DB credentials in HTML/JavaScript or expose them from `public_html`. Browser access must go through authenticated server-side APIs.

## Account/organization provisioning
Existing fixed script:
- Repository: `scripts/provision-gis-test-organizations.php`
- Installed server copy: `/home/sabzroyanuraman/private/provision-gis-test-organizations.php`
- Run on the server using CLI only: `php /home/sabzroyanuraman/private/provision-gis-test-organizations.php`
- Uses private config `/home/sabzroyanuraman/private/gis-admin-db.php`; prompts for passwords with terminal echo disabled.
- Idempotently handles only the two predefined workspaces `kadaster_Sabtasnad` / `kadaster-sabtasnad` / `kadaster_sabtasnad` and `test` / `test` / `test_gis`. It preserves existing user/org IDs and passwords, ensures owner membership, and creates an active subscription only if needed. It aborts on conflicting/inactive records; it does not run migrations or delete records.
- It does **not** provision `GISADMIN` and is **not** a generic new-customer provisioning tool.
- To add another customer organization, create/review a separate secure CLI-only, parameterized provisioning flow with transaction, uniqueness/conflict checks, hidden password prompt, owner membership, subscription, and audit record. Do not adapt the fixed test script without review.
- Never pass passwords as CLI arguments, save them in shell history, or paste them into chat.

## User-confirmed behavior and known boundaries
The user reported on 2026-10-25 that the GIS login/session, project save/restore, separation of drawings between accounts, and attribute tables were working correctly and that drawings did not overlap across accounts. Treat these as user-validated at that time; do not casually alter them.
Recent master commits included fixes to the GIS login/logout/session flow, switching away from legacy/platform-admin sessions without organization membership, and organization selection for cloud projects. Review current code before changing this flow.
Historical diagnostic note (2026-10-24): requests to the GIS API returned HTTP 200 in repeated endpoint checks, while the WebGIS “Add Online Data” path showed `Failed to fetch`; possible CORS/auth/response handling was considered. Current status was not re-verified in this documentation update. Re-test only if the problem is still present or reported again.

## WebGIS architecture and development direction
- Goal: a practical browser GIS with ArcMap 10.8-inspired tool organization and workflows, not merely a basic Leaflet map.
- Desired/ongoing capabilities include layer tree/TOC, attribute table, Identify/Find, Standard/Tools/Editor toolbars, ArcToolbox/geoprocessing, Layout View, Layer Properties, Definition Query, Field Calculator, spatial selection, editing/undo, project save/restore, WFS/GeoServer interoperability, and secured PostGIS-backed operations.
- Spatial services and editing must go through server-side APIs. Never put DB credentials in the browser.
- Preserve project geometry, attributes, layer visibility/order, symbology, extent, and selection through save/restore.
- GIS account projects and edits must remain organization/user scoped, with authorization and audit logging enforced server-side.

## Dependency status — important discrepancy to resolve
The user has said GIS libraries were downloaded/self-hosted and wants no CDN dependence. The repository does contain local assets under `sabzroyanuraman-site/vendor/web-gis/`. However, during this review, the current `master` version of `web-gis.html` (blob SHA `4c9d29d3b9ea79a0aa34df6a58ceb8dcc56cc034`) was observed to still reference these external CSS URLs in its first lines:
- `https://unpkg.com/leaflet@1.9.4/dist/leaflet.css`
- `https://unpkg.com/leaflet-draw@1.0.4/dist/leaflet.draw.css`

Therefore do **not** claim the whole page is fully CDN-independent until every script, stylesheet, font, worker, and dynamically loaded dependency has been checked and replaced/verified. This is a discrepancy to fix in a focused follow-up, while ensuring the map and tools do not regress.

## Development priorities
1. Reconcile external CDN references with the local vendor assets and verify no hidden/dynamic CDN references remain.
2. Audit the visible tool menu against actual handlers/state and test each tool end-to-end; tool labels alone do not count as implementation.
3. Stabilize map/basemap and existing tools before adding new functionality.
4. Continue ArcMap-style selection: click/multi-select, rectangle/polygon, attribute/location selection, switch/clear selection, zoom to selection, and create layer from selected features.
5. Improve Definition Query, Field Calculator, spatial selection, and Layout View after regression checks.
6. Verify WFS GetCapabilities, layer discovery, BBOX filtering, pagination, authentication, error handling, and GeoServer interoperability.
7. For multi-user editing, enforce server-side permissions, conflict handling, and audit trails.
8. Keep project persistence and GIS account/session regression tests in the loop.

## Regression checklist
- [ ] No uncaught JavaScript errors on load and after common tool actions.
- [ ] Basemap and map navigation continue working after opening/closing tools.
- [ ] All visible tool/menu entries have correct event wiring and meaningful outcomes.
- [ ] Layer visibility/order, map, and attribute table remain synchronized.
- [ ] Selection, multi-selection, clear/switch selection, and zoom-to-selection work.
- [ ] Editing, undo, cancel, and save do not silently lose data.
- [ ] Save project, reload page, restore project; verify geometry and attributes.
- [ ] Separate accounts/projects cannot read or overwrite each other's drawings.
- [ ] GIS sign-out ends the server session and clears the client session.
- [ ] CDN audit passes before declaring dependencies fully self-hosted.
- [ ] Relevant CI passes; live browser/server tests are reported separately.

## Last review evidence (2026-10-25)
- Current WebGIS file SHA: `4c9d29d3b9ea79a0aa34df6a58ceb8dcc56cc034`.
- Current provisioning script SHA: `a5675b6613d47bbad63b85e2d739bf757f7a4b89`.
- Current schema migration SHA: `c1acdba081d657bea5083b92df66c650bc0c1a06`.
- Operational context file updated directly on `master`; no branch or PR was created.
- This review inspected repository content; it did not run a live database query, browser test, or server deployment.

## Session handoff rule
At the end of each meaningful batch, update this file with the verified date, commit SHA, files changed, tests actually run, outcomes, and unresolved items. Keep this file as the durable handoff for future ChatGPT accounts/conversations.


## WebGIS ArcMap-style scale and menus (2026-10-25)
- Updated `sabzroyanuraman-site/web-gis.html` directly on `master`; implementation commit: `180d6f7bae33359197b05845350bb5776c1236a9`.
- Replaced the fixed zoom-level selector with a typed scale input and suggestions from 1:1,000,000 to 1:500. Scale denominator uses center latitude and the requested 96-DPI formula; Enter computes a fractional zoom, temporarily disables `zoomSnap`, clamps to map min/max zoom, then restores snapping. Scale refreshes on `zoomend` and `moveend`.
- Removed Backspace from the sketch-vertex removal shortcut; Delete remains.
- Removed Editor and Topology from the main menu and added both to the beginning of the Editor toolbar. Toolbar-originated menus open below the triggering toolbar button.
- Added File > Print Map, Edit > Cut/Copy/Paste/Delete wired to existing toolbar buttons, and View > Layout View / Data View wired to the registered `layoutViewTool`.
- No CDN URLs or library references were changed.
- Verification: GitHub content read-back and targeted source assertions confirmed the expected edits. The requested `node --test tests/web-gis-static.test.cjs` was **not run** because this session has no network access to retrieve/run the repository files locally; GitHub reports no status checks attached to this commit. No headless browser test or live deployment test was run.
- Remaining: run all 13 static tests and browser checks (all 10 main menus, Editor-toolbar menu placement, entering `1:5000`, Layout View, and console errors); verify scale behavior at min/max zoom and on basemap changes.


### Verification update (2026-10-25)
- Re-evaluated all 13 assertions from `tests/web-gis-static.test.cjs` against the committed HTML using a JavaScript harness: **13/13 passed**. The 11 inline JavaScript blocks also passed syntax compilation.
- This was not the literal `node --test tests/web-gis-static.test.cjs` CLI invocation; the local container could not reach GitHub to check out the repository. No browser/Playwright run was performed.


### Verification update (2026-10-25, Editor toolbar row sizing)
- Fixed the Editor toolbar CSS to use the same compact 28px row height as Standard/Tools; disabled wrapping and enabled horizontal scrolling so its longer toolset no longer makes the row taller.
- Topology remains immediately beside Editor at the start of the Editor toolbar.
- HTML commit: ccd3fae820b321a448a98b8b80aa13994c266b5c.
- CSS change committed to master; live browser/deployment verification was not performed.


### Verification update (2026-10-25, Editor toolbar late-style override)
- Root cause: the previous height CSS had accidentally been inserted into the generated map-viewer HTML string, not the main page. A later body-level style also reset #editorToolbar to wrapping/32px padding, explaining the brief correct appearance followed by expansion.
- Removed the misplaced CSS and appended a high-specificity final override at the end of the actual page, enforcing a 28px row after later styles.
- HTML commit: 2719160fd53e31b02c947a83bce8dd7c127fc6fc.
- Still requires live browser confirmation after refresh.


## Property form ↔ Web GIS handoff (2026-10-26)
- Implemented directly on `master` in `sabzroyanuraman-site/map-preparation-form.html` and `sabzroyanuraman-site/web-gis.html`.
- The property-location form opens `web-gis.html?propertyHandoff=1` in a separate window. In this mode Web GIS immediately presents the existing **GIS Online Login** dialog; ordinary Web GIS visits are unchanged.
- After drawing, the user can select the property feature and click **ذخیره در حساب GIS و بازگشت به فرم**. The handoff requires an authenticated GIS organization, saves the serialized project through the existing server API, then sends the selected GeoJSON feature and representative coordinates to the opener with same-origin `postMessage`. The form validates the origin, source window, message type/version, and coordinate ranges, saves the location in its existing draft, and displays the geometry on its Leaflet map.
- This is code-level implementation only; no live browser, server, or end-to-end login/save test was run in this session. Follow-up: run the full flow in the deployed site, verify popup permissions, organization selection, remote save success, form draft restoration, and polygon rendering. Do not treat a successful postMessage as proof of server persistence; the UI waits for the GIS save API to return first.


### Handoff implementation read-back (2026-10-26)
- Commits on `master`: form integration `2fd7f865c96ab0c33fa2d49df7bd48997573bc36`; Web GIS handoff `7f99c0f63a1b2a3cb7cea73dd58851e1913e2709`; password masking/cancel recovery `2f520424ba551b9b60655c033d6a7a18d1003ae7`; this context update follows.
- GitHub read-back confirmed the latest file contents. Static checks passed for all seven targeted handoff assertions. JavaScript syntax compilation passed for the form's 1 inline script and Web GIS's 12 inline scripts.
- Syntax compilation does not execute code. Browser UI, popup handling, login, server-side project persistence, geometry transfer, and draft restoration remain untested end-to-end.


### Property GIS login scope fix (2026-10-26)
- User reported `remoteGisUser is not defined` and no GIS Online Login dialog. Root cause: the handoff code was in a separate script block and could not access the main Web GIS script's lexical `let` state or `remoteGisLogin()`.
- Fixed in commit `5868f001592c65fbdbda7df2571d613432a2fadf`: moved the handoff logic into the existing main GIS script scope so it can call the real login dialog, organization selector, and save API.
- Re-read master after commit. Static validation passed: all 11 inline JavaScript blocks compile, the handoff and auth state are now in the same script scope, and the `GIS Online Login` dialog remains present. Browser end-to-end verification is still required.


## ArcMap MXD/MPK import status — 2026-10-27
- Added **File → Import ArcMap Document (.mxd / .mpk)** in `sabzroyanuraman-site/web-gis.html`.
- The picker accepts `.mxd`, `.mxt`, and `.mpk`. `.mpk` is inspected as a ZIP package and supported embedded GeoJSON, KML, and Shapefile datasets are added to the existing map/TOC.
- For `.mxd`/`.mxt`, the browser attempts the optional same-origin endpoint `api/gis/import-mxd.php` and accepts returned GeoJSON feature collections. **That converter endpoint has not been implemented or verified here**; if absent, the UI explains the limitation rather than claiming the MXD opened.
- Important limitation: this is an import entry point and partial data extraction, **not** full ArcMap project reconstruction. It does not preserve MXD symbology, fonts, label rules, layer-source connections, map frames, or layout. Full reconstruction requires a Windows ArcMap/ArcObjects-compatible conversion worker or another licensed conversion workflow, plus a defined server response contract.
- Static regression test added in `tests/web-gis-static.test.cjs`. The test suite and browser workflow have not yet been executed in this turn; do not report them as passing.


## File Geodatabase vs. MXD settings — 2026-10-27

- A File Geodatabase (.gdb) primarily contains feature classes, geometries, attributes, and related geodatabase schema. It does **not** generally contain the ArcMap map's complete layer symbology, label placement/fonts, layer order/grouping, bookmarks, or page layout. Those are primarily stored in the MXD/MXT map document (with some styling possibly embedded in source data or layer files).
- The web importer must not claim a faithful MXD import when it only converts GDB feature classes to GeoJSON. Full fidelity needs an ArcMap/ArcObjects-compatible extraction step that exports a manifest for layer sources, renderer classes/symbols, label expressions and placement, fonts, visibility/order/groups, coordinate systems, bookmarks, and layout, plus accessible data/assets.
- Recent master fix: GDB import reports the number of datasets, imported layers/features, empty classes, and failed classes instead of silently swallowing per-layer failures. It does not yet fix GDAL's possible inability to preserve the selected .gdb directory structure in the browser; runtime testing with the user's actual GDB is still required.
- Automatic labels based merely on fields like name, title, or کد were disabled because they are not proof that ArcMap configured those fields as labels and caused visual clutter. Labels should be restored from explicit MXD metadata or configured in Layer Properties.
- Removed a global vector-effect: non-scaling-stroke rule so Leaflet's normal zoom-aware vector rendering applies. Vector geometry coordinates remain unchanged; display symbol width is a renderer setting, not a change to the geometry.
- Commit: 6aa6f37cabb9359d949fb3a7a86ae82d1340d658. Inline JavaScript syntax check passed (12 inline scripts, 0 syntax errors); no live browser/GDB runtime test was performed.

## File Geodatabase importer update — 2026-10-27

- Master commit `66417ad9a41850fbdaf37bb83cd3f283ddca9d08` packages the selected `.gdb` folder contents while preserving relative paths, opens the archive through GDAL's `vsizip` handler, iterates every layer reported within each GDAL dataset, and uses explicit SQL when a dataset contains multiple feature classes. It also captures GDAL dataset/schema details where available and downloads a JSON import report listing files, dataset/layer counts, feature counts, empty classes, open warnings, and failures.
- This is a code-path improvement, not yet a verified full import: the user's actual `.gdb` has not been tested in a browser. Confirm `Gdal.open` returns layer metadata as expected and that the GDAL 3.8.4 build supports the archive layout. Source coordinates are reprojected to EPSG:4326 for Leaflet display; source metadata is attached where GDAL exposes it.
- Do not claim every geodatabase component is imported. Feature classes and their simple feature attributes/geometries are the current target. FileGDB domains, subtypes, relationship classes, attachments, annotation, topology/network rules, raster/mosaic content, and ArcMap renderer/label settings need separate extraction and support. Full coverage requires a richer manifest and/or a server-side native GDAL/ArcObjects-compatible conversion worker.
- Inline JavaScript syntax check after the change: 12 inline scripts, 0 syntax errors; no live runtime/browser test.

## FileGDB import audit — follow-up corrections (2026-10-27)

- Commit `be7f7afe725ac43602807387244631c6a1e5b804` fixes a concrete archive-layout defect: the ZIP now preserves the `.gdb/` directory instead of flattening its contents into loose files at ZIP root. It also falls back to the `ogrinfo` layer inventory when `getInfo()` does not return layers, records field definitions and source/export count mismatches, and attempts to download diagnostics even when opening fails.
- Commit `7ec475302d5712232df6f946246f3267ebf5c1e7` requests preservation of feature IDs and explicitly lists unsupported content in the import report and status.
- Static regression assertions updated in commit `a0b45328a28ca0e0b96e1d3c64aeb5d491f9e059`.
- Important: the ZIP/GDAL path fix has only passed inline-JavaScript syntax checks (12 scripts, zero syntax errors); it has not been runtime-tested with a real `.gdb`. MXD/MXT import endpoint `api/gis/import-mxd.php` is referenced by the front end but was not found in the repository, so native ArcMap symbology, labels, text, and layout cannot currently be restored. A complete solution still requires an ArcMap/ArcObjects-compatible Windows extraction worker or another verified reader that exports a manifest plus original data/assets. Browser GeoJSON rendering alone cannot preserve topology rules, domains, subtypes, relationship classes, attachments, annotation, raster/mosaic datasets, exact field semantics, or full symbology.

## Full FileGDB audit follow-up — 2026-10-27

- Commit `41b0135b3785d36d6de7cd376367242d8cbbb1aa` improves the importer: retains source field definitions when GDAL exposes them, keeps empty feature classes in the layer catalog with schema metadata, compares source/export feature counts, and explicitly reports raster datasets as unsupported rather than treating them as vector classes.
- Static regression assertions added in `tests/web-gis-static.test.cjs` (commit `46fc3b8e8fc78fa6b72f8728d90f60f20c8ebc3b`). These assertions have been authored but not run in a local Node environment during this turn; inline JS syntax check passed for 12 scripts.
- Audit conclusion: this is still not 'everything'. Current importer does not yet render rasters, nor reconstruct FileGDB domains/subtypes/relationship classes/attachments/annotation/topology/network datasets, full original field types/nullability/aliases/defaults, or ArcMap renderer/labels/layout. It reprojects vector geometry to EPSG:4326 for Leaflet and retains available metadata, so it is not a lossless archival conversion. A native server-side GDAL conversion pipeline or ArcMap/ArcObjects-compatible worker plus a structured manifest is needed for complete coverage. Do not claim full import until compared against the actual source geodatabase inventory and counts.

## MXD reconstruction priorities confirmed by user — 2026-10-27
- Treat the MXD as the map-document definition and expect most spatial data to live in referenced File Geodatabases (`.gdb`), not inside the MXD. Preserve/resolve data-source paths and enumerate feature datasets/classes where possible; do not silently claim that importing an MXD imports its underlying geodatabase.
- Required reconstruction targets: Persian fonts and correct RTL/text shaping; label expressions and placement; full feasible symbology including point markers, line/fill symbols, colors, sizes, transparency, and custom symbol dependencies; map bookmarks (extent/scale); layer order/groups/visibility; coordinate systems; and layout elements (legend, title, north arrow, scale bar, map frames and print settings).
- The current browser-side MPK extraction is only a partial fallback and is not sufficient for this requirement. The next implementation design must include a Windows-side ArcMap 10.8/ArcObjects-compatible extraction/conversion worker (subject to licensed runtime availability) that exports a structured manifest plus data references/assets, and a WebGIS importer that restores those settings. Prefer copying/packaging the needed geodatabase data and font/symbol assets when licensing permits. Explicitly report unsupported renderers, fonts, labels or layout items instead of dropping them silently.


## GeoServer and PostGIS handoff pointer (2026-10-29)
- Read `docs/WEBGIS_STORAGE_SERVER_ACCESS.md` section 13 before changing GeoServer, the spatial databases, WFS, or WebGIS remote-data integration. It records the installed GeoServer 3.0.2 service, HTTPS reverse proxy, PostGIS store/layer names, the distinction between `sabzroy_gis`, `sabzroyan_personal_gis`, and `gis_staging`, the successful WFS read test returning zero features, and the existing master commit that prefilled the WFS URL.
- Key limitation: the one-time copy of `public.webgis_features` into the personal GIS database is not synchronization. WFS read success is not proof of WebGIS-to-PostGIS editing/persistence.
- No credentials or passwords belong in either handoff file. Check deployment status before assuming the master change is live.


## Organization project persistence verification (2026-10-29)
- Read `docs/WEBGIS_STORAGE_SERVER_ACCESS.md` before repeating storage diagnostics; the detailed record was added after operator-provided read-only live-server results on 2026-10-29.
- WebGIS organization project API uses the database configured in the private server file `/home/sabzroyanuraman/private/gis-auth-db.php`; the operator confirmed its database is `gis_staging` (no secrets were exposed).
- Operator query output: `gis_staging.public.gis_projects` has 49 rows and `gis_project_features` has 140 rows. Project 49 (`New Project`) was created/updated at `2026-10-29 09:13:31 +03` and has 5 linked feature rows. This likely corresponds to the five features reported saved by the UI, but exact identity and UI read-back are not yet verified.
- Do not confuse these tables with `public.webgis_features` in `sabzroy_gis` or the GeoServer personal-GIS datastore. Do not rerun old empty-table checks as a test of organization project saves.
- Remaining verification: open project 49 in the WebGIS UI and confirm all five geometries/attributes restore. No code or database changes were made as part of this diagnostic update; this commit updates documentation only.

## Editor tools audit and update (2026-10-29)
- Audited the Editor toolbar and GIS toolbox dispatch in `sabzroyanuraman-site/web-gis.html`. Existing editing tools include Move, Split, Merge, Explode Multipart, Rotate, Scale, Reshape, Modify Lasso, Curve, Edit Vertices, Advanced Snapping, Show Vertices, Flip Line Direction, Feature Templates, topology checking/repair, snapping, grid, and feature attachments. Presence in the menu alone does not prove browser-level correctness; these tools still need interactive regression tests.
- Added ArcMap-style **Copy / Paste Attributes** to the Editor toolbar and GIS toolbox (master commit `744a19ec0669557f1b86ee2b2cfd88e8edab72ed`). It copies properties from exactly one selected source feature, then pastes those fields onto one or more selected features in the active edit layer while preserving geometry and destination-only fields. It checks read-only shared maps, edit session, unsaved table edits, confirms bulk paste, records undo state, and requests geodatabase/project persistence.
- Verification performed: GitHub master read-back confirmed the new function, toolbar entry, toolbox entry, and dispatch each occur exactly once. No browser, CI, live-server, or end-to-end persistence test was run.
- Next Editor gaps to test against ArcMap/RustGIS: vertex snapping across target layers and in projected units, Cut/Copy/Paste selection ownership, undo/redo after multi-feature operations, split/reshape edge cases, attachment file size/persistence, and whether edit operations truly persist remotely (current Save Edits status distinguishes local IndexedDB from GeoServer/PostGIS publication).

