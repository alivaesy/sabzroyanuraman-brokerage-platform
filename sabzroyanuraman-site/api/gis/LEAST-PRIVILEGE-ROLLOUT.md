# GIS API least-privilege rollout (staging only)

## Package
- `sql/003_gis_api_least_privilege.sql`: revokes blanket table/sequence grants and grants column-level permissions to `gis_staging`; creates a separate non-superuser `gis_admin_api` login with restricted admin-only permissions.
- `tests/check-gis-db-grants.sh`: read-only checks for forbidden privileges plus PHP syntax checks.
- `gis-admin-db.php`: required private connection config for the admin endpoint; it is deliberately not included in the repository.

## Apply on staging
1. Back up the staging database and confirm you are connected to `gis_staging`.
2. Run the SQL as a PostgreSQL administrator: `sudo -u postgres psql -X -v ON_ERROR_STOP=1 -d gis_staging -f sabzroyanuraman-site/api/gis/sql/003_gis_api_least_privilege.sql`.
3. Set a password for the new role interactively, not in shell history: `sudo -u postgres psql -d postgres`, then run `\\password gis_admin_api`, then `\\q`.
4. Create `/home/sabzroyanuraman/private/gis-admin-db.php` with the same host/database but username `gis_admin_api`; use the password entered interactively. Owner `sabzroyanuraman:sabzroyanuraman`, mode `600`. Never paste the password into chat or commit it.
5. Run `bash sabzroyanuraman-site/api/gis/tests/check-gis-db-grants.sh`.
6. Test login, logout, `me`, project create/read/update/archive/delete, and all four admin actions in staging before enabling anything publicly.

## Important limitations
- This package does not deploy PHP files, create the private admin credential file, or activate the API.
- The grant test is a privilege-boundary check, not a complete endpoint integration test.
- The current `gis_staging` login must not be used for admin actions after this change.
- The audit table is append-only for both application roles. A PostgreSQL superuser can still alter it; use server/database audit logging for tamper evidence.
- Review custom schema ownership/default privileges before production rollout. Do not apply this to production without a backup and staging validation.
