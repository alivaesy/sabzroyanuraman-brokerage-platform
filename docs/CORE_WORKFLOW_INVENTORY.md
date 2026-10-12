# Core Workflow Inventory

Status: repository-derived engineering inventory, not a compliance certification or proof that external services are production-ready.

## API workflow entry points observed on `feat/payment-foundation-sadad-ready`

| Capability | Route / component | Observed behavior | Evidence still required |
| --- | --- | --- | --- |
| Generic service request | `POST /service-requests` | Requires Applicant authorization and verified identity; persists the request with status `Created` and its initial workflow stage `INITIAL`. Undefined numeric service codes are rejected. | Confirm each service code has an approved workflow definition and that the generic path cannot bypass service-specific required fields/stages. Regression tests added and passed in pipeline `2938914890` for branch head `59b971df4a306a25c37461981c9be03cd1e1b302`. |
| S01 request | `POST /service-requests/s01` | Requires Applicant authorization and verified identity state; executes the S01 use case, performs identity verification, submits to the organization adapter, records workflow stages and audit events. | Confirm adapter is an approved sandbox/production integration rather than a development mock. **Submission idempotency and audit atomicity are unresolved blockers; see the risk review below.** |
| Read own/request-authorized case | `GET /service-requests/{id}` | Loads a request and checks applicant ownership or an explicitly privileged role. | Verify every returned field is necessary for the caller and role grants match the approved authorization matrix. |
| Workflow-stage history | `GET /service-requests/{id}/workflow-stages` | Uses the same request access predicate before reading stage history. | Verify stage metadata does not reveal unnecessary organization or personal information. |
| Organization status refresh | `GET /service-requests/{id}/organization-status` | Reads remote organization status, mutates the stored status, may complete/create workflow stages, and writes audit events. | GET has state-changing side effects. Existing tests cover sequential repeated calls, but not concurrent requests or partial failures. The latest branch pipeline is green; concurrency/partial-failure coverage remains outstanding. Add regression coverage for concurrency and audit/state consistency; consider an explicit refresh command only after compatibility review. |
| Payment creation | `POST /payments` | Disabled by default; requires Applicant, MFA, ownership, server-side tariff and idempotency key. | Merchant-specific Sadad contract, authoritative service/tariff mapping, sandbox evidence, and explicit PHP/.NET ownership decision remain rollout gates. |
| Sadad callback | `GET/POST /payments/sadad/callback` | Treats callback token as untrusted and invokes server-to-server verification; ambiguous results are not automatically settled. | Verify the implementation against the current merchant-issued integration pack and sandbox callback behavior. |
| Payment reconciliation views | `GET /ops/payments/*` | Protected operational endpoints expose bounded stale-verification/reconciliation candidates and recommended manual checks. | Validate role assignments, operator runbook, alerting, and recovery process in the deployment environment. |

## Core workflow risk review

### 1. Organization submission can be retried without an idempotency contract

- `OrganizationRetryExecutor` retries `ServerError`, `Timeout`, and `RateLimit` outcomes.
- `IOrganizationApiClient.SubmitAsync` currently accepts a service code and cancellation token, but no caller-supplied idempotency key or request identity.
- If the external organization accepts a submission but the response times out, retrying may create a second external case. The repository currently does not prove that the external adapter deduplicates submissions.
- **Release gate:** obtain the organization's documented idempotency/correlation contract and test ambiguous-timeout behavior before enabling production submission retries. Do not invent an external field or assume that a tracking ID can be supplied before submission.

### 2. Audit writes and S01 persistence are not one atomic unit

- `AuditEventWriter.WriteAsync` calls `SaveChangesAsync` immediately.
- `CreateS01ServiceRequest.ExecuteAsync` emits stage audit events before adding the request and workflow stages through `IServiceRequestRepository`; it only adds and saves those entities after the external submission succeeds and the stage audit calls have run.
- A later failure can therefore leave audit events for a request that was never durably created, and multiple writes can partially succeed. The audit record mapping does not enforce a foreign key to the service request.
- **Release gate:** define the required failure semantics for external submission versus local persistence, then make local request/stage/audit persistence transactional where possible. If external submission cannot participate in a local transaction, use an approved idempotency/outbox/reconciliation design rather than assuming a distributed transaction.

### 3. Organization-status GET has write side effects

- `GET /service-requests/{id}/organization-status` performs a remote lookup and writes organization status, workflow stages and audit events.
- Sequential repeated-call tests verify that the result stage is not duplicated, but do not establish concurrency safety or all-or-nothing persistence under failure.
- **Next test batch:** concurrent status refreshes for the same request, duplicate stage prevention, audit consistency after persistence failures, and owner/privileged-role authorization. Do not change the public HTTP method until client compatibility is checked.

## Boundary and release rules

- A green CI pipeline proves only the checks in that pipeline passed for that commit. It does not establish legal compliance, production connectivity, merchant approval, or end-to-end readiness.
- The organization, Sana/Shahkar and geospatial adapters must be classified individually as mock, contract-defined, sandbox-tested, or production-approved.
- Do not invent missing service stages, service-code mappings, tariffs, external API behavior, or legal interpretations.
- Keep sensitive identity/registry payloads out of logs, telemetry, caches, fixtures and durable storage unless the approved data policy explicitly permits them.
- Preserve the live PHP payment flow and keep .NET payment initiation disabled until the payment rollout checklist is signed off.
- Before any merge, review the complete branch diff; current branch scope includes payment, site, CI, audit, organization integration and operational changes.


## Latest verification (2026-10-12)
- Branch `feat/payment-foundation-sadad-ready` head: `59b971df4a306a25c37461981c9be03cd1e1b302`.
- Pipeline `2938914890` passed all five jobs (`site_validate`, `php_validate`, `dotnet_build`, `dotnet_migration_verify`, `dotnet_test`): https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2938914890
- Generic request persistence and initial workflow-stage tests passed. The generic route now rejects S01 so service-specific S01 processing cannot be bypassed; the rate-limiting test was aligned to test the generic route with S02.
- CI success does not resolve the documented organization idempotency, audit atomicity, or status-refresh concurrency risks.


## Latest verification and privacy follow-up (2026-10-12)
- Commit `23330b0f2f6bc543ab475f7497189652d679dafb` passed pipeline `2939200020`; all five required jobs passed: https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2939200020.
- Organization status responses without a non-empty status are rejected, and regression coverage verifies the S01 workflow does not advance on that invalid response.
- A follow-up change removes the actual organization-returned status value from general logs and adds a regression test asserting a sensitive status marker is returned to the caller but never logged. This follow-up still requires its own pipeline result.
- Architecture compliance remains unproven: layered projects do not establish a deployed microservices topology or a completed Angular/Vue/React SPA. Treat both as evidence gaps, not completed requirements.
- Concurrency, partial persistence, external idempotency, and audit/request transaction boundaries remain open release gates.
