# Web GIS PostGIS API

## 1. Create an empty test layer

Run the SQL migration on the server database (it creates an empty table and does not insert demo records):

`api/gis/sql/001_create_webgis_features.sql`

The table is `public.webgis_features`, geometry column `geom`, SRID 4326. The migration grants the app role read-only access to the table and its ID sequence.

## 2. Private database configuration

Create this file **outside** `public_html` and outside Git:

`/home/sabzroyanuraman/private/gis-db.php`

```php
<?php
return [
    'host' => '127.0.0.1',
    'port' => 5432,
    'database' => 'sabzroyan_gis',
    'username' => 'sabzroyan_app',
    'password' => 'PUT_THE_DATABASE_PASSWORD_HERE',
    'layers' => [
        [
            'id' => 'webgis_features',
            'title' => 'Web GIS test layer',
            'table' => 'webgis_features',
            'geometry_column' => 'geom',
            'properties' => [
                'id' => 'id',
                'name' => 'name',
                'category' => 'category',
                'created_at' => 'created_at',
            ],
        ],
    ],
];
```

Replace the password placeholder directly in the server terminal/editor; never commit this file or paste the password into chat. Keep this file outside the document root. Permissions must let the cPanel PHP process read it while denying other users (normally directory `750`, file `640`, adjusted to the actual account/group).

## 3. Deploy endpoints

Upload these repository files to matching server paths:

- `api/gis/health.php` → `/home/sabzroyanuraman/public_html/api/gis/health.php`
- `api/gis/layers.php` → `/home/sabzroyanuraman/public_html/api/gis/layers.php`
- `api/gis/features.php` → `/home/sabzroyanuraman/public_html/api/gis/features.php`

Test:

- Health: `https://sabzroyanuraman.com/api/gis/health.php`
- Layer catalog: `https://sabzroyanuraman.com/api/gis/layers.php`
- Empty GeoJSON test: `https://sabzroyanuraman.com/api/gis/features.php?layer=webgis_features&bbox=44,25,64,40&limit=100`

Expected health response:

```json
{"ok":true,"service":"gis-api"}
```

The feature endpoint returns an empty FeatureCollection until records are imported. This is expected; do not insert fake business data merely to test the API.

## Security boundaries

- Keep PostgreSQL bound to localhost; do not open port 5432 publicly.
- Never place credentials in `public_html`, HTML/JavaScript, or Git.
- These endpoints are read-only. No arbitrary SQL or edits are accepted.
- Only configured tables/columns are exposed, and GeoJSON queries require a WGS84 bounding box with a maximum of 1,000 returned features.
- Keep existing ticket, feedback, and tracking endpoints untouched.
- GitHub changes do not automatically install files on the server unless a deployment workflow is configured.


## 4. Persistent GeoServer diagnostic record (2026-10-29)

Use this section as the handoff record before repeating diagnostics. It records verified results from the live server; it is not a claim that the full GIS persistence workflow is complete.

### Verified live checks

- `GET /api/gis/health.php` → HTTP 200.
- `GET /api/gis/projects.php` without a session → HTTP 401. Expected for an authenticated endpoint; this does not test project save/load.
- `GET /api/gis/features.php` without required query parameters → HTTP 400. Expected validation behavior.
- WFS 2.0.0 `GetCapabilities` → successful; published feature types:
  - `ne:boundary_lines`
  - `ne:coastlines`
  - `ne:countries`
  - `ne:disputed_areas`
  - `ne:populated_places`
  - `ne:webgis_features`
- WFS 2.0.0 `GetFeature` for `ne:webgis_features`, `count=1` → HTTP 200, `numberMatched=0`, `numberReturned=0`.
- WMS 1.1.1 `GetMap` for `ne:webgis_features`, bbox `44,25,64,40`, 800x600 PNG → HTTP 200, valid PNG (9,688 bytes). Since the feature layer has no records, this does not prove that user-drawn features render.
- WMS 1.1.1 `GetMap` for `ne:countries` with the same bbox/size → HTTP 200, valid PNG (33,007 bytes).
- WFS capabilities and WMS GetMap are public/readable through HTTPS. GeoServer REST `/geoserver/rest/layers.json` returned HTTP 401 without credentials; this is not by itself a fault and no credentials should be put in shell history or chat.
- API layer catalog `/api/gis/layers.php` → HTTP 200; advertises `webgis_features`, geometry type `GEOMETRY`, SRID 4326.
- At the time of checking, `public.webgis_features` contained 0 records in both `sabzroyan_personal_gis` and `sabzroyan_gis`.

### Current known gaps

1. GeoServer WMS/WFS service response works, but the published personal feature layer is empty.
2. The location of the previously drawn/saved shapes has not yet been identified. Do not assume the shapes are lost and do not insert test/demo records.
3. REST layer listing requires authentication; use an authenticated, read-only request if the exact published layer/store configuration needs verification.
4. WFS-T write/edit behavior has not been tested and must remain disabled for anonymous users.
5. The live connection from the WebGIS drawing workflow to the intended PostGIS table has not been end-to-end verified.

### Next diagnostic sequence (read-only)

1. Identify geometry tables in both databases. On this server's PostGIS version, use the actual `geometry_columns` view column names:
   `f_table_schema, f_table_name, f_geometry_column, coord_dimension, srid, type`
   (not `table_schema`, `table_name`, `column_name`, `udt_name`).
2. If that view returns no useful rows, inspect ordinary table names/columns and counts in each database without selecting sensitive attribute values.
3. Locate the previous shapes before changing GeoServer layer/store configuration.
4. After locating the correct table, verify the GeoServer store's database/schema/table/geometry-column and only then test WMS/WFS against that layer.
5. Keep writes behind the authenticated application API. Do not enable anonymous WFS-T, expose database credentials, open PostgreSQL publicly, or insert fabricated records for testing.

### Avoid repeating completed checks

Do not rerun WMS GetMap for `ne:countries`, WFS GetCapabilities, or the same empty `ne:webgis_features` GetFeature request unless configuration changes or new evidence justifies a retest. Start from the next diagnostic sequence above and update this record with new verified findings.


### Follow-up result (2026-10-29)

- Queried `geometry_columns` correctly in both `sabzroyan_personal_gis` and `sabzroyan_gis`.
- Each database exposes exactly one geometry table: `public.webgis_features`, geometry column `geom`, SRID 4326, generic type `GEOMETRY`.
- No other registered geometry table was found in either database. Both `webgis_features` tables were previously counted as 0 rows.
- Next: inventory all ordinary tables and row counts in both databases (not just geometry tables). This can reveal whether saved data lives in a non-geometry project table or another named table. Do not alter schema/data until this inventory is reviewed.


### Follow-up result: complete public-table count inventory (2026-10-29)

- Corrected the inventory command after the initial `\\gexec` syntax error; no database changes were made.
- In both `sabzroyan_personal_gis` and `sabzroyan_gis`, the only public tables returned were:
  - `public.spatial_ref_sys`: 8,500 rows (PostGIS spatial reference system metadata, not user features).
  - `public.webgis_features`: 0 rows.
- Therefore the previously saved shapes are not currently present in either of these two tables. This does **not** prove they are lost: other databases, non-public schemas, project-storage tables, or browser IndexedDB may be involved.
- Next read-only step: list database names, schemas, and tables across all non-template databases; inspect only names/metadata first, not sensitive attribute values. Do not create/import sample records or modify GeoServer until the actual storage location is identified.


### Follow-up result: staging project data located (2026-10-29)

- Read-only inventory across all connectable non-template databases found the project tables in both `gis_staging` and `gis_auth_staging`.
- Counts in `gis_staging`: `public.gis_projects` = 48 rows; `public.gis_project_features` = 135 rows.
- Counts in `gis_auth_staging`: both tables = 0 rows.
- In both databases, `public.gis_project_features.geom` is registered in `geometry_columns` as SRID 4326, type `GEOMETRY`.
- This is the first positive evidence of stored project/feature records and is a strong candidate for the user's previously saved work. Do not call it production-ready or change application connections yet: verify feature distribution and the WebGIS project's configured database first.
- Next read-only checks: aggregate feature counts by project and geometry type in `gis_staging`; inspect application config using commands that reveal only the database name/host, never passwords or full secret files. Do not copy staging records into production or expose personal/project attributes.


### Follow-up result: staging feature integrity check (2026-10-29)

- Read-only aggregate check on `gis_staging.public.gis_project_features`: 135 features, associated with 43 distinct project IDs, 0 NULL geometries, and 0 geometries failing `ST_IsValid`.
- This confirms that the staging feature table contains geometrically valid records; it does not alone establish that these are the exact shapes the user remembers or that this staging database is the active production target.
- `gis_auth_staging` has the same project/feature tables but both are empty. The two `sabzroyan_*_gis` databases' `webgis_features` tables are also empty.
- Code review: `api/gis/projects.php` reads and writes `public.gis_projects` and `public.gis_project_features` using `gis_auth_db()`, while `api/gis/features.php` uses the separate private config `/home/sabzroyanuraman/private/gis-db.php` and is read-only. These are distinct data paths.
- Next: safely identify only the database name used by `gis_auth_db()` and the `database` field in `gis-db.php` (do not print credentials or full config files). Do not repoint either path until the intended active database and ownership of the 135 staging features are confirmed.


### Follow-up result: API database targets confirmed (2026-10-29)

- Sanitized server-side config inspection confirms the authenticated projects API (`gis-auth-db.php`) targets database `gis_staging`.
- The separate read-only feature catalog/query API (`gis-db.php`) targets `sabzroyan_gis`.
- This explains the observed split: the authenticated project API has 48 project rows and 135 geometrically valid features in `gis_staging`, while `sabzroyan_gis.public.webgis_features` is empty.
- Do not repoint either config yet. First determine whether `gis_staging` is intentionally a staging/test database and whether the 48 projects/135 features are user-owned data that must be retained. The correct next engineering step is to audit deployment/runtime status and the WebGIS save/load calls, then plan a backed-up, authenticated migration/connection change only after explicit confirmation of the intended production target.


### WebGIS front-end save/load code audit (2026-10-29)

- In `web-gis.html`, `saveProject()` persists to the server only when both `remoteGisUser` and `remoteGisOrganization` are set; otherwise it writes the project to browser IndexedDB and explicitly reports that it is local-only.
- The online account flow calls the authenticated same-origin `api/gis/projects.php` endpoint. That endpoint uses `gis_auth_db()` and the confirmed `gis_staging` database. Server project save stores the full serialized map in `gis_projects.project_data` and normalized feature geometries in `gis_project_features`.
- Edit-session `Save Edits` is a separate local IndexedDB path and explicitly does not publish changes to GeoServer/PostGIS. Do not confuse it with saving the whole project through the online account workflow.
- The read-only `api/gis/features.php` endpoint uses `gis-db.php` → `sabzroyan_gis` → `public.webgis_features`; it is not the source used to load authenticated server projects. This mismatch is why the generic feature endpoint can be empty while server projects/features exist in staging.
- User confirmed the 135 staging features are disposable test shapes. They do not need preservation as business data. Nevertheless, do not silently erase the 48 project rows or change the active connection until deciding the target architecture.
- Recommended implementation direction: keep project save/load authenticated through `projects.php`; avoid exposing organization project features via the public read-only endpoint. If the generic feature endpoint is needed for published/shared layers, give it a separate explicitly public/published-only data source or require authorization. Consider changing UI wording so users can distinguish local project save, local edit save, and authenticated server project save.


## Storage architecture decision: multiple independent WebGIS routes (2026-10-29)

The WebGIS must support multiple storage scopes, with at least two first-class routes and room for future providers:

1. **Organization workspace**: authenticated organization membership, role checks, active subscription checks, and organization-scoped project access. Current implementation: `api/gis/projects.php` via `gis-auth-db.php` → `gis_staging`; it stores `project_data` plus normalized feature rows.
2. **Personal workspace**: authenticated individual ownership, independent from organization membership/subscription. It must not require an `organization_id` and must not be able to read or modify organization projects. Target database: `sabzroyan_personal_gis` unless deployment review determines another production target. This route is not implemented yet; the database currently only has the empty `webgis_features` table.
3. **Future providers**: keep the front-end provider choice separate from editing tools and map state. Add providers through a small registry/adapter interface (list/open/save/delete/capabilities) rather than branching throughout drawing/editing code.

### Required implementation sequence

- Add an explicit storage-scope selector: Personal / Organization. Do not silently fall back from a failed server save to IndexedDB; clearly label local-only save as a separate option.
- Keep the organization endpoint and its role/subscription checks intact.
- Implement personal project endpoints with session authentication, CSRF protection, owner-scoped SQL predicates, payload/geometry validation, limits, and audit logging. Personal endpoints must never accept an organization ID as an ownership substitute.
- Create a versioned SQL migration for personal project/feature tables in `sabzroyan_personal_gis`; do not reuse `webgis_features` for private project snapshots.
- Separate the read-only published-feature catalog from private project storage. No anonymous WFS-T and no browser-to-database credentials.
- Verify save → reload → edit → save → reload for both scopes, and test cross-user/cross-organization access denial before declaring complete.

### Current constraints / status

- Organization storage exists in code and currently points at `gis_staging`; this is a staging database, not yet confirmed as production.
- Personal project storage is a design target, not yet implemented. Do not imply that the empty `sabzroyan_personal_gis` database already stores personal projects.
- Existing IndexedDB remains browser-local and is not a third server scope; label it explicitly as local backup/workspace.
- Do not migrate or delete existing staging data as part of this design step. The 135 staging features were confirmed by the user to be disposable test shapes.
