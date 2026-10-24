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
