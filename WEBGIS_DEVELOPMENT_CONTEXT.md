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
