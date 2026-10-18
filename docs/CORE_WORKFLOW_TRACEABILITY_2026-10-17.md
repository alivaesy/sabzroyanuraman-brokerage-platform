# Core Workflow Traceability — 2026-10-18

Status: source-derived inventory for the working branch, not a certification that licensing requirements or external integrations are satisfied.

## API workflows observed

| Workflow | Route(s) | Current behavior evidenced in repository | Tests / evidence | Open gate |
|---|---|---|---|---|
| Identity verification | `POST /identity/verify`, `GET /identity/me` | Applicant verification and current-user identity state. Identity errors are covered by no-store tests; unexpected non-cancellation provider exceptions are recorded as generic failure audit events without the submitted identifier. | `IdentityVerificationEndpointTests.cs`, `PrivacyComplianceTests.cs` | Official provider contract, approved adapter, sandbox and production approval. |
| OTP and MFA | `POST /identity/otp/challenges`, `POST /identity/otp/verify`, `GET /identity/mfa-required` | Persistent OTP validates incomplete inputs before database mutation; authorization and rate-limit policies exist. | `PersistentAuthenticationStateTests.cs`, rate-limit and authorization tests | Production session/key configuration and approved delivery channel evidence. |
| Generic service request | `POST /service-requests` | The generic path accepts only S02 and S03, requires Applicant authorization and verified identity, then persists the request, `INITIAL` stage and audit events in one local transaction. S01 is rejected from this path; S04/S05 and other unapproved service codes are not enabled. The organization-status refresh endpoint also rejects S02/S03 until their approved workflow exists, preventing S01-specific stage advancement. This is a shared request-creation foundation, not proof that each service-specific business workflow is complete. | `CreateServiceRequestAuditTests.cs`; API coverage exercises both S02 and S03 through create → read request → read stage history, checks `no-store` on each response, and verifies unverified applicants cannot create either service request, and verifies another applicant cannot read S02/S03 request or stage data. | Map S02 and S03 to their approved service-specific required fields, stages, organization integration and failure/recovery behavior; do not infer those rules from the generic route. |
| S01 organization-status refresh | `GET/POST /service-requests/{id}/organization-status` | The legacy GET and command-style POST share the S01 refresh handler; repeated refresh does not duplicate the result-notification stage. The handler rejects S02/S03 before contacting the organization adapter, because their approved workflows are not defined. S01 submission failures are also recorded with a generic provider-failure outcome before a local request exists; provider details and raw identifiers are excluded. Both standardized provider failures and unexpected non-cancellation transport/runtime exceptions are audited with generic metadata only; request/stage state remains unchanged. | `BrokerageApiTests.cs` — repeated GET/POST refresh and generic-service rejection tests | Approved provider contract, status/idempotency semantics and concurrent refresh policy. |
| S01 service request | `POST /service-requests/s01` | Verifies identity, submits to organization adapter, then attempts to persist request/stages/audit in a local transaction. Known tracking IDs are persisted despite caller cancellation. | `CreateS01ServiceRequestAuditTests.cs` covers rollback and cancellation cases. | Repository, audit writer and unit of work are now mandatory constructor dependencies; regression tests use explicit test doubles or SQLite-backed implementations. Commit `f0691874` passed pipeline `2959925854`; the current persistence guard commit `b7432181` has pipeline `2959937877` running. External acceptance and local commit remain non-atomic; obtain idempotency/status-lookup and recovery contract before implementing automated retry/recovery. |
| Read case and stage history | `GET /service-requests/{id}`, `GET /service-requests/{id}/workflow-stages` | Authorization/access predicate checks request access before returning data. | `AuthorizationTests.cs`, `PrivacyComplianceTests.cs` | Check returned fields against data minimization and approved role matrix. |
| Organization status refresh | `POST /service-requests/{id}/organization-status` (preferred), `GET` (legacy compatibility) | Reads remote status and updates local status/stages/audit; optimistic concurrency can return 409. | `BrokerageApiTests.cs`, workflow consistency tests | POST is the preferred state-changing refresh method; GET remains as a compatibility alias and still has side effects until clients are migrated. Production provider behavior and client conflict handling remain unverified. Production provider behavior and client conflict handling remain unverified. |
| Payment creation | `POST /payments` | Server-side tariff, idempotency and ownership controls; `Payment:Enabled=false` by default. | Payment endpoint/persistence tests | Keep disabled until approved tariff/service mapping, current Sadad merchant pack, sandbox evidence and PHP/.NET ownership decision. |
| Sadad callback | `GET/POST /payments/sadad/callback` | Callback alone does not finalize payment; server verification and conservative ambiguous-outcome handling exist. | Callback and gateway tests | Verify exact fields, cryptography, date/time, response codes and callback behavior against current merchant pack and sandbox. |
| Payment operations | `GET /ops/payments/stale-verifications`, `GET /ops/payments/reconciliation-candidates` | Bounded, role-protected operational views. | Payment operational authorization and boundary tests | Named operators, alerts and recovery runbook. |
| Audit export | `GET /audit/events/export` | Role-restricted NDJSON with database-side filters/order/limit, no-store, and a minimal audit event for successful exports. | `AuditComplianceTests.cs` | The configured 180-day target header (`X-Audit-Retention-Target-Days`) does not enforce storage retention; actual retention and access review remain open; successful export access is now recorded with count/limit metadata only. |
| Monitoring | `GET /ops/metrics`, `GET /ops/status`, `GET /health`, `GET /ready` | Health/readiness and process metrics endpoints exist. | Operational metrics/status/telemetry tests | Production SIEM, alerting, organization-accessible read-only dashboard and multi-instance controls remain open. The technical annex also requires 24/7 monitoring, secure immutable logs retained for at least six months, domestic hosting, horizontal scaling/load balancing, and a tested disaster-recovery plan with explicit RTO/RPO. |

## Licensing and production acceptance gates

The supplied `الزامات_کارگزاری.html` includes both licensing conditions and a technical annex. Key evidence gates include:
1. At least three services implemented against their own approved requirements for the applicable licensing gate (subject to the stated exception).
2. Approved-lab penetration test, complete report and software technical approval.
3. Approved domestic hosting for production and test environments.
4. Direct payment gateway linked to the legal entity, not an intermediary account.
5. Evidence for monthly uptime (the technical annex specifies 99.5%, stricter than the 99% licensing text), read-only regulator dashboard, support/call center, NDA/SLA, deposit and corporate-purpose requirements.

Repository evidence does **not** establish three complete approved service workflows. S01 is the only dedicated service-specific creation route observed; do not count generic creation as completed S02–S05. Confirm authoritative service requirements before implementing stages or validations.

## Ordered next work

1. Enforce mandatory persistence/audit/transaction dependencies in S01 and preserve regression coverage for remote success followed by local failure.
2. Obtain the approved requirements for each of at least three services and map entry point → authorization/identity → validation → stages/state → persistence/audit → adapter → failure/recovery → tests.
3. Obtain the organization integration's idempotency/correlation/status-lookup contract; do not add automatic submission retries without it.
4. Approve production database, multi-instance concurrency, migration, backup/restore, recovery, distributed rate limiting and monitoring.
5. Keep payment disabled until the tariff, merchant contract, sandbox, PHP/.NET ownership and operational release gates are signed off.
6. Track legal, hosting, security certification, uptime, support and dashboard evidence separately from CI.

## Scope protections

- Do not alter the live PHP payment handler or expose secrets.
- Do not infer tariffs, undocumented provider fields or service workflow rules.
- A green pipeline verifies only the checks it ran; it does not certify external/legal requirements.

## Current branch hardening — 2026-10-18

- Identity verification now writes the account-bound state and its mandatory audit event in one local unit-of-work transaction. If audit persistence fails, the state change is rolled back. Regression tests cover both rollback and successful state-plus-audit commit.
- MFA verification writes the audit event before marking the in-memory MFA verification state, so an audit write failure fails closed for that request.
- Persistent OTP verification rejects and removes a challenge whose stored hash is malformed rather than throwing while attempting to decode it. A regression test covers this corrupted-persistence boundary.
- S01 audit events are asserted through the audit writer boundary, not by logging full audit payloads. This preserves event coverage without leaking event state into routine application logs.
- These changes improve local security boundaries; they do not establish production database suitability, distributed rate limiting, external identity provider approval, or regulator acceptance.
