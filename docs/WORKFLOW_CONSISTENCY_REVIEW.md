# Workflow Consistency and Failure-Semantics Review

**Status:** design review; no business contract or release gate is presumed approved.  
**Current implementation branch:** `codex/audit-otp-ci-handoff-hardening`, based on `feat/payment-foundation-sadad-ready` at `7d76a81b`.  
**Current validation:** initial integrated pipeline [2945622710](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2945622710) passed build, migration verification, site validation, secret scan, and PHP syntax lint, but one new API test failed because its EF-log assertion counted multiple diagnostic messages. A test-only correction narrows capture to the executed command; follow the corrected HEAD from the [branch pipeline list](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines?ref=codex%2Faudit-otp-ci-handoff-hardening). The planning-only pipeline [2945576068](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2945576068) is not code evidence. Payment remains disabled; the live PHP payment flow is outside this work.

## Current hardening checkpoint (2026-10-14)

The current branch bounds SQLite audit export with database-side date predicates, deterministic timestamp/EventId ordering, and LIMIT; removes expired OTP rows with a targeted database delete; and makes the local PowerShell checks call the same site and EF-migration validation scripts as GitLab CI. New SQLite regressions check equivalent non-UTC date bounds, tie ordering, bounded query SQL, and expired versus active OTP rows. Pipeline [2945622710](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2945622710) confirmed build and migration validation but exposed a false-positive test assertion over EF diagnostics. The test now selects the executed-command log entry; the corrected branch pipeline must pass before these regressions are called verified. No deployment or production database was inspected.

## Prior implementation checkpoint (2026-10-13; historical)

The sections below preserve the earlier workflow review and payment-branch implementation notes. Their references to branch heads and pipelines are historical and do not supersede the current hardening checkpoint above.

## Current implementation checkpoint (2026-10-13)

The original review below records earlier implementation snapshots; where it conflicts with this checkpoint, this section is authoritative. The latest branch commit `f46e7607352840d9b0288a90e1da96ba08ec475f` changes documentation only and passed pipeline [2942086092](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2942086092), including all five configured jobs. The underlying code/test checkpoint `00927789eed4a206fc29b9888895502023780178` passed pipeline [2941850499](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2941850499). The S01 path opens the local unit-of-work transaction after the organization adapter returns a tracking ID, writes request/stage/audit records under that transaction, and commits with `CancellationToken.None` once remote success is known. SQLite tests cover cancellation after remote success and rollback of local request/stage/audit rows after an injected persistence failure; the rollback test asserts the external submission occurs once. The deterministic organization-status concurrency regression passed in the code/test checkpoint pipeline. Submission retries remain disabled because remote acceptance can be ambiguous; no approved external idempotency or reconciliation contract is evidenced here. This does not make remote submission and local persistence atomic, and does not solve process crashes or database failures.

## 1. Scope and observed behavior at original review snapshot (superseded where noted above)

This review focuses on the S01 submission path and the organization-status refresh path. It does not change public API behavior, external organization contracts, stage codes, or payment behavior.

### S01 submission sequence

The current sequence in `CreateS01ServiceRequest.ExecuteAsync` is:

1. Verify the applicant identity through the configured identity adapter.
2. Submit S01 to the external organization adapter and receive a tracking ID.
3. Construct the local service request and its workflow stages.
4. Emit audit events; the production `AuditEventWriter` persists each event immediately with its own `SaveChangesAsync`.
5. Add the request and stages through `IServiceRequestRepository`, then save them.
6. The API endpoint emits an additional `ServiceRequestCreated` audit event.

Consequences:
- A local failure after the external submission may leave an external case without a durable local request.
- A failure between audit writes and request persistence may leave audit records for a request that is not present in the request table.
- Because each audit write saves immediately, later failures do not roll back earlier audit records.
- Moving audit writes later without a transaction would only move the partial-failure window; it would not establish atomicity.

### Organization-status refresh sequence

The current `GET /service-requests/{id}/organization-status` handler:
- Loads and authorizes the request.
- Calls the external organization status API.
- Updates the request status field.
- Loads workflow stages and completes/creates stages as needed.
- Writes audit events during this sequence.
- Saves request/stage changes, then writes the final status audit event.

Consequences:
- The GET has state-changing side effects.
- Audit events may persist before the associated request/stage changes are saved.
- Two concurrent requests can both observe an incomplete stage or missing result stage and race to write state.
- Sequential repeat tests do not prove concurrent idempotency or all-or-nothing persistence.
- Changing the HTTP method may break existing clients, so compatibility must be checked before changing the route.

## 2. Proposed design constraints

These are engineering constraints for the next implementation phase, not assumptions about external business rules.

1. **One local transaction for local state:** where supported by the existing database provider, request, workflow-stage and audit-record changes belonging to one local transition should commit or roll back together.
2. **One scoped persistence boundary:** the application use case should coordinate local persistence through a unit-of-work/transaction abstraction backed by the same scoped `BrokerageDbContext`. Avoid making each audit append independently durable inside a transition.
3. **Do not hold a database transaction across a network call:** the external organization service cannot participate in a local EF/database transaction. Keep network calls outside short local transactions.
4. **Explicit external-side-effect recovery:** define how to reconcile “organization accepted submission, local save failed” and “response timed out, organization may have accepted submission.” Use only the external authority's documented idempotency/correlation contract. Do not invent an idempotency header, request key, status, or stage.
5. **Concurrency must be explicit:** choose a supported optimistic-concurrency token, database locking strategy, or compare-and-set transition after reviewing provider support and retry semantics. Make stage creation and request state changes safe under concurrent refreshes.
6. **Preserve public compatibility:** keep the existing GET route until clients and API compatibility are reviewed. A command-style endpoint can be proposed separately; do not silently change method semantics.
7. **Data classification applies to storage as well as logs:** organization status is currently stored in `ServiceRequest.OrganizationStatus` and audit previous/new state fields. Confirm whether raw status values may be retained and for how long before changing this behavior.

## 3. Ordered implementation and verification plan

### Phase A — characterize behavior (no schema change)

- Add failure-injection tests around audit writes, request/stage persistence, and the final status audit event.
- Add a concurrent status-refresh test using the same persisted request and a controlled organization API client.
- Assert request state, stage count/completion, and audit rows after both success and injected failure.
- Add a test that identifies whether status values are intentionally persisted in audit records; do not change expected retention semantics until the data policy is approved.
- Record the current database provider and test-fixture behavior so the tests exercise realistic transaction semantics.

### Phase B — local transaction boundary

- Introduce a scoped transaction/unit-of-work contract only after deciding which layer owns it and confirming all repositories/writers use the same scoped `BrokerageDbContext`.
- Change audit writing so it participates in the caller's local transaction rather than independently committing each event during the transition.
- Persist request, stages, and audit records in one short transaction; add rollback tests that prove none of those local changes survive an injected failure.
- Keep external calls outside that transaction and explicitly represent/reconcile an external success followed by local failure according to the approved business policy.

### Phase C — concurrency protection

- Choose the concurrency mechanism supported by the production database and deployment model.
- Re-read and re-evaluate the transition after a concurrency conflict; do not blindly retry an entire external request.
- Validate duplicate-stage behavior against the actual stage lifecycle. Do not add a unique `(ServiceRequestId, StageCode)` index until multiplicity rules are approved and existing data is checked.
- Run repeated and parallel refresh tests and verify there is no duplicated result stage, inconsistent current-stage pointer, or audit/state mismatch.

### Phase D — external idempotency and reconciliation

- Obtain the authoritative organization API contract and clarify timeout-after-acceptance behavior.
- Determine whether the external service supports idempotency, query-by-correlation, or another official reconciliation process.
- Implement only the documented mechanism and add contract/sandbox tests. Until then, do not treat automatic submission retries as production-safe.

## 4. Acceptance criteria

- A local transition either persists its request/stage/audit changes as one unit or leaves none of those local changes committed.
- A failed local persistence operation after an external submission is visible to an approved reconciliation process and does not get silently retried as a new external submission.
- Parallel organization-status refreshes converge to one valid workflow state with no duplicate result stage and no audit event claiming an uncommitted transition.
- Privacy tests cover both general logs and any approved persistent audit fields.
- The exact branch commit has a successful CI pipeline; test evidence is reviewed independently of the pipeline status.
- No new enum, stage, external API field, tariff, service mapping, database constraint, or legal interpretation is invented.

## 5. Explicitly out of scope

- Enabling .NET payments or changing `Payment:Enabled=false`.
- Modifying the live PHP payment flow.
- Merging this branch to `master`.
- Claiming production readiness, regulatory compliance, or external API approval from repository tests alone.

 
## Implementation update — S01 local transaction boundary (2026-10-12)
- Commit `530c87f97cdfbfe516adf769e3416a59df1b273a` passed pipeline `2939464325` (all five configured jobs): https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2939464325
- The S01 use case now starts a local EF transaction after the external organization submission and before any local request/stage/audit writes. Those local writes share the scoped `BrokerageDbContext`; the `ServiceRequestCreated` event is now written before transaction commit.
- The external organization call is deliberately not inside the transaction. Remote success followed by local failure still requires a documented reconciliation/idempotency policy; do not automatically resubmit.
- The SQLite-backed failure-injection test passed pipeline `2939513854` and verifies persisted request/stage/audit rows are rolled back after local persistence failure.
- The organization-status GET remains outside this change and still needs targeted concurrent-refresh and partial-failure tests. Do not change the public method or add stage uniqueness constraints without compatibility and data review.

 
## Organization-status implementation follow-up — 2026-10-12
- The SQLite-backed S01 local transaction rollback test passed in pipeline `2939513854`: https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2939513854
- The status refresh endpoint now opens a local EF transaction after the organization API returns and commits request status, stage completion/creation, and all associated audit records together. An API regression test injects failure when writing the final status audit event and checks that request status, stages, and audit records revert.
- The new endpoint test is not verified until this commit's pipeline passes. This change is atomicity work, not a concurrency guarantee: external status retrieval remains outside the transaction; optimistic concurrency and stage uniqueness are unchanged and require separate analysis/testing.
- S01 external submit reconciliation/idempotency remains unresolved. Do not change HTTP method, infer organization contract, or add uniqueness constraints without evidence.


## Sequential idempotency versus concurrent refresh — 2026-10-12
- The sequential repeat-refresh API test now checks both persisted workflow state and audit history: two successful refresh responses are recorded, while `ResultNotification` creation and `OrganizationFollowUp` completion are each recorded only once.
- This is a sequential idempotency assertion only. It does not exercise overlapping requests. The endpoint reads the request and its prior status before the remote call and opens the local transaction only after the remote call returns; overlapping requests can therefore act on stale tracked request state.
- `workflow_stages` currently has a primary key but no uniqueness rule on `(ServiceRequestId, StageCode)`, and `ServiceRequest` has no EF concurrency token. Do not add a unique index until stage multiplicity and existing data are reviewed. Before claiming race safety, choose and test a provider-supported strategy (e.g. optimistic concurrency with conflict handling or a transaction/locking design), including its migration and client-visible retry/error semantics.


## Rollback regression follow-up — 2026-10-12
- Extend the organization-status audit-failure regression to capture the request's audit-event count before the refresh and assert that the count is unchanged after the injected failure. This protects both sides of the rollback contract: no partial status/stage/audit additions, and no loss of pre-existing audit history.
- This assertion complements the existing checks for unchanged request status, incomplete `OrganizationFollowUp`, absent `ResultNotification`, and absent status-refresh audit events. It does not address concurrent refresh races.


## Optimistic concurrency implementation follow-up — 2026-10-13
- Configure `ServiceRequest.UpdatedAt` as an EF Core concurrency token. The organization-status endpoint maps `DbUpdateConcurrencyException` to HTTP 409 and does not automatically repeat the external status request.
- The first API test run for this change reported 158/159 tests passing. Review found that the concurrency test incorrectly tied the first HTTP task to the first integration-service invocation; concurrent scheduling does not guarantee that correspondence. The test now waits for the first invocation's persisted status before releasing the second invocation, then asserts one 200 and one 409 and checks the single result-stage/status-audit transition.
- Pipeline `2940869925` passed site validation, PHP validation, .NET build, and migration verification, but `dotnet_test` failed. The deterministic-test correction above is not verified until its next pipeline passes.
- This is optimistic concurrency protection for writes to the service-request row, not a distributed lock or proof of every production-provider race mode. Keep external calls outside the transaction; do not add stage uniqueness constraints or infer external idempotency semantics.


## S01 external side-effect failure-window follow-up — 2026-10-13
- Pipeline [2941032895](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2941032895) passed all five configured jobs for commit `6c72b0ebd0f12e9081f90ef6578e2ede2ab512b5`, including the deterministic concurrent organization-status refresh regression.
- The next test change makes the existing SQLite rollback scenario assert the complete failure window explicitly: the external S01 submit returns a tracking ID exactly once; local request, workflow-stage, and audit rows are then written inside the local transaction; an injected persistence failure rolls all local rows back.
- This test documents an important distributed-systems boundary, not a complete recovery mechanism. A local rollback cannot undo an accepted external submission. Until the official organization API contract specifies idempotency/correlation and lookup/reconciliation behavior, automatic resubmission is unsafe and no new external fields or stages should be invented.
- Keep the external network call outside the local database transaction. Payment remains disabled, the live PHP payment flow remains untouched, and no merge is authorized.


## External submission retry-safety follow-up — 2026-10-13
- Pipeline [2941145121](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2941145121) passed all five configured jobs for commit `0bf0dab425a937dbd8697c044577e48121b8e0d9`.
- Review identified that the shared organization retry executor's policy permits retrying timeouts, server errors, and rate limits. Applied to a non-idempotent submit without a verified external idempotency contract, that can duplicate a case when the remote side accepted the request but its response was lost.
- Proposed/implemented guard for verification: submission explicitly disables executor retries; status lookup retains the existing configurable retry behavior. Tests should prove one attempt for submit failures and preserve status-read retry coverage.
- This reduces automatic duplicate-submission risk; it does not reconcile an ambiguous result. An authoritative external API contract and an approved recovery procedure are still required. No API fields, headers, keys, or business stages are invented.


## Current CI checkpoint and cancellation follow-up — 2026-10-13
- Pipeline [2941232829](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2941232829) passed all five configured jobs for commit `0c8998db8d9b16943bc35ea927d0319c42d5a09e`.
- S01 submission retry is disabled at the call site because an accepted remote request may have lost its response; no official organization idempotency/correlation contract is present in the repository evidence. Status lookup still has retry coverage for timeout, server error, and rate limit.
- The next regression asserts caller-requested cancellation propagates without being mistaken for an integration timeout and without another submission attempt. This is a test-only behavior check; do not claim it verified until the next CI run passes.
- Recovery for remote success followed by local failure remains manual and contract-driven. Do not invent a replay key, status, stage or external request field.


## CI failure and correction — 2026-10-13
- Pipeline [2941371827](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2941371827) failed in `dotnet_build` before test execution: the follow-up commit inserted a second `CancellationOrganizationApiClient` and a redundant cancellation test even though the file already contained both cancellation coverage and its helper.
- The corrective change removes only the duplicate test/helper, preserving the pre-existing `SubmitAsync_WhenCancellationIsRequested_PropagatesCancellation` regression test. The existing cancellation test remains the source of truth; no runtime behavior change is intended.
- The next pipeline must verify the correction. The previous failed pipeline is not evidence that the cancellation behavior failed; it exposed compile-time duplicate definitions.


## Caller cancellation before retry — 2026-10-13
- Pipeline `2941486180` passed all five configured jobs for commit `a066667beba65b9140e50fa74a71a2023a966692`.
- Additional hardening checks the caller cancellation token after a failed operation result and before retry evaluation. A regression simulates an adapter returning a retryable server-error result after the caller cancels; the expected result is propagated cancellation and exactly one invocation.
- This is limited to cancellation/retry semantics. It does not change submission retry policy, external API contracts, workflow stages, payment behavior, or reconciliation claims. Verify only after the next pipeline completes.


## Cancellation after known remote success — 2026-10-13
- Pipeline `2941591856` passed all five configured jobs for commit `239cd8216fc11f1c49086cd7ffe50ba2e291d6ac`.
- A further regression targets the narrow window where the organization adapter returns a tracking ID and the caller token is then cancelled. Local transaction creation, repository writes and commit now use `CancellationToken.None` after this confirmed remote response, to avoid abandoning the known remote case solely because the HTTP caller disconnected.
- SQLite-backed coverage asserts that the request, three workflow stages and seven audit events persist in that scenario. This does not solve process crashes or local persistence failures, and it does not make remote submission and local persistence atomic. Verify in the next pipeline before treating this guard as established.
