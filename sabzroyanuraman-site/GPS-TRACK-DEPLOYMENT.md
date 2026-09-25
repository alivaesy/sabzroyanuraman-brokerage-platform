# GPS Track API deployment notes

The consent-based browser tracker posts to `/api/location/track.php`.

## Deploy
1. Deploy this directory so the endpoint is publicly reachable at `https://sabzroyanuraman.com/api/location/track.php`.
2. Ensure PHP has PDO_SQLite enabled and the PHP/LSAPI user can create directories under the account home directory.
3. The endpoint creates its SQLite database under `~/private-data/gps-tracks/tracks.sqlite`, outside `public_html` for the standard cPanel layout. Directory mode is restricted to 0700 and database file to 0600.
4. Test from the actual HTTPS domain. Browser geolocation requires a secure context and user permission.

## Important
- The browser uploads points only after the visitor presses Start and grants browser location permission. Stop sends a close event and stops browser watch.
- Point uploads are throttled to at most one every 5 seconds per active page.
- This endpoint stores tracks but does not provide an admin listing/viewer or authenticated deletion/export. Those belong to a separate secured admin stage.
- Before public launch, publish an appropriate privacy notice, retention/deletion policy, and contact for data requests. This endpoint does not collect names, email addresses, or IP addresses in the track tables.
- Add rate limiting at the web server/WAF layer before promoting this feature publicly; server-side unauthenticated ingestion can otherwise be abused.
