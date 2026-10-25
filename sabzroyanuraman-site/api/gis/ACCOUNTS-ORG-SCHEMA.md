# GIS accounts, subscriptions and tenant project schema

Migration: `sql/002_accounts_orgs_projects.sql`

## Scope
Creates the database structure for:
- local GIS users (store only a PHP `password_hash()` result in `password_hash`)
- organizations and memberships/roles
- manually activated user- or organization-level subscriptions
- hashed opaque session tokens
- organization-owned projects and PostGIS features
- audit records for administrative actions

This is schema only. It inserts no accounts or business data and does not modify `webgis_features`.

## Important safety boundary
- The migration intentionally grants **no** new privileges to `sabzroyan_app`.
- Do not expose signup/admin activation/project write endpoints until server-side authentication, CSRF protection, rate limiting, authorization checks, and tenant isolation are implemented and tested.
- The API must derive user and organization IDs from the authenticated server-side session, never trust IDs supplied by the browser.
- Enforce active subscription dates and membership/role on every request.
- Session cookies should be Secure, HttpOnly and SameSite; rotate session IDs on login and revoke sessions on logout/password change.
- Never store plaintext passwords or raw session tokens. Use `password_hash()` / `password_verify()`; store only a cryptographic hash of a randomly generated session token.
- Account creation and subscription activation are intended to be administrator-controlled after payment is verified outside the site. No payment gateway integration is included.
- Government SSO is not implemented by this migration; add only after obtaining official integration documentation and credentials.

## Deployment plan
1. Review the migration in version control.
2. Back up `sabzroyan_gis`.
3. Apply the SQL as a database administrator only after explicit approval.
4. Verify table definitions and constraints.
5. Implement and test the PHP auth/admin/project APIs.
6. Grant only the exact table/sequence permissions required by the reviewed APIs; do not grant superuser or broad schema privileges.
7. Test cross-organization access denial before enabling writes.

The migration is not deployed to the server by committing it to GitHub.
