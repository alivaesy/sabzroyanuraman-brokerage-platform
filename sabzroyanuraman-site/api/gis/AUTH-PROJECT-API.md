# GIS account and project API (staging only)

All endpoints are proposed for staging and are not deployed automatically by GitHub. They require a private file at `/home/sabzroyanuraman/private/gis-auth-db.php` returning host, port, database, username and password. Do not put credentials in the repository or paste them into chat.

## Endpoints
- `GET /api/gis/auth.php?action=csrf` — issue CSRF token before login.
- `POST /api/gis/auth.php?action=login` — JSON username/password; send `X-CSRF-Token` matching the `gis_csrf` cookie.
- `GET /api/gis/auth.php?action=me` — current account and active organization memberships.
- `DELETE /api/gis/auth.php?action=logout` — revoke current session; send CSRF header.
- `POST /api/gis/admin.php` — platform-admin actions: `create_user`, `create_organization`, `create_subscription`, `activate_subscription`.
- `GET /api/gis/projects.php?organization_id=ID` — list projects (membership + active subscription required).
- `GET /api/gis/projects.php?organization_id=ID&id=PROJECT_ID` — retrieve project and GeoJSON features.
- `POST /api/gis/projects.php` with `action=save` — create or replace project and features.
- `PATCH /api/gis/projects.php` with `action=archive` — archive project.
- `DELETE /api/gis/projects.php` — soft-delete project.

## Provisioning and security
1. Review and test migration `002_accounts_orgs_projects.sql` on a staging database first.
2. Configure a **dedicated least-privilege DB role** for these endpoints. Existing `sabzroyan_app` is read-only and must remain so until separately reviewed. Grant only required table and sequence privileges; never use PostgreSQL superuser credentials in PHP.
3. Bootstrap the first platform administrator out-of-band by an authorized DBA using a password hash generated locally with PHP `password_hash()`. Do not create a public bootstrap endpoint.
4. Configure private database credentials with restrictive filesystem permissions.
5. Apply same-origin HTTPS, Secure/HttpOnly/SameSite cookies, CSRF token checks, rate limits at the web server, and monitoring before enabling the endpoints.
6. Test that users from organization A cannot list, retrieve, modify or archive organization B projects; test expired subscriptions and suspended accounts.
7. The project save API currently replaces the submitted feature collection for a project in one transaction. Keep request size limits at the web server too.
8. Government SSO and payment processing are not included. Subscription activation is manual after payment verification outside this site.

Do not enable on production until syntax tests, staging migration, role grants and cross-organization authorization tests pass.


## Separate database credentials for privileged administration

The regular API connection uses `/home/sabzroyanuraman/private/gis-auth-db.php`. The platform-admin mutation endpoint uses a different connection file, `/home/sabzroyanuraman/private/gis-admin-db.php`, and must use a dedicated non-superuser PostgreSQL role. Do not copy the regular role's broad grants to the admin role.

Before enabling this endpoint, provision grants narrowly for only the columns and operations required by `admin.php`:
- read only the minimum user columns needed to validate an organization owner;
- insert users, organizations, organization memberships, subscriptions, and audit events;
- read the columns required by `INSERT/UPDATE ... RETURNING` and predicates;
- update only subscription activation columns; do not grant updates to `gis_users.is_platform_admin`;
- do not grant UPDATE/DELETE on `gis_admin_audit_log`, nor broad table-wide grants;
- grant sequence `USAGE` only for identity sequences used by these inserts.

Keep both configuration files outside the web root, owned by the site OS user, mode `600`. The admin config is intentionally a separate prerequisite; if absent, the admin endpoint returns a generic 503 rather than falling back to the regular DB role. Revoke existing broad grants on the staging role only after the replacement grants have been verified against staging integration tests.
