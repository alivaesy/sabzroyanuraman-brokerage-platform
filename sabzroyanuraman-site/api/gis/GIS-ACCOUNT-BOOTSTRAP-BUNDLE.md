# GIS account and organization provisioning

Operational scripts and instructions are maintained in this GitHub repository. Never commit database credentials or expose bootstrap scripts through a public URL.

## Permanent test workspaces
- Organization: `kadaster_Sabtasnad`; slug: `kadaster-sabtasnad`; owner account: `kadaster_sabtasnad`.
- Organization: `test`; slug: `test`; owner account: `test_gis`.

The script is idempotent: it preserves existing organization/user IDs and passwords, creates missing records, ensures active owner membership, and adds a 10-year active subscription only when no active subscription exists. It does not run migrations or delete records. If a slug exists with a different name or an account is inactive, it aborts the transaction for manual review.

## Prerequisites
1. Migration `api/gis/sql/002_accounts_orgs_projects.sql` must already be applied to the intended database. Do **not** rerun it.
2. Keep `/home/sabzroyanuraman/private/gis-admin-db.php` outside `public_html`. It must return `host`, `port`, `database`, `username`, and `password`. Use a least-privilege database role with the necessary SELECT/INSERT/UPDATE rights on `gis_organizations`, `gis_users`, `gis_organization_members`, `gis_subscriptions`, `gis_admin_audit_log`, and required sequence usage. Never paste credentials into chat or GitHub.
3. PHP CLI must have PDO_PGSQL enabled.
4. The admin DB role must have the least-privilege grants defined in `api/gis/sql/003_gis_api_least_privilege.sql`. That SQL source includes the row-locking and membership-upsert permissions needed by the provisioning script. Do not rerun the migration blindly on a live database; review its REVOKE section and apply only the missing grants if the migration has already been used.

## Run on the server
Run this grouped command after pulling the latest `master` (or it downloads the committed script directly):

```bash
set -eu
PRIVATE=/home/sabzroyanuraman/private
mkdir -p "$PRIVATE"
curl -fLsS --retry 3 \
  'https://raw.githubusercontent.com/alivaesy/sabzroyanuraman-brokerage-platform/master/scripts/provision-gis-test-organizations.php' \
  -o "$PRIVATE/provision-gis-test-organizations.php"
chmod 700 "$PRIVATE/provision-gis-test-organizations.php"
php -l "$PRIVATE/provision-gis-test-organizations.php"
php "$PRIVATE/provision-gis-test-organizations.php"
```

Passwords are requested invisibly only for accounts that do not already exist, and must be at least 12 characters. Passwords are hashed and never printed or committed. Keep the CLI script outside `public_html`; it can be retained there for future idempotent runs.

## Verify isolation
1. Sign in as `kadaster_sabtasnad`, create and save a project.
2. Sign out and sign in as `test_gis`. The first organization's project must not appear, load, or be editable.
3. A project ID from the first organization requested under the second organization must return 404; missing membership must return 403.
4. Sign out/in and verify each account can reload its own project.

Do not claim production isolation is verified until these live tests pass. GitHub can store the code and instructions but cannot execute commands on the cPanel host or validate the production database.
