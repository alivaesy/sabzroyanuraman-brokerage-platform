# GIS account and first-admin deployment bundle

This bundle combines the Web GIS account/workspace UI with a one-time CLI-only bootstrap for the initial platform administrator.

## Contents
- `sabzroyanuraman-site/web-gis.html`: GIS account login, organization selection, server project list/load/save integration.
- `scripts/bootstrap-gis-platform-admin.php`: creates the first platform admin, initial organization, owner membership, and active subscription atomically.
- Existing API endpoints and schema migration remain in `sabzroyanuraman-site/api/gis/`.

## Safe rollout
1. Back up the GIS database and web-gis.html.
2. Confirm migration `api/gis/sql/002_accounts_orgs_projects.sql` has been applied to the intended database.
3. Create `/home/sabzroyanuraman/private/gis-bootstrap-db.php` outside `public_html`, with a temporary database account limited to the needed inserts/select and sequence usage. Never put secrets in Git or HTML.
4. Deploy the bootstrap PHP file outside `public_html` (for example `/home/sabzroyanuraman/private/bootstrap-gis-platform-admin.php`) and run it interactively using PHP CLI. It refuses to run if a platform administrator already exists.
5. Remove the bootstrap script and temporary DB config after successful creation.
6. Deploy the updated `web-gis.html` and ensure the API files and private DB configs are present.
7. Test: login → select organization → draw a feature → save project → logout → login → reload project. Repeat using a second organization and verify that its projects are not visible or editable.
8. Only merge/enable production after PHP lint, JavaScript syntax checks, and staging tenant-isolation tests pass.

## Important
The GitHub connector cannot execute commands on the cPanel host or validate the production database. This bundle is prepared for review; it must not be treated as proof that the production end-to-end test passed. Do not create an unauthenticated public bootstrap URL, and do not use a database superuser for the application.
