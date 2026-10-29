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
