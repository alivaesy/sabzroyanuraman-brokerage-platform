# Web GIS Development Context

Last reviewed: 2026-10-25
Primary file: `sabzroyanuraman-site/web-gis.html`
Source of truth: `master`

## Mandatory workflow for future sessions
1. Read the current `master` HEAD and this document before inspecting or changing GIS code.
2. Work directly on `master`. **Do not create branches or pull requests** unless the user explicitly changes this instruction.
3. Inspect existing code and commit history before reimplementing a feature. Prefer small, targeted edits over replacing large sections.
4. After each grouped change, inspect the diff, run available relevant checks/CI, and clearly distinguish static review from browser/live runtime testing.
5. Do not revisit validated areas without a reported regression or evidence of a defect.
6. Do not claim a tool is working merely because its name or handler exists; check event wiring, state changes, error paths, and end-to-end behavior.

## Confirmed project direction
- Goal: browser-based GIS with ArcMap 10.8-inspired workflows and tool organization.
- Web GIS libraries are intended to be local/self-hosted; do not reintroduce CDN dependencies without explicit approval.
- Authentication, project save/restore, drawing isolation between users, and attribute tables were reported by the user as correct on 2026-10-25. Treat these as validated at that point; do not modify them casually.
- Never put database credentials in browser code. PostGIS access must go through secured server-side APIs.
- Preserve existing data, layer state, symbology, and project behavior when extending tools.

## GIS online login accounts and provisioning (verified 2026-10-25)
- Account/organization database: PostgreSQL database `gis_staging` (this is the GIS auth/account database; do not confuse it with the separate PostGIS data database).
- Core tables: `gis_users`, `gis_organizations`, `gis_organization_members`, `gis_subscriptions`; related auth/session/login-attempt/project/feature/audit tables also exist.
- Schema migration: `sabzroyanuraman-site/api/gis/sql/002_accounts_orgs_projects.sql`.
- Private database configuration (never expose contents): `/home/sabzroyanuraman/private/gis-auth-db.php` and admin provisioning connection `/home/sabzroyanuraman/private/gis-admin-db.php`.
- Provisioning script in repository: `scripts/provision-gis-test-organizations.php`.
- Installed server copy: `/home/sabzroyanuraman/private/provision-gis-test-organizations.php`.
- Run on the server via CLI only: `php /home/sabzroyanuraman/private/provision-gis-test-organizations.php`. It prompts for passwords with terminal echo disabled; never pass passwords as command-line arguments or store them in chat.
- The script is idempotent and only provisions/reconciles these two predefined test organizations/accounts; it is **not a generic create-any-organization script**. It preserves existing account passwords and IDs, and refuses to reset inactive/conflicting records.
- Last verified account inventory (2026-10-25): 2 active owner accounts / organizations:
  - username `kadaster_sabtasnad` → organization `kadaster_Sabtasnad` (reported IDs: user 3, organization 4).
  - username `test_gis` → organization `test` (reported IDs: user 4, organization 5).
  - Active subscriptions were reported through 2036-10-25.
- These are the last known counts, not a live count. To answer “how many accounts are there now?”, query `gis_users` and memberships in the live `gis_staging` database; do not infer current count from this note.
- Creating a **third/new organization** requires a separate, parameterized CLI provisioning script (organization name/slug, username, securely prompted password, owner membership, subscription, and audit record). Do not repurpose the fixed two-account script without reviewing its transaction, conflict checks, and permissions.
- Never put credentials or password hashes in this document, Git, browser JavaScript, or public webroot.

## GIS development priorities
1. Stabilize existing tools and basemap behavior before adding more features.
2. Audit every visible tool end-to-end; identify menu items that are wired incorrectly, incomplete, or nonfunctional.
3. Improve ArcMap-like selection workflows: feature selection, rectangle/polygon selection, attribute/location selection, switch/clear selection, zoom to selection, and create layer from selected features.
4. Continue Definition Query, Field Calculator, spatial selection, and Layout View only after regression checks on existing tools.
5. Continue WFS/GeoServer interoperability and secure PostGIS-backed editing when relevant; test pagination, BBOX, errors, permissions, and concurrent-edit behavior.
6. Keep project save/restore regression tests for geometry, attributes, visibility, symbology, extent, and selection.

## Regression checklist
- [ ] Page loads with no uncaught JavaScript errors.
- [ ] Basemap loads and remains usable after tool interactions.
- [ ] Layer tree visibility/order and attribute table remain synchronized.
- [ ] Select, multi-select, clear/switch selection, and zoom-to-selection work on map and table.
- [ ] Editing and undo/cancel behavior do not silently discard data.
- [ ] Save, reload, and restore a project; verify geometries and attributes.
- [ ] Two separate users/projects do not see or overwrite each other's drawings.
- [ ] GIS sign-out actually ends the server session and clears the browser session.
- [ ] No external CDN dependency is introduced.
- [ ] Relevant CI checks pass; live browser/server checks are reported separately.

## Session handoff notes
Record only verified changes here after they land on `master`: date, commit SHA, files changed, tests actually run, results, and unresolved issues. Do not mark a check as passed unless it was run.
