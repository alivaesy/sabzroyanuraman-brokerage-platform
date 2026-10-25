# GIS organization authorization and DB-role acceptance tests

Run only against a disposable/staging database and staging URL. Do not use production identities or project data.

## Preconditions
- Apply reviewed migrations to staging and configure the regular API connection as `gis_staging` and the privileged admin endpoint as `gis_admin_api` via private config files.
- Provision two active organizations (A and B), one active user in each, and an editor in A plus a viewer in A. Give A and B active subscriptions; also retain a test subscription that can be expired.
- Create one uniquely named project with a feature in each organization. Record the IDs without putting credentials or personal data in logs.
- Use separate cookie jars per test identity. Obtain CSRF token through `GET /api/gis/auth.php?action=csrf`; send matching cookie and `X-CSRF-Token` on every state-changing request.

## HTTP authorization matrix

| Case | Request identity / condition | Operation | Expected |
|---|---|---|---|
| AUTH-01 | no session | list projects for A | 401 `authentication_required` |
| ORG-01 | user A, active member | list A | 200; only A projects |
| ORG-02 | user A, no membership in B | list B | 403 `organization_access_denied` |
| ORG-03 | user A | retrieve known B project ID scoped to B | 403 before project lookup |
| ORG-04 | user A/editor | save or archive a B project | 403; B data unchanged |
| ORG-05 | user A/editor | soft-delete B project | 403; B data unchanged |
| ROLE-01 | user A/viewer | save, archive, or delete in A | 403 |
| SUB-01 | member of A, subscription expired/not started/suspended | read or write A | 403 `active_subscription_required_for_organization` |
| SUB-02 | active A member and subscription | read/write A | permitted by role |
| CSRF-01 | authenticated user, missing/mismatched CSRF | POST/PATCH/DELETE | 403 `csrf_failed` |
| ORIGIN-01 | authenticated user, foreign origin | mutation | 403 `origin_rejected` |
| ADMIN-01 | non-platform-admin with valid session + CSRF | admin endpoint | 403 `platform_admin_required` |
| ADMIN-02 | platform admin | allowed admin mutation | success; audit row recorded |
| SESSION-01 | session revoked or expired | project request | 401 `authentication_required` |

For every denial, verify the response contains no project contents and compare database rows before/after. The exact HTTP error may be generalized by the API; preserve the security outcome (denied, no cross-tenant disclosure or mutation).

## Database role separation

Run the existing `tests/check-gis-db-grants.sh` as the PostgreSQL host administrator. Then test connections using each private config/role:
- Regular role must not be superuser, create roles/databases, delete users, change platform-admin flags, activate subscriptions, or rewrite audit history.
- Admin API role must be distinct from regular role; it must not be superuser, alter platform-admin flags, mutate project tables, or rewrite/delete audit history.
- Temporarily remove/rename the admin private config in staging only: admin endpoint should return 503 and must not fall back to the regular DB role.
- Confirm PostgreSQL logs identify the expected login role for regular project calls and admin mutations.

## Evidence / merge gate

Record staging URL (no credentials), migration revision, commit SHA, date, test case IDs and outcome. Attach sanitized HTTP status/results and role-grant output. Merge gate: all cases pass, PHP lint is green, and no production migration/deployment is included. This document is a test plan; it does not claim the staging tests have been executed.
