-- Least-privilege grants for the GIS account/project API.
-- Run as a PostgreSQL administrator while connected to gis_staging.
-- This script intentionally does not set passwords. Provision the admin role's
-- password out-of-band with: \password gis_admin_api
BEGIN;

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'gis_admin_api') THEN
    CREATE ROLE gis_admin_api LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
  END IF;
END
$$;

ALTER ROLE gis_admin_api NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
GRANT CONNECT ON DATABASE gis_staging TO gis_staging, gis_admin_api;
GRANT USAGE ON SCHEMA public TO gis_staging, gis_admin_api;

-- Remove earlier blanket grants before adding specific permissions.
REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA public FROM gis_staging, gis_admin_api;
REVOKE ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public FROM gis_staging, gis_admin_api;

-- Regular API: identity/session/login checks and organization-scoped projects.
GRANT SELECT (id, username, email, password_hash, display_name, status,
              is_platform_admin, last_login_at, updated_at)
  ON public.gis_users TO gis_staging;
GRANT UPDATE (last_login_at, updated_at) ON public.gis_users TO gis_staging;

GRANT SELECT (id, name, slug, status) ON public.gis_organizations TO gis_staging;
GRANT SELECT (organization_id, user_id, role, status)
  ON public.gis_organization_members TO gis_staging;
GRANT SELECT (id, organization_id, user_id, status, starts_at, expires_at)
  ON public.gis_subscriptions TO gis_staging;

GRANT SELECT (identity_hash, attempted_at, succeeded)
  ON public.gis_login_attempts TO gis_staging;
GRANT INSERT (identity_hash, succeeded) ON public.gis_login_attempts TO gis_staging;

GRANT SELECT (id, user_id, token_hash, expires_at, revoked_at)
  ON public.gis_sessions TO gis_staging;
GRANT INSERT (user_id, token_hash, expires_at, user_agent)
  ON public.gis_sessions TO gis_staging;
GRANT UPDATE (revoked_at) ON public.gis_sessions TO gis_staging;

GRANT SELECT (id, organization_id, created_by, name, description, project_data,
              status, created_at, updated_at)
  ON public.gis_projects TO gis_staging;
GRANT INSERT (organization_id, created_by, name, description, project_data)
  ON public.gis_projects TO gis_staging;
GRANT UPDATE (name, description, project_data, status, updated_at)
  ON public.gis_projects TO gis_staging;

GRANT SELECT (id, project_id, organization_id, name, layer_name, properties, geom)
  ON public.gis_project_features TO gis_staging;
GRANT INSERT (project_id, organization_id, created_by, name, layer_name, properties, geom)
  ON public.gis_project_features TO gis_staging;
GRANT DELETE ON public.gis_project_features TO gis_staging;

GRANT INSERT (actor_user_id, organization_id, action, target_type, target_id, details)
  ON public.gis_admin_audit_log TO gis_staging;

-- Admin API uses a distinct DB login; it cannot elevate users or rewrite audit history.
GRANT SELECT (id, status, username, email, display_name)
  ON public.gis_users TO gis_admin_api;
GRANT INSERT (username, email, password_hash, display_name, status)
  ON public.gis_users TO gis_admin_api;

GRANT INSERT (name, slug) ON public.gis_organizations TO gis_admin_api;
GRANT SELECT (id, name, slug, status) ON public.gis_organizations TO gis_admin_api;
GRANT INSERT (organization_id, user_id, role, status)
  ON public.gis_organization_members TO gis_admin_api;

GRANT INSERT (organization_id, user_id, plan_code, requested_days, status,
              starts_at, expires_at, max_users, activated_by, activation_note)
  ON public.gis_subscriptions TO gis_admin_api;
GRANT SELECT (id, organization_id, user_id, plan_code, requested_days, status,
              starts_at, expires_at, max_users)
  ON public.gis_subscriptions TO gis_admin_api;
GRANT UPDATE (status, starts_at, expires_at, activated_by, activation_note, updated_at)
  ON public.gis_subscriptions TO gis_admin_api;

GRANT INSERT (actor_user_id, organization_id, action, target_type, target_id, details)
  ON public.gis_admin_audit_log TO gis_admin_api;

-- Explicitly keep spatial reference metadata read-only.
GRANT SELECT ON public.spatial_ref_sys TO gis_staging, gis_admin_api;

-- Identity sequence access is limited to tables that each API inserts into.
GRANT USAGE, SELECT ON SEQUENCE
  public.gis_login_attempts_id_seq,
  public.gis_sessions_id_seq,
  public.gis_projects_id_seq,
  public.gis_project_features_id_seq,
  public.gis_admin_audit_log_id_seq
  TO gis_staging;
GRANT USAGE, SELECT ON SEQUENCE
  public.gis_users_id_seq,
  public.gis_organizations_id_seq,
  public.gis_subscriptions_id_seq,
  public.gis_admin_audit_log_id_seq
  TO gis_admin_api;

COMMIT;
