# WebGIS / RustGIS / WASM Deployment Handoff
Last updated: 2026-10-31
Repository: `alivaesy/sabzroyanuraman-brokerage-platform`
Default deployment branch: `master`
Primary WebGIS file: `sabzroyanuraman-site/web-gis.html`

This note is the continuation handoff for another ChatGPT account/conversation. Read it before changing deployment, RustGIS, WASM, or WebGIS code. Keep future updates in this file and in `WEBGIS_DEVELOPMENT_CONTEXT.md`.

## 1. Live website deployment on cPanel

### Host paths
- Repository clone: `/home/sabzroyanuraman/repositories/brokerage-platform`
- Website source in clone: `/home/sabzroyanuraman/repositories/brokerage-platform/sabzroyanuraman-site`
- Public webroot: `/home/sabzroyanuraman/public_html`
- Active deploy script: `/home/sabzroyanuraman/bin/deploy-site.sh`
- Deployment log: `/home/sabzroyanuraman/deploy.log`

### Cron
The existing cPanel Cron schedule was deliberately left unchanged:
```cron
* * * * * /home/sabzroyanuraman/bin/deploy-site.sh >> /home/sabzroyanuraman/deploy.log 2>&1
```
It continues to run every minute. Do not change the schedule unless the user explicitly asks.

### Current safe deploy behavior
The active script was replaced on 2026-10-31 and its owner/mode were corrected:
- Owner: `sabzroyanuraman:sabzroyanuraman`
- Mode: `700`
- It fetches `origin master`, then resets the server clone to `origin/master`.
- It rsyncs the repository website source into `public_html`.
- It uses `--no-owner --no-group --no-perms`; it does not run recursive `chown`.
- It does not use `--delete`. Never add `--delete`.
- It excludes these host-managed paths from deployment:
  - `/payment/***` — contains the live payment gateway PHP/configuration and related files.
  - `/api/gis/staging-auth/***` — host-only staging authentication files.
  - `/.htaccess` — host root security configuration.
  - `/api/.htaccess` — host API configuration.
  - `.git`

Current script:
```bash
#!/bin/bash
set -euo pipefail

REPO="/home/sabzroyanuraman/repositories/brokerage-platform"
SITE="$REPO/sabzroyanuraman-site"
PUBLIC="/home/sabzroyanuraman/public_html"

cd "$REPO"

echo "===== FETCH GITHUB MASTER ====="
git fetch origin master
git reset --hard origin/master

echo "===== SAFE WEBSITE DEPLOY ====="
rsync -av \
  --no-owner \
  --no-group \
  --no-perms \
  --exclude='.git' \
  --exclude='/payment/***' \
  --exclude='/api/gis/staging-auth/***' \
  --exclude='/.htaccess' \
  --exclude='/api/.htaccess' \
  "$SITE/" "$PUBLIC/"

echo "===== DEPLOY SUCCESS ====="
git log -1 --oneline
```

### Verified deployment outcome
On 2026-10-31:
- The active script passed `bash -n`.
- The checksum dry-run with the same exclusions produced no changed-file entries.
- The Cron log showed repeated `DEPLOY SUCCESS` at commit `c4906ac`.
- An earlier `Permission denied` occurred during the ownership/mode transition; after correcting owner and mode, subsequent Cron executions succeeded.
- An earlier legacy-script `chown: Operation not permitted` error is historical log output; the active script no longer contains `chown`.

Important deployment constraints:
1. Never overwrite or delete the host payment directory.
2. Never overwrite `api/gis/staging-auth`.
3. Keep root and API `.htaccess` excluded unless the user explicitly approves changing host configuration.
4. Never use `rsync --delete`.
5. Do not reintroduce recursive `chown`.
6. The script runs `git reset --hard origin/master`; therefore do not leave valuable uncommitted work in the server clone. Commit/push work to GitHub first.
7. When adding a new host-only configuration directory/file, inspect it and add a narrowly scoped rsync exclusion before deployment.

## 2. RustGIS / Rust / WASM status

### Design direction
- Keep the existing Leaflet-based WebGIS and GeoServer/PostGIS architecture.
- Use `opengeos/RustGIS` as a source of Rust geospatial algorithms and reference capabilities, not as a replacement for Leaflet, GeoServer, or PostGIS.
- RustGIS/WASM is a browser-side processing engine/adapter. Server-side spatial storage and publishing remain separate responsibilities.
- Do not describe a build or CI success as proof that the WASM engine is deployed and working on the live site.

### Work reported as implemented before this handoff
The recent Phase 2 work was reported to include:
- A Rust/WASM adapter and several RustGIS vector-processing operations (reported as four operations).
- Raster processing integration and GeoTIFF output path.
- Local IndexedDB-backed result/layer handling.
- An initial GeoLibre WASI integration.
- Rust tests and GitHub Actions workflow coverage.

These are implementation/CI claims from the recent development history; re-open the current branch/files and verify before relying on exact operation names or declaring end-to-end functionality. PNG is not a substitute for a georeferenced raster output: preserve actual GeoTIFF data and CRS/geotransform metadata.

### Latest known Phase 3 branch / PR state
- Working branch: `feat/rustgis-vector-geoprocessing-phase-3`.
- Pull request: **#44**, still Draft at the last reported check.
- A previous publish workflow skipped generated/package files because they were not tracked; a forced `git add -f` correction was committed as `a9b722c`.
- Both Rust/WASM builds and tests were reported successful in that development pass.
- The workflow identified in the prior handoff is **RustGIS Cron Publish**. It needed to be rerun after the tracking fix.
- Expected generated/package paths mentioned in that work: `wasm/rust-raster/`, `rustgis-vector-wasm-pkg/`, and the JavaScript bridge file(s). Inspect the branch to confirm exact paths and names.
- Critical status: PR #44 had not been merged into `master`, and the Phase 3 WASM assets had not been confirmed deployed to the live host. The website Cron above fetches only `master`; it will not publish unmerged branch content.

### Required next actions for RustGIS/WASM
1. Inspect the latest branch head and PR #44 before modifying anything.
2. Inspect the `RustGIS Cron Publish` workflow definition and its latest run. Confirm it packages and publishes the intended WASM outputs, rather than merely compiling them.
3. Rerun the workflow after the `a9b722c` tracking correction if it has not already completed successfully.
4. Verify generated WASM/package/bridge files are committed and present in the branch.
5. Review PR #44 changes and tests. Do not merge automatically; follow the user's established approval process.
6. After merge to `master`, verify the website deploy script's rsync source includes the WASM output paths and that they are not caught by any exclusions.
7. Verify live HTTP access to the deployed WASM and bridge files, correct MIME types, browser loading, and a real processing operation.
8. Keep build, unit-test, package-publish, host-deploy, and browser-E2E results distinct in reports.

## 3. Remaining Phase 2 integration work
The following were explicitly outstanding in the latest recorded status:
- Connect Plugin Manager to the secure execution environment and RustGIS permission model.
- Add GeoLibre outputs as actual WebGIS layers, not only downloadable files.
- Move raster assets from browser-only IndexedDB storage to server-side storage so they are available across browsers/devices.
- Deploy WASM artifacts to the host and perform real browser/E2E testing.
- Verify GeoTIFF output, projection/CRS, geotransform, nodata, and layer re-opening after reload.
- Test processing errors, large inputs, memory limits, cancellation, and permission boundaries.

## 4. WebGIS continuation priorities
Main file: `sabzroyanuraman-site/web-gis.html`.

Continue toward an ArcMap 10.8-inspired GIS workflow. Do not replace working tools with decorative controls. Audit each visible tool against its actual handler and test it.
Recommended order:
1. Verify the Rust/WASM branch, workflow, generated artifacts, and PR #44 state.
2. Complete WASM deployment and live browser execution.
3. Connect processing outputs directly to the WebGIS layer tree and map.
4. Complete server-side raster asset storage and retrieval.
5. Connect Plugin Manager with sandbox/permission checks.
6. Run regression tests for Vector, Raster, ModelBuilder, editing, layer styling, project persistence, and GIS account isolation.

## 5. Safety and reporting rules for future sessions
- Read this file and `WEBGIS_DEVELOPMENT_CONTEXT.md` first.
- Inspect current GitHub branch/commit/workflow/PR state; this handoff is a snapshot dated 2026-10-31, not a live status feed.
- Do not claim a feature is complete based only on a menu entry, source-code presence, or successful compilation.
- Report separately: static checks, Rust tests, WASM build, GitHub Actions, artifact publication, host deployment, and browser end-to-end testing.
- Preserve host payment/auth files and the deployment exclusions above.
- Keep responses concise and execute grouped, targeted work where safe.
