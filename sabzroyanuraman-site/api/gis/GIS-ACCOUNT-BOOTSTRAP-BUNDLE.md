# GIS account and organization provisioning

This repository keeps operational instructions and scripts in GitHub. Never create a public bootstrap URL or commit database credentials.

## Purpose
Provision two persistent, independent GIS test workspaces:
- Organization `kadaster_Sabtasnad`, slug `kadaster-sabtasnad`, owner account `kadaster_sabtasnad`.
- Organization `test`, slug `test`, owner account `test_gis`.

The provisioning script is idempotent: it preserves existing organization/user IDs and existing passwords, creates missing records, ensures owner membership, and creates a 10-year active subscription only when no active subscription exists. It does not run migrations or delete records. If an organization exists with a mismatched name, or an account exists but is inactive, it aborts the whole transaction for manual review.

## Prerequisites
1. Confirm migration `api/gis/sql/002_accounts_orgs_projects.sql` has already been applied to the intended database. Do **not** rerun it.
2. Keep `/home/sabzroyanuraman/private/gis-bootstrap-db.php` outside `public_html`. It must return an array with `host`, `port`, `database`, `username`, and `password`. Use a least-privilege DB role with only the required SELECT/INSERT/UPDATE rights on `gis_organizations`, `gis_users`, `gis_organization_members`, `gis_subscriptions`, `gis_admin_audit_log`, and the required identity-sequence usage. Never paste credentials into chat or GitHub.
3. Ensure PHP CLI has PDO_PGSQL enabled.

## Deploy and run on the server
After this change is merged into `master`, run this single grouped command on the server:

```bash
set -eu
REPO=/home/sabzroyanuraman/repositories/brokerage-platform
PRIVATE=/home/sabzroyanuraman/private
mkdir -p "$PRIVATE"
curl -fLsS --retry 3 \
  'https://raw.githubusercontent.com/alivaesy/sabzroyanuraman-brokerage-platform/master/scripts/provision-gis-test-organizations.php' \
  -o "$PRIVATE/provision-gis-test-organizations.php"
chmod 700 "$PRIVATE/provision-gis-test-organizations.php"
php -l "$PRIVATE/provision-gis-test-organizations.php"
php "$PRIVATE/provision-gis-test-organizations.php"
```

The script asks for each account password only if that account does not already exist. Password entry is hidden; use 12+ characters. Passwords are hashed with PHP `password_hash`, never displayed, and never stored in Git. If it reports that an account or organization needs manual review, stop and inspect before retrying.

## Verification
After successful provisioning, test each account separately in Web GIS:
1. Sign in as `kadaster_sabtasnad`, create and save a project.
2. Sign out; sign in as `test_gis`. The first organization's project must not appear, load, or be editable.
3. Attempting to request the first project's ID while scoped to the second organization must return 404; membership failure must return 403.
4. Sign out and back in to each account and verify its own project still loads.

Do not claim production tenant-isolation is verified until these live tests have actually passed. The GitHub connector can commit code and documentation but cannot run commands on the cPanel host or validate its database.
