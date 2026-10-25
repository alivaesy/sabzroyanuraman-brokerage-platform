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
