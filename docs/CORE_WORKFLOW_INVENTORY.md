# Core Workflow Inventory

Status: repository-derived engineering inventory, not a compliance certification or proof that external services are production-ready.

## API workflow entry points observed on `feat/payment-foundation-sadad-ready`

| Capability | Route / component | Observed behavior | Evidence still required |
| --- | --- | --- | --- |
| Generic service request | `POST /service-requests` | Requires Applicant authorization and verified identity; persists the request with status `Created` and its initial workflow stage `INITIAL`. Undefined numeric service codes are rejected. | Confirm each service code has an approved workflow definition and that the generic path cannot bypass service-specific required fields/stages. Regression tests added and passed in pipeline `2938914890` for branch head `59b971df4a306a25c37461981c9be03cd1e1b302`. |
| S01 request | `POST /service-requests/s01` | Requires Applicant authorization and verified identity state; executes the S01 use case, performs identity verification, submits to the organization adapter, records workflow stages and audit events. | Confirm adapter is an approved sandbox/production integration rather than a development mock. **Submission idempotency and audit atomicity are unresolved blockers; see the risk review below.** |
| Read own/request-authorized case | `GET /service-requests/{id}` | Loads a request and checks applicant ownership or an explicitly privileged role. | Verify every returned field is necessary for the caller and role grants match the approved authorization matrix. |
| Workflow-stage history | `GET /service-requests/{id}/workflow-stages` | Uses the same request access predicate before reading stage history. | Verify stage metadata does not reveal unnecessary organization or personal information. |
| Organization status refresh | `GET /service-requests/{id}/organization-status` | Reads remote organization status, mutates the stored status, may complete/create workflow stages, and writes audit events. `ServiceRequest.UpdatedAt` is an EF concurrency token; a stale local writer is mapped to HTTP 409 rather than silently overwriting a newer transition. | The optimistic concurrency token and HTTP 409 conflict path are implemented, but the first CI run of the new regression failed (158/159 API tests passed). The test is being made deterministic by observing the first committed status before releasing the stale writer; rerun CI before claiming the behavior is verified. Confirm client retry handling and production database/provider behavior before release. GET still has state-changing side effects; preserve route compatibility until clients are reviewed. |
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


## Latest verified state and consistency risks (2026-10-12)
- Current branch HEAD: `5808352357792b0ff1ae14e623a2403fbdf785d9`. Pipeline `2939258054` passed all five configured jobs: `site_validate`, `php_validate`, `dotnet_build`, `dotnet_migration_verify`, and `dotnet_test`. https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2939258054
- Organization status validation now fails closed for missing/blank values. General logs omit the returned status and status-request tracking ID; the privacy regression tests passed in the latest pipeline.
- **Important scope distinction:** the status value is still returned to the workflow and persisted on the service request; the current status audit event also stores the old/new status fields. Logging redaction alone does not establish that persistence is compliant with the approved data-retention policy.
- **Persistence risk:** S01 audit events are written and saved before the request/stages are persisted. Each audit write saves independently. A failure can leave audit events without a durable request or leave a partially completed sequence.
- **Concurrency risk:** organization-status refresh performs a read/modify/write sequence in a GET handler without an explicit optimistic-concurrency check or transaction around request, stage and audit writes. The existing sequential repeat test does not prove two concurrent requests cannot race.
- **External side-effect risk:** organization submission precedes local persistence. A remote success followed by local failure needs an approved reconciliation/idempotency policy; a local DB transaction cannot roll back the remote organization's submission.
- Do not add a global unique constraint on `(ServiceRequestId, StageCode)` until stage multiplicity rules and existing deployed data are checked. Do not change the public GET method until client compatibility is reviewed.
- A proposed test/design matrix and safe implementation order are recorded in `docs/WORKFLOW_CONSISTENCY_REVIEW.md`. Payment remains disabled and no production-readiness claim is implied by CI success.

 
## Current branch evidence — S01 transaction work (2026-10-12)
- Commit `530c87f97cdfbfe516adf769e3416a59df1b273a` passed all five configured CI jobs in pipeline `2939464325`: https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2939464325
- S01 local request, workflow-stage and audit writes are now coordinated by a scoped EF transaction; the `ServiceRequestCreated` audit event is emitted inside the use case before commit.
- The external organization submit remains outside the local transaction. If the organization accepts a request and local persistence subsequently fails, the remote case may still exist; approved reconciliation/idempotency behavior is required before production use.
- A SQLite-backed rollback regression test passed pipeline `2939513854`, providing real local rollback evidence for S01 persistence. Organization-status GET consistency work is described below; concurrency remains open.

 
## Organization-status consistency update — 2026-10-12
- The S01 SQLite rollback test passed in pipeline `2939513854`: https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2939513854
- Organization-status GET now groups local request status, stage updates, and audit writes in one scoped EF transaction after the external status response is received. A new injected-audit-failure test verifies local rollback and passed in pipeline `2939580658` for commit `51eac5102ffa98dce2cf542aa2757511826691dc`.
- Concurrency is not claimed as solved: external fetch remains outside the transaction and no optimistic concurrency token or unique stage constraint was added. Concurrent refresh testing and provider-specific semantics remain open.


## S01 submission recovery boundary — verified CI follow-up (2026-10-13)
- The local S01 transaction protects request, workflow-stage, and audit persistence from partial local commits when a later local write fails.
- It does not provide a distributed transaction with the organization API. If the external submission is accepted and local persistence later fails, the external case may exist without a durable local request; a timeout may also leave acceptance ambiguous.
- Regression coverage is being strengthened to assert one successful external submit followed by complete local rollback under injected persistence failure. This characterizes the failure window; it does not itself reconcile the external case.
- Required before production-safe retry/recovery: authoritative organization API documentation for idempotency/correlation and status lookup, plus an approved operator recovery procedure. Do not guess headers, keys, statuses, stages, or retry semantics.
- Pipeline [2941032895](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2941032895) passed all five configured jobs for the prior commit. The next commit's test/documentation changes must pass their own pipeline.


## External organization submission retry boundary — 2026-10-13
- A configured retry count above zero previously allowed S01 submission attempts to repeat after timeout, server-error, or rate-limit results. Such results do not prove the organization rejected the request.
- Submission now explicitly opts out of automatic retries pending a verified external idempotency/correlation contract. Status lookups keep configurable retries because they are read operations. CI must verify this change before it is treated as validated.
- This is a safety guard, not an external reconciliation mechanism. Ambiguous submissions still require authoritative organization lookup and approved operator recovery. Payment remains disabled; live PHP payment is unchanged.


## Current verified retry boundary — 2026-10-13
- Pipeline [2941232829](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2941232829) passed all five configured jobs for commit `0c8998db8d9b16943bc35ea927d0319c42d5a09e`.
- S01 submission now calls the shared retry executor with `allowRetries: false`; regression tests cover timeout, server error, and rate-limit outcomes and expect one attempt. Status-read retry tests still cover timeout, server error, and rate limiting.
- This supersedes the older risk-review wording above that described submission as automatically retrying in the current code. The underlying external-side-effect risk remains: no authoritative idempotency/correlation contract or automated reconciliation has been established.
- Local transactions cannot undo a completed remote submission. Keep ambiguous cases unresolved for manual, evidence-based investigation until the organization's documented contract and an approved recovery policy exist.
- Caller-cancellation propagation is being added as a regression check and remains unverified until its pipeline passes.


## CI failure and correction — 2026-10-13
- Pipeline [2941371827](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2941371827) failed in `dotnet_build` before test execution: the follow-up commit inserted a second `CancellationOrganizationApiClient` and a redundant cancellation test even though the file already contained both cancellation coverage and its helper.
- The corrective change removes only the duplicate test/helper, preserving the pre-existing `SubmitAsync_WhenCancellationIsRequested_PropagatesCancellation` regression test. The existing cancellation test remains the source of truth; no runtime behavior change is intended.
- The next pipeline must verify the correction. The previous failed pipeline is not evidence that the cancellation behavior failed; it exposed compile-time duplicate definitions.


## Caller cancellation and retry boundary — 2026-10-13
- Pipeline `2941486180` passed all five configured jobs for branch commit `a066667beba65b9140e50fa74a71a2023a966692`.
- A focused follow-up checks the caller cancellation token after a failed adapter result and before any retry decision. This prevents a non-cooperative adapter that returns a transient failure after cancellation from triggering further attempts.
- The regression test and implementation require the next pipeline for verification; no new external API semantics are assumed. S01 submit remains non-retrying, status reads retain configured retries, and ambiguous remote outcomes remain a manual/contract-driven recovery boundary.


## S01 persistence after caller cancellation — 2026-10-13
- The latest pipeline `2941591856` passed all five jobs for commit `239cd8216fc11f1c49086cd7ffe50ba2e291d6ac`.
- After the external organization adapter returns a tracking ID, local persistence now uses a non-cancelled token so a disconnected/cancelled HTTP request does not itself abort recording a known remote submission. A SQLite regression test verifies the request, workflow stages and audit rows persist when the caller token is cancelled immediately after remote success.
- This narrows but does not eliminate the distributed failure window. Process termination, database failure, or timeout-after-acceptance remain unresolved without an approved reconciliation/idempotency contract. Payment stays disabled and the PHP live flow remains unchanged.
