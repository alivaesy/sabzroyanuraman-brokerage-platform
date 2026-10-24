# Web GIS PostGIS API

## 1. Private database configuration

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
    // Only explicitly listed tables/columns can be exposed by the read API.
    'layers' => [
        // Add real table/column names after inspecting your database:
        // [
        //     'id' => 'parcels',
        //     'title' => 'Parcels',
        //     'table' => 'parcels',
        //     'geometry_column' => 'geom',
        //     'properties' => [
        //         'id' => 'id',
        //         'name' => 'name',
        //     ],
        // ],
    ],
];
```

Replace the password placeholder directly in the server terminal/editor; never commit this file or paste the password into chat. Do not add a layer until its real table name, geometry column and property columns have been verified. Directory/file permissions must allow the cPanel PHP process to read the file while denying other users (normally directory `750`, file `640`, adjusted to the actual account/group).

## 2. Deploy endpoints

Upload these files from this repository to the matching paths on the server:

- `api/gis/health.php` → `/home/sabzroyanuraman/public_html/api/gis/health.php`
- `api/gis/layers.php` → `/home/sabzroyanuraman/public_html/api/gis/layers.php`
- `api/gis/features.php` → `/home/sabzroyanuraman/public_html/api/gis/features.php`

Health test:

`https://sabzroyanuraman.com/api/gis/health.php`

Expected response:

```json
{"ok":true,"service":"gis-api"}
```

Layer catalog:

`https://sabzroyanuraman.com/api/gis/layers.php`

It returns only layers explicitly configured in the private file. Until a real layer is added, an empty list is expected.

GeoJSON example (bbox is WGS84 longitude,latitude order):

`/api/gis/features.php?layer=parcels&bbox=46,32,48,34&limit=500`

The API requires a bbox and caps responses at 1,000 features. Configure only data intended to be readable by users of the public map.

## Security boundaries

- Keep PostgreSQL bound to localhost; do not open port 5432 publicly.
- Never place credentials in `public_html`, HTML/JavaScript, or Git.
- These endpoints are read-only. No arbitrary SQL or edits are accepted.
- Keep existing ticket, feedback, and tracking endpoints untouched.
- A successful health check does not prove the layer endpoints are deployed or that a table is configured.
