#!/usr/bin/env bash
set -euo pipefail
DB="${GIS_TEST_DB:-gis_staging}"
PSQL=(sudo -u postgres psql -X -v ON_ERROR_STOP=1 -d "$DB")
# This suite verifies the security boundary without creating or modifying data.
"${PSQL[@]}" <<'SQL'
DO $$
BEGIN
  IF NOT has_database_privilege('gis_staging','gis_staging','CONNECT') THEN RAISE EXCEPTION 'regular role cannot connect'; END IF;
  IF NOT has_table_privilege('gis_staging','public.gis_projects','INSERT') THEN RAISE EXCEPTION 'regular role cannot create projects'; END IF;
  IF NOT has_table_privilege('gis_staging','public.gis_projects','SELECT') THEN RAISE EXCEPTION 'regular role cannot read projects'; END IF;
  IF NOT has_column_privilege('gis_staging','public.gis_sessions','user_id','SELECT') THEN RAISE EXCEPTION 'regular role cannot scope session revocation'; END IF;
  IF NOT has_column_privilege('gis_staging','public.gis_projects','project_data','UPDATE') THEN RAISE EXCEPTION 'regular role cannot update project data'; END IF;
  IF NOT has_column_privilege('gis_staging','public.gis_project_features','geom','INSERT') THEN RAISE EXCEPTION 'regular role cannot save geometries'; END IF;
  IF NOT has_column_privilege('gis_admin_api','public.gis_users','password_hash','INSERT') THEN RAISE EXCEPTION 'admin role cannot create user credentials'; END IF;
  IF NOT has_column_privilege('gis_admin_api','public.gis_subscriptions','status','UPDATE') THEN RAISE EXCEPTION 'admin role cannot activate subscriptions'; END IF;
  IF (SELECT rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication FROM pg_roles WHERE rolname='gis_staging') THEN RAISE EXCEPTION 'regular role has elevated role attributes'; END IF;
  IF (SELECT rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication FROM pg_roles WHERE rolname='gis_admin_api') THEN RAISE EXCEPTION 'admin API role has elevated role attributes'; END IF;
  IF has_table_privilege('gis_staging','public.gis_users','DELETE') THEN RAISE EXCEPTION 'regular role can delete users'; END IF;
  IF has_column_privilege('gis_staging','public.gis_users','is_platform_admin','UPDATE') THEN RAISE EXCEPTION 'regular role can elevate users'; END IF;
  IF has_table_privilege('gis_staging','public.gis_subscriptions','UPDATE') THEN RAISE EXCEPTION 'regular role has table-wide subscription update'; END IF;
  IF has_column_privilege('gis_staging','public.gis_subscriptions','status','UPDATE') THEN RAISE EXCEPTION 'regular role can activate subscriptions'; END IF;
  IF has_table_privilege('gis_staging','public.gis_admin_audit_log','UPDATE') OR has_table_privilege('gis_staging','public.gis_admin_audit_log','DELETE') THEN RAISE EXCEPTION 'regular role can rewrite audit log'; END IF;
  IF has_table_privilege('gis_admin_api','public.gis_subscriptions','UPDATE') THEN RAISE EXCEPTION 'admin role has table-wide subscription update'; END IF;
  IF has_column_privilege('gis_admin_api','public.gis_users','is_platform_admin','UPDATE') THEN RAISE EXCEPTION 'admin role can elevate users'; END IF;
  IF has_table_privilege('gis_admin_api','public.gis_admin_audit_log','UPDATE') OR has_table_privilege('gis_admin_api','public.gis_admin_audit_log','DELETE') THEN RAISE EXCEPTION 'admin role can rewrite audit log'; END IF;
  IF has_table_privilege('gis_admin_api','public.gis_projects','INSERT') THEN RAISE EXCEPTION 'admin role has unrelated project access'; END IF;
END $$;
SELECT 'GIS least-privilege checks passed' AS result;
SQL
# PHP syntax checks only; integration behavior still needs endpoint tests.
php -l sabzroyanuraman-site/api/gis/auth-common.php
php -l sabzroyanuraman-site/api/gis/auth.php
php -l sabzroyanuraman-site/api/gis/projects.php
php -l sabzroyanuraman-site/api/gis/admin.php
