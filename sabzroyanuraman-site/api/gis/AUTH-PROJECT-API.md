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
