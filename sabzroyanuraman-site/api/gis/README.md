# Web GIS PostGIS API

## Read-only health endpoint

`health.php` checks that PHP can connect to PostGIS. It does not expose database names, versions, or connection errors, and accepts only GET requests.

### Server configuration (never commit credentials)

Create this file **outside** `public_html` and outside the Git repository:

`/home/sabzroyanuraman/private/gis-db.php`

Contents:

```php
<?php
return [
    'host' => '127.0.0.1',
    'port' => 5432,
    'database' => 'sabzroyan_gis',
    'username' => 'sabzroyan_app',
    'password' => 'PUT_THE_DATABASE_PASSWORD_HERE',
];
```

Replace the placeholder directly in the server terminal/editor; never commit the file or paste the password into chat. Set directory/file permissions so the site's PHP process can read the file, but other users cannot (normally directory `750`, file `640`, adjusted to the actual cPanel account/group).

Upload `api/gis/health.php` to:

`/home/sabzroyanuraman/public_html/api/gis/health.php`

Then request:

`https://sabzroyanuraman.com/api/gis/health.php`

Expected success response:

```json
{"ok":true,"service":"gis-api"}
```

A `503` means the private config is missing/unreadable or the database connection failed. Details are intentionally kept in the server PHP error log.

## Security boundary

- Keep PostgreSQL bound to localhost; do not open port 5432 publicly.
- Do not put credentials in `public_html`, HTML/JavaScript, or Git.
- This endpoint is only a connectivity check. It is not a layer query or editing API.
- Keep existing ticket, feedback, and tracking endpoints untouched.
