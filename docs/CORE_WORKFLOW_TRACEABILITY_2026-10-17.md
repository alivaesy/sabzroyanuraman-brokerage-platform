# Core Workflow Traceability — 2026-10-18

Status: source-derived inventory for the working branch, not a certification that licensing requirements or external integrations are satisfied.

## API workflows observed

| Workflow | Route(s) | Current behavior evidenced in repository | Tests / evidence | Open gate |
|---|---|---|---|---|
| Identity verification | `POST /identity/verify`, `GET /identity/me` | Applicant verification and current-user identity state. Identity errors are covered by no-store tests. | `IdentityVerificationEndpointTests.cs`, `PrivacyComplianceTests.cs` | Official provider contract, approved adapter, sandbox and production approval. |
| OTP and MFA | `POST /identity/otp/challenges`, `POST /identity/otp/verify`, `GET /identity/mfa-required` | Persistent OTP validates incomplete inputs before database mutation; authorization and rate-limit policies exist. | `PersistentAuthenticationStateTests.cs`, rate-limit and authorization tests | Production session/key configuration and approved delivery channel evidence. |
| Generic service request | `POST /service-requests` | Applicant-authorized creation checks verified identity; persists request and initial stage, and writes creation/stage audit events within one local transaction. S01 is rejected from this generic path. | `CreateServiceRequestAuditTests.cs`; pipeline `2959886605` succeeded for commit `0203dde2`. | Approved per-service required fields, workflow stages and acceptance criteria are still missing for S02–S05. Generic creation is not proof of complete services. |
| S01 service request | `POST /service-requests/s01` | Verifies identity, submits to organization adapter, then attempts to persist request/stages/audit in a local transaction. Known tracking IDs are persisted despite caller cancellation. | `CreateS01ServiceRequestAuditTests.cs` covers rollback and cancellation cases. | **Code gap:** constructor still permits missing repository, audit writer and unit of work; direct/manual construction can return a request without durable persistence or persisted audit. Production DI registers these services, but the use case itself does not enforce the invariant. Make dependencies mandatory and update unit tests before treating this as hardened. External acceptance and local commit remain non-atomic; obtain idempotency/status-lookup and recovery contract. |
| Read case and stage history | `GET /service-requests/{id}`, `GET /service-requests/{id}/workflow-stages` | Authorization/access predicate checks request access before returning data. | `AuthorizationTests.cs`, `PrivacyComplianceTests.cs` | Check returned fields against data minimization and approved role matrix. |
| Organization status refresh | `GET /service-requests/{id}/organization-status` | Reads remote status and updates local status/stages/audit; optimistic concurrency can return 409. | `BrokerageApiTests.cs`, workflow consistency tests | GET has write side effects; preserve compatibility until clients are reviewed. Production provider behavior and client conflict handling remain unverified. |
| Payment creation | `POST /payments` | Server-side tariff, idempotency and ownership controls; `Payment:Enabled=false` by default. | Payment endpoint/persistence tests | Keep disabled until approved tariff/service mapping, current Sadad merchant pack, sandbox evidence and PHP/.NET ownership decision. |
| Sadad callback | `GET/POST /payments/sadad/callback` | Callback alone does not finalize payment; server verification and conservative ambiguous-outcome handling exist. | Callback and gateway tests | Verify exact fields, cryptography, date/time, response codes and callback behavior against current merchant pack and sandbox. |
| Payment operations | `GET /ops/payments/stale-verifications`, `GET /ops/payments/reconciliation-candidates` | Bounded, role-protected operational views. | Payment operational authorization and boundary tests | Named operators, alerts and recovery runbook. |
| Audit export | `GET /audit/events/export` | Role-restricted NDJSON with database-side filters/order/limit and no-store. | `AuditComplianceTests.cs` | Retention header is not retention enforcement; approve retention, access review and export auditing. |
| Monitoring | `GET /ops/metrics`, `GET /ops/status`, `GET /health`, `GET /ready` | Health/readiness and process metrics endpoints exist. | Operational metrics/status/telemetry tests | Production SIEM, alerting, organization-accessible read-only dashboard and multi-instance controls remain open. |

## Licensing and production acceptance gates

The supplied `الزامات_کارگزاری.html` describes licensing/activity-permission requirements. Key evidence gates include:
1. At least three services implemented against their own approved requirements for the applicable licensing gate (subject to the stated exception).
2. Approved-lab penetration test, complete report and software technical approval.
3. Approved domestic hosting for production and test environments.
4. Direct payment gateway linked to the legal entity, not an intermediary account.
5. Evidence for the stated 99% monthly uptime, read-only regulator dashboard, support/call center, NDA/SLA, deposit and corporate-purpose requirements.

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
