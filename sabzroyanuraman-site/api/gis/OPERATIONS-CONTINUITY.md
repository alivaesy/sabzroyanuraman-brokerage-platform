# WebGIS Operations Continuity

Last updated: 2026-10-25

## Current objective
Finish first GIS platform administrator setup and verify login/project persistence. Keep responses and operational steps concise; avoid repeating completed checks.

## Repository state
- Repository: `alivaesy/sabzroyanuraman-brokerage-platform`
- WebGIS entry: `sabzroyanuraman-site/web-gis.html`
- PR #39: https://github.com/alivaesy/sabzroyanuraman-brokerage-platform/pull/39
- Branch: `feat/gis-account-bootstrap-bundle`
- Head at last check: `af371fcf725ce5611a9088ddd8b89fa99fcf15cb`
- PR is open and Draft. Do not merge until staging end-to-end checks pass.

## Server findings (2026-10-25)
- Target used by existing GIS auth/admin configs is database `gis_staging`, host `127.0.0.1`.
- `gis_staging` contains 9 GIS tables: `gis_admin_audit_log`, `gis_login_attempts`, `gis_organization_members`, `gis_organizations`, `gis_project_features`, `gis_projects`, `gis_sessions`, `gis_subscriptions`, `gis_users`.
- PostGIS 3.4.2 is enabled; required bootstrap columns exist.
- `gis_users` currently has 0 rows with `is_platform_admin=true`.
- Existing table owners are `postgres`. Login roles found: `gis_admin_api`, `gis_staging`, `sabzroyan_app`, `sabzroyan_auth`; no role memberships were found.
- PHP CLI is 8.2.34 and `PDO`, `pdo_pgsql`, `pgsql` are available.
- Private directory: `/home/sabzroyanuraman/private` (mode 700).
- Existing configs: `gis-auth-db.php` uses DB role `gis_staging`; `gis-admin-db.php` uses `gis_admin_api`. Never display or commit credentials.
- Bootstrap config `/home/sabzroyanuraman/private/gis-bootstrap-db.php` is currently missing.
- Verified backup created: `/var/backups/sabzroyan-gis/gis_staging_before_bootstrap_2026-10-25_15-12-50.dump` (53 KB; `pg_restore -l` succeeded). Existing older backups were for `sabzroyan_gis`, not staging.

## Immediate next action
Create the initial administrator in `gis_staging` using PR #39's one-time CLI script `scripts/bootstrap-gis-platform-admin.php`. Do not rerun migration `sabzroyanuraman-site/api/gis/sql/002_accounts_orgs_projects.sql`; schema already exists. Do not use `postgres` credentials in a web config or expose the bootstrap script under `public_html`.

Before execution, choose a safe temporary DB access method. Bootstrap needs SELECT on `gis_users`, INSERT on `gis_users`, `gis_organizations`, `gis_organization_members`, `gis_subscriptions`, `gis_admin_audit_log`, plus required sequence privileges. Prefer a dedicated temporary login with only these grants; revoke/drop it and remove the private config/script after successful bootstrap. Never ask the user to paste passwords.

## Required verification after bootstrap
1. Confirm exactly one platform admin and its organization/member/subscription records exist.
2. Test browser login, create/draw, save project, logout, login, reload project.
3. Test organization isolation using a second organization/account.
4. Only then consider marking PR #39 ready/merging.

## Working style
Keep user-facing replies short and action-oriented. Group related shell commands into one block. Track completed checks here instead of asking the user to repeat them.
