# Core Workflow Traceability — 2026-10-17

Status: source-derived inventory originally captured from `master` and updated as a working-branch traceability baseline; not a certification that licensing requirements or external integrations are satisfied. Route names and behavior were inspected in `Program.cs`, endpoint classes, use cases, and test-file inventory.

## API workflows observed

| Workflow | Route(s) | Current behavior evidenced in repository | Tests / evidence | Open gate |
|---|---|---|---|---|
| Identity verification | `POST /identity/verify`, `GET /identity/me` | Verification endpoint uses Applicant authorization; identity state is queried for the current user. | `IdentityVerificationEndpointTests.cs`, `PrivacyComplianceTests.cs` | Official identity-provider contract, approved adapter, sandbox and production approval. |
| OTP and MFA | `POST /identity/otp/challenges`, `POST /identity/otp/verify`, `GET /identity/mfa-required` | OTP challenge issuance/verification are authorized and rate-limited; MFA-verified policy exists. | `PersistentAuthenticationStateTests.cs`, `RateLimitingTests.cs`, authorization tests; persistent OTP blank-input guards are covered on the continuation branch | Production identity/session configuration, key management and approved delivery channel evidence. |
| Generic service request | `POST /service-requests` | Applicant-authorized creation requires verified identity and persists a request plus initial stage. | `BrokerageApiTests.cs`, `AuthorizationTests.cs` | Verify service-specific validation and stage definitions against each service's approved requirement; do not let generic creation bypass service-specific flows. |
| S01 service request | `POST /service-requests/s01` | Applicant-authorized, verified-identity path calls the organization adapter and persists request/stages/audit locally. | Workflow tests in `BrokerageApiTests.cs`; rollback and concurrency evidence documented in `CORE_WORKFLOW_INVENTORY.md` | Organization acceptance vs local persistence is not atomic. Official idempotency/correlation/status-lookup contract and recovery runbook are required. |
| Read case and workflow history | `GET /service-requests/{id}`, `GET /service-requests/{id}/workflow-stages` | Authorization is required; request-level access checks protect case and stage history. | `AuthorizationTests.cs`, `PrivacyComplianceTests.cs` | Review the returned fields against data minimization and role matrix from approved requirements. |
| Refresh organization status | `GET /service-requests/{id}/organization-status` | Reads remote status and updates request/stages/audit; optimistic concurrency maps stale local writes to HTTP 409. | `BrokerageApiTests.cs` and workflow consistency notes | GET has write side effects; preserve compatibility until client contract is reviewed. Production-provider concurrency remains unverified. |
| Create payment | `POST /payments` | Applicant/MFA/ownership, server-owned tariff and idempotency controls exist; initiation checks `Payment:Enabled`, false by default. | `PaymentEndpointTests.cs`, `PaymentPersistenceTests.cs`, `SadadPaymentGatewayTests.cs` | Keep disabled until merchant contract, approved tariff/service mapping, sandbox and PHP/.NET ownership are signed off. |
| Sadad callback | `GET/POST /payments/sadad/callback` | Callback is not treated as final settlement alone; server-side verification and conservative ambiguous-outcome handling are implemented. | `PaymentCallbackPostTests.cs`, payment tests | Confirm exact request/signature/date/response behavior against the current merchant pack and authorized sandbox. |
| Payment operations | `GET /ops/payments/stale-verifications`, `GET /ops/payments/reconciliation-candidates` | Read-only operational views are authorization- and rate-limit-protected. | `PaymentOperationalEndpointAuthorizationTests.cs`, `PaymentStaleTimestampBoundaryTests.cs` | Assign named operators, alerts and runbook; verify target-environment role mappings. |
| Audit export | `GET /audit/events/export` | Role-restricted NDJSON export with bounded result count, DB-side filtering/order/limit, and no-store response. | `AuditComplianceTests.cs` | Retention header is not by itself a retention enforcement policy; approve retention/deletion, access review and export audit requirements. |
| Operational monitoring | `GET /ops/metrics`, `GET /ops/status`, `GET /health`, `GET /ready` | Health/readiness and operational metrics/status endpoints exist; privileged monitoring routes have authorization and read limits. | `OperationalMetricsTests.cs`, `OperationalStatusTests.cs`, `OperationalTelemetryContractTests.cs` | Dashboard is not equivalent to organization-accessible read-only monitoring; production SIEM, alerting, support and multi-instance limits remain open. |

## Licensing requirement cross-check

The supplied `الزامات_کارگزاری.html` describes a separate license and activity-permission process. The most material software acceptance gates from that source are:

1. A working brokerage system implementing at least three services according to their own requirements for the relevant licensing gate (subject to the explicit exception in the supplied text).
2. Qualified penetration test by an approved lab, complete report, and software technical approval.
3. Production and test environments hosted exclusively in approved domestic data centers.
4. Direct payment gateway connected to the legal entity's account, without intermediary accounts.
5. At least 99% monthly uptime as stated in the supplied guideline, plus evidence for read-only regulator dashboards covering service status, resource use, API latency, error rates and relevant logs.
6. Support/call center, NDA, SLA, required security deposit and prescribed corporate-purpose wording; these are organizational/contractual evidence, not API tests.

Current repository evidence does **not** establish that the minimum three complete, approved service workflows exist. S01 is the only service-specific creation route visible in the current API route inventory; generic request creation is not proof that multiple independently approved services are complete. Confirm the exact approved service requirements before implementing additional workflows.

## Required next work, ordered

1. Obtain/identify the approved requirement for each service and create one acceptance checklist per service.
2. For each service, trace: public entry point → API route → authorization/identity → validation → domain state/stages → persistence/audit → external adapter → failure/retry/reconciliation → regression tests.
3. Mark each external adapter as `mock`, `contract-defined`, `sandbox-tested`, or `production-approved`; do not promote a status without evidence.
4. Add only missing regressions tied to explicit approved rules; avoid broad refactors while the service rules and external contracts are unknown.
5. Track legal, hosting, security certification, uptime, support, dashboard, and payment-account evidence separately from CI.

## Scope protections

- Do not alter the live PHP payment handler or expose secrets.
- Do not activate the .NET payment flow while its release checklist is incomplete.
- Do not infer tariffs, undocumented external API fields, or workflow stages.
- A green pipeline verifies the checks it ran, not the external/legal requirements above.
