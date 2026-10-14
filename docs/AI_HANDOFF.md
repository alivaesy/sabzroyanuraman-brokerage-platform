# AI HANDOFF

## Project
Brokerage Platform / Sabz Royan Orouaman

## Purpose
Shared handoff between ChatGPT accounts; GitLab is the source of truth.

## Current Status

- **Current hardening checkpoint (2026-10-14):** branch `codex/audit-otp-ci-handoff-hardening`, based on `feat/payment-foundation-sadad-ready` at `7d76a81b`. Commit `e30bb010` added an index for OTP expiration cleanup, its migration, and a SQLite regression test; pipeline [2945993575](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2945993575) passed all six jobs. This follow-up consolidates .NET build, EF migration verification, and tests into one job/container so restore/build are not repeated on the single self-hosted Docker runner. Pipeline [2946175079](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2946175079) confirmed the layout: all four jobs passed, EF discovered the new migration, and the Application/API suites passed 102/102 and 166/166 tests. Pipeline elapsed time was about 5m36s versus about 13m39s for the preceding six-job run; this is one-run evidence, not a guaranteed timing. Do not treat CI as proof of production-provider or external API readiness. Payment remains disabled and the live PHP flow is untouched.

- **Prior verified checkpoint (2026-10-13):** the payment branch’s code/test checkpoint `00927789eed4a206fc29b9888895502023780178` passed pipeline [2941850499](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2941850499), and the documentation-only checkpoint `f46e7607352840d9b0288a90e1da96ba08ec475f` passed pipeline [2942086092](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2942086092).
- Active implementation branch: `codex/audit-otp-ci-handoff-hardening` (based on `feat/payment-foundation-sadad-ready`).
- User reports local build/tests are green; the assistant does not execute tests on the user's machine.
- Historical CI note: pipeline [2940869925](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2940869925) for the earlier concurrency-hardening commit failed one API test because its ordering assumption was nondeterministic. The test was subsequently corrected to wait for the first persisted status before releasing the stale writer; the latest pipeline [2941850499](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2941850499) passed all five jobs.
- Pipeline `2933065509` for commit `d1b0e5d4` failed at `site_validate` before any project commands ran: the self-hosted Docker runner could not resolve `registry-1.docker.io` while pulling `alpine:3.20` (`runner_external_dependency_failure`). `php_validate` passed; .NET jobs were skipped. This is a runner/DNS/network dependency failure, not evidence of a code test failure: https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2933065509
- Earlier pipeline `2932874002` failed because `AuditComplianceTests.Migrations_ApplyToEmptyDatabase_AndCreateAuditImmutabilityTriggers` hard-coded the applied migration list and omitted `20261010100000_AddPaymentStatusUpdatedAtIndex`; the concurrent callback test itself passed.
- Added SQLite stale reconciliation cutoff-boundary and non-UTC-offset coverage, including equivalent strict-cutoff coverage for stale `Verifying` payments.

## Completed Payment Foundation Work
- Sadad adapter/configuration foundation, idempotency, conservative ambiguous-outcome handling, read-only operations endpoints.
- Operations endpoints require PaymentOperations authorization, limited to TechnicalSecurity and Administrator, and use Cache-Control: no-store.
- Payment service tests cover amount mismatch, missing provider reference, successful replay, and transport failure requiring reconciliation.
- Sadad timestamp request format uses invariant `MM/dd/yyyy h:mm:ss tt` based on legacy Shaparak VPG examples; verify against current merchant-specific integration pack before production.
- Stale payment queries are bounded in SQLite with SQL cutoff/order/limit and a composite Status+UpdatedAt index/migration.
- Added service-level concurrency test to ensure a second callback while Verify is in flight does not issue another Verify request.
- Added SQLite timestamp boundary tests for stale reconciliation and stale `Verifying` queries: timestamps strictly older than the cutoff are included; equal/newer timestamps are excluded; a non-UTC cutoff representing the same instant is normalized to UTC.
- Audit NDJSON export now explicitly sets `Cache-Control: no-store` before authorization/validation, so successful, forbidden, and invalid-range responses cannot be cached; tests cover those response paths.

## Live PHP Payment Boundary (User-Reported)
- The live PHP website and Sadad gateway flow are already working, including Sadad Verify after gateway return and recording the result.
- **Do not redo, replace, or modify the live website payment flow.** Sensitive/live PHP payment files remain outside GitLab and must stay private. Do not request or commit credentials, TerminalKey, raw secrets, or sensitive PHP code.
- The .NET payment foundation is separate from the live PHP flow. Do not activate it against live transactions until ownership is explicitly decided; PHP and .NET must not independently finalize the same transaction.

## Latest Work
- Follow-up test-only correction on `codex/audit-otp-ci-handoff-hardening`: the audit SQL test now filters EF logs to the executed-command entry after pipeline `2945622710` showed the previous assertion counted multiple diagnostic records. Await the corrected HEAD's pipeline before calling the added regression verified.
- The 2026-10-14 hardening commit moves SQLite audit-export filtering/order/limit and expired-OTP deletion into the database, adds SQL-shape and behavior regressions, and makes local/GitLab site and migration checks call shared scripts. Its pipeline is linked in Current Status; this entry records the expected run and must not be read as a pass until GitLab reports success.
- Organization-status refresh uses `ServiceRequest.UpdatedAt` as an EF Core concurrency token and returns HTTP 409 when a stale refresh loses the local write race. The controlled concurrent-refresh regression and model-snapshot metadata passed in pipeline `2941850499`; the documentation-only follow-up pipeline `2942086092` also passed all five jobs.
- Added a shared per-user/IP limit of 60 reads per minute to protected operational metrics and status endpoints, with regression coverage for 429 and no-store on rejection. This protects the status endpoint's database readiness probe from unbounded polling.
- Organization integration failures no longer forward adapter-supplied error text to the API response; responses use stable generic messages. Integration logs no longer include organization tracking IDs, reducing exposure of case-linked identifiers.
- Added a restrictive API Content-Security-Policy (`default-src 'none'`, no framing, no base URI, no form submissions) and expanded response-header tests to cover it on success and authorization failure.
- Logging/security-header hardening is present in the branch: exception logging is constrained to correlation ID and exception type rather than exception details, with regression coverage; response-header tests cover successful and authorization-failure paths. The current branch's full configured CI suite passed in pipeline `2942086092`.
- Fixed migration compliance test to assert required migrations without treating the list as permanently closed; explicitly checks the payment Status+UpdatedAt index migration.
- Added strict cutoff and UTC-offset regression coverage for stale `Verifying` recovery candidates.
- Hardened audit export against caching and added regression assertions for successful, forbidden, invalid date-range, and rate-limit rejection responses. Identity verification and OTP issue/verify responses set `Cache-Control: no-store`, with regression coverage for successful identity verification, invalid identifiers, OTP challenge issuance, and rejected OTP verification. A path-scoped middleware now also applies `no-store` to all `/identity/*` and `/service-requests/*` responses, including authorization failures; tests cover `/identity/me` and a forbidden service-request read.
- Corrected rate-limit partition keys to use authenticated user-id/name-identifier claims plus remote IP, instead of relying on `Identity.Name`.
- Bounded operational telemetry endpoint cardinality to 200 labels under concurrent traffic, preventing unique-path floods from bypassing the previous approximate count guard; total request aggregates remain accurate. Regression test confirms the global request count remains 2,000 while per-endpoint tracking stays at 200.

## Remaining / Release Gates
- Confirm exact Sadad timestamp format, TerminalKey encoding and encryption details against the current merchant-issued integration pack before production enablement of the separate .NET adapter.
- Real provider credentials and end-to-end tests are environment-dependent; never commit secrets.
- Define non-overlapping PHP/.NET payment ownership before production activation of .NET payment flow.
- Real Sana/Shahkar adapters, document integration, production security verification, and broader scaling/monitoring remain outside the current payment foundation.
- Audit NDJSON export has its own fixed-window rate limit (10 requests per minute per authenticated user/IP partition); rate-limit rejection responses also carry Cache-Control: no-store.
- OTP issue and verification share the five-requests-per-minute per-user/IP policy; tests now verify the shared limit and use a guaranteed non-numeric invalid code instead of relying on `000000`. Both service-request creation endpoints are limited to 10 requests per minute per authenticated user/IP partition, with a regression test proving the generic and S01 creation endpoints share one bucket and no-store is applied on 429. Identity verification has regression coverage for its five-requests-per-minute per-user/IP limit, separate user partitions, and no-store on 429 responses. The three policies (OTP issue/verify, identity verification, audit export) share the authenticated user-id/name-identifier plus remote-IP partition strategy. All three policies (OTP issue/verify, identity verification, audit export) derive partition identity from the authenticated user-id/name-identifier claim plus remote IP; do not rely on `ClaimsPrincipal.Identity.Name`, which is not populated by the development test handler.
- Keep ambiguous payment outcomes in reconciliation/manual review; never infer settlement from callback alone or automatically mark ambiguous transactions succeeded/failed.
- Do not expose gateway tokens/provider raw bodies from operational endpoints.
- Do not merge to master without explicit approval.

- Latest CI investigation: pipeline for commit 2c32d77e passed build, PHP/site validation, and migration verification; the rate-limit shared-bucket test passed. The test job had one unrelated pre-existing privacy test failure because it requested a random non-existent service-request ID (correct response is 404, not 403). The regression test was corrected to create an S01 request as one applicant and request it as a different applicant, asserting 403 plus Cache-Control: no-store. Recheck the next pipeline before treating CI as green.

## Workflow Rules
- Inspect current repository and history before changing files.
- User runs local build/tests and reports results; do not claim to have run them locally.
- The live PHP website payment flow is working and must not be reimplemented as part of .NET foundation work.
- Pushes trigger GitLab pipelines; avoid unnecessary commits and group related work.
- CI migration verification explicitly checks `20261010100000_AddPaymentStatusUpdatedAtIndex` so the required migration list matches the current schema. The local test/build result reported by the user remains green; latest GitLab pipeline status must be checked independently before calling CI green.
- CI build log identified and the branch fixed a compile-only test issue: `RateLimitingTests.cs` uses `IServiceProvider.CreateScope()` and imports `Microsoft.Extensions.DependencyInjection`. The service-request throttling regression test verifies the generic and S01 endpoints share the same ten-per-minute per-user/IP bucket and that a different user is not blocked by the first user's exhausted bucket. OTP issue/verify regression coverage verifies a second user has an independent five-per-minute bucket after the first user's shared bucket is exhausted.
- Update this file after significant work and record the latest branch/commit.

- Privacy/logging hardening: incoming `X-Correlation-Id` is now accepted only when it is 1–64 characters from the safe ASCII set [A-Za-z0-9._-]. Invalid, oversized, or potentially identifying values are replaced with a server-generated ID before being echoed or included in logs; middleware regression tests cover unsafe inputs.

## Latest CI (2026-10-10)
- Pipeline `2933884019` for commit `b3de4aa337ccbc3cacc8f3e96eb43cf3458cb797` failed only in `dotnet_test`; build, migration verification, PHP validation, and site validation passed: https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2933884019
- Failure diagnosis: two API tests still asserted the old mock-specific error strings, while the integration service now deliberately returns generic non-provider-revealing messages. Updated the assertions to match the generic public API contract. Operational rate-limit regression and privacy/security-header tests passed in this pipeline.
- Next: rerun CI, then continue with safe production-readiness hardening in bounded batches. Do not alter the live PHP/Sadad payment flow; do not merge to master without explicit approval.

- Follow-up privacy review found one remaining successful organization-status log that still included the tracking ID despite earlier redaction. Removed that field from the log and added a regression test using a distinctive sensitive tracking ID; no provider tracking ID should be emitted to application logs.

- Added Sadad Verify-path regression coverage for HTTP 502 and malformed JSON responses. The adapter returns stable error codes and does not propagate raw provider response bodies; these outcomes remain failures requiring conservative reconciliation behavior at the payment-service layer.

## CI and Repository Reconciliation (2026-10-11)
- Current branch HEAD at review: `812771c26e96a330790163dc45d39b8dc484fc71` (`test: cover Sadad verify transport and malformed responses`). The Sadad adapter, its tests, payment service tests, payment operational endpoints, and the recent security changes are present on this branch; they were confirmed by reading the branch tree and file contents.
- Pipeline `2933884019` did fail in `dotnet_test` (114 passed, 2 failed) because two API assertions still expected mock-specific organization error messages. Those assertions were updated in commit `5983219a11f6879d12a1c81ff4b59354fa44c19e`.
- Pipeline `2933971600` for commit `812771c26e96a330790163dc45d39b8dc484fc71` did not reach .NET build/tests: `php_validate` and `site_validate` failed while pulling Docker images due runner DNS/network/timeout errors; the .NET jobs were skipped. This is an infrastructure failure, not a code-test result.
- A fresh pipeline `2935201237` was created for the same branch HEAD to re-run the gates: https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2935201237. At the last check it was pending, with validation jobs pending and .NET jobs created; do not mark CI green until all required jobs finish successfully.
- The earlier “Latest CI” notes above are historical snapshots, not the current status. Always query GitLab before reporting current CI.
- User also reported that some items appear to have been recorded but are not present where expected. The branch tree confirms the recent commits and payment files exist in this repository branch, but the exact missing item/location has not yet been identified. Compare the expected UI/behavior against the actual branch and deployment target before assuming a commit is absent. Do not modify the live PHP/Sadad payment flow or merge to `master` without explicit approval.

- Payment operations hardening: `/ops/payments/*` now receives `Cache-Control: no-store` before authorization, including anonymous/forbidden responses, and both reconciliation-list endpoints use the shared 60-requests-per-minute per-user/IP operational-read policy. Regression assertions cover no-store on authorization failures. This protects sensitive operational metadata from caching and unbounded polling; the current branch still intentionally has no public create/verify payment route because service-request pricing/ownership and the PHP/.NET finalization boundary must be specified first.
- Follow-up test coverage: payment reconciliation listing now exercises the 60/minute limiter through the actual endpoint and verifies no-store on HTTP 429; audit export applies no-store before authorization so forbidden responses are covered too.


## CI and Follow-up Hardening (2026-10-11, latest review)
- Latest branch head at review: `4617b621840d58166b42cb0b505bbdd65cf1bd23` (`fix: simplify local CI PHP and test commands`).
- Latest pipeline `2935243125` is fully successful: `site_validate`, `php_validate`, `dotnet_build`, `dotnet_migration_verify`, and `dotnet_test` all passed. Pipeline: https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2935243125.
- The prior failure in pipeline `2933884019` was limited to `dotnet_test`; build, migration verification, PHP validation, and site validation passed. The failure was caused by two API assertions expecting old mock-specific organization error messages after the service had been changed to return generic redacted errors. The assertions were corrected and the newer pipeline is green.
- Reviewed repository tree against recent handoff notes: Sadad adapter, gateway tests, payment service, persistence migration/index, and operational endpoints are present on the active branch. This confirms repository presence only; it does not prove a separate deployed website or another branch has those files. The exact item the user says is recorded but missing is not yet identifiable; compare its expected path/location against this branch and the deployment target rather than assuming it was not committed.
- Additional logging review found that `AuditEventWriter` was writing the entire audit record to general application logs. Commit `30f023cb` removes full-object logging and retains only event type and outcome in general logs. A regression test is being added to prevent actor identifiers from leaking back into general logs.
- The latest successful CI trace also showed nullable warnings in `CorrelationIdMiddleware`; use an explicitly validated non-null local correlation ID so the compiler can prove the header and scope values are safe.
- Remaining release gates are unchanged: confirm Sadad crypto/timestamp wire contract against the current merchant integration pack; define exclusive PHP/.NET payment finalization ownership; production provider integrations, deployment security, monitoring/scaling/DR, and live end-to-end gateway verification remain incomplete. Never modify the working live PHP payment flow or merge to `master` without explicit approval.


## Repository Asset Path Reconciliation (2026-10-11)
- Tree audit found that `sabzroyanuraman-site/brokerage-details.html` referenced `/payment/آپ.png` and `/payment/سداد.png`, but the committed repository tree contains those image files at the site root (`sabzroyanuraman-site/آپ.png`, `sabzroyanuraman-site/سداد.png`) and has no tracked `payment/` directory. This was a concrete example of a recorded/reference path not matching the files actually committed.
- Corrected only those two HTML image references to the existing root assets. The legacy `/payment/asan-logo.png`, `/payment/sadad-logo.png` references and `/payment/order.php` flow were intentionally left unchanged because the live payment folder/PHP integration is user-reported as separately deployed and must not be rewritten or exposed in this repository.
- The exact other item(s) the user noticed as missing still need identification; this path audit found and corrected one verified mismatch without changing the live PHP payment flow.


## CI Parity and Asset Guard (2026-10-11)
- Latest confirmed branch head before this batch: `5641da6ee4a8ba0976faf46dd425e7664bcd175c`; pipeline `2935341881` passed all five required jobs (build, tests, migration verification, PHP validation, site validation): https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2935341881.
- The prior `dotnet_test` failure in pipeline `2933884019` was fixed by updating assertions to the intentionally redacted organization-integration error contract; the latest pipeline is fully green.
- A concrete “recorded but missing” mismatch was found and fixed: payment logos `آپ.png` and `سداد.png` are committed at the site root, not in a tracked `payment/` folder. `brokerage-details.html` now references the root assets; the separate deployed legacy paths `/payment/asan-logo.png`, `/payment/sadad-logo.png`, and PHP payment flow remain untouched.
- Follow-up: make both GitLab `site_validate` and `ci-local.ps1` assert the two committed logo files and matching references, so this path mismatch cannot silently recur. Also align local migration verification with GitLab CI by checking `20261008170000_AddPaymentTransactions` and `20261010100000_AddPaymentStatusUpdatedAtIndex` in addition to the earlier migrations.
- Current source tree is not proof of what is deployed on hosting or what exists on another branch. If a different recorded item is still missing, its exact name/path and expected location must be identified; do not assume it was deleted or modify the live PHP payment flow.


## Sadad Response-Boundary Hardening (2026-10-11)
- Added a 64 KiB maximum for Sadad create/verify response bodies. The adapter reads bounded chunks instead of unconditionally materializing arbitrary provider response bodies, and oversized responses produce the stable `GATEWAY_RESPONSE_TOO_LARGE` error without exposing provider content.
- Added regression tests for oversized create and verify responses. These are code-level changes only until the new GitLab `dotnet_build` and `dotnet_test` jobs pass; no local execution is claimed.
- This remains a conservative ambiguity outcome; it does not mark a payment settled or rejected and must flow through existing reconciliation behavior.

- Follow-up review of the response-boundary patch corrected the two result types/read paths before the next CI result: create oversize returns `PaymentGatewayCreateResult`, verify oversize returns `PaymentGatewayVerifyResult`, and both provider bodies use the bounded reader.


## Sadad Response Parsing and Timeout Hardening (2026-10-11)
- Guarded both Sadad create and verify response parsers against valid JSON whose root is not an object (for example arrays, strings, or `null`). These responses now return the stable `INVALID_GATEWAY_RESPONSE` result instead of allowing `JsonElement.TryGetProperty` to throw an unexpected `InvalidOperationException`.
- Added regression tests for all three non-object JSON root shapes in both create and verify paths.
- Configured the typed Sadad HTTP client with a 30-second timeout to bound time spent waiting on a stalled provider. Existing payment-service handling treats transport/time-out outcomes conservatively; no automatic retry or settlement was introduced.
- These changes are committed on `feat/payment-foundation-sadad-ready`; validate through the resulting GitLab pipeline and the user's local `.\ci-local.ps1` before treating the batch as complete.


## Payment Create Transport-Exception Regression (2026-10-11)
- Added an application-level regression test for a transport exception during Sadad payment creation: the already-persisted transaction remains Pending without a gateway token, the outcome is audited as requiring review, and replaying the same idempotency key does not call Sadad Create a second time.
- This complements the existing ambiguous HTTP/malformed-response tests and protects the uncertain-outcome path when HttpClient times out or throws before receiving a response.
- Await the newest GitLab pipeline and the user's local `.\ci-local.ps1` before marking the current batch verified.


## Payment Hardening Update (2026-10-11)
- Commit `5d1b744c` validates the exact Sadad callback token before returning an already-succeeded replay result, preventing payment-state disclosure to callers that do not possess the issued token; regression test added. Pipeline must be checked for this commit and later commits before considering it CI-verified.
- Next patch distinguishes a provider rejection with no explicit error code (`VERIFY_REJECTED`) from an actual amount mismatch, with regression coverage. This avoids misleading reconciliation reasons; provider outcomes still remain in manual reconciliation when not conclusively verified.
- Latest known completed pipeline for the previous commit is `2935632862` (commit `0712e0f4`); it passed. It does not validate newer commits.


## Sadad Follow-up Hardening (2026-10-11)
- Expanded payment-service regression coverage so HTTP errors, malformed provider JSON, oversized responses, and a successful provider code without a token all remain in an ambiguous/pending state and cannot trigger an automatic second Create call.
- Added adapter tests asserting the Verify request never includes the raw TerminalKey and that non-HTTPS provider request endpoints are rejected before any network request.
- CI must be checked against the newest branch head after these tests; do not treat the previous green pipeline as validating this batch. Merchant-specific encryption/timestamp details and sandbox E2E remain explicit release gates.


## Sadad Validation and CI Follow-up (2026-10-11)
- Fixed invalid C# escaping in the Sadad verify request assertions (commit `84a00c7a`); the previous pipeline failed compilation at those assertions, not because of gateway runtime behavior.
- Added parameterized regression coverage proving all four configured Sadad URLs (RequestUrl, VerifyUrl, PaymentUrl, CallbackUrl) must use HTTPS before any provider request is sent. The latest pipeline for `84a00c7a` is running; do not mark this batch green until its result is successful.
- Continue to keep live PHP payments untouched; Sadad merchant contract and sandbox behavior still require confirmation from the merchant-specific integration pack/environment.

- Commit `84a00c7a` corrected malformed string escaping that broke C# compilation in the Sadad verify tests. Commit `a28fcc71` adds parameterized HTTPS checks for all configured Sadad URLs. The latest pipeline for `a28fcc71` is pending at the time of this update.
- Added a no-auto-redirect policy for the Sadad typed HTTP client so signed payment request bodies are not silently forwarded to a redirect target. HTTP 3xx responses are handled as non-success provider responses and remain subject to conservative reconciliation rules.

- Latest Sadad batch (2026-10-11): fixed C# escaping in provider request assertions (`84a00c7a`); added HTTPS validation regression coverage for all four configured URLs (`a28fcc71`); disabled automatic HTTP redirects for the Sadad client to avoid forwarding signed payment payloads to redirect targets (`e3988efb`); added create/verify tests that 302 responses are not treated as successful gateway operations (`e2bfb1ba`). The current head pipeline must be green before this batch is considered validated.


## CI correction — 2026-10-11
- Pipeline `2935720459` failed in `PaymentServiceTests.CreateAsync_AmbiguousGatewayResponseRemainsPendingAndIsNotRetriedAutomatically`: `GATEWAY_RESPONSE_TOO_LARGE` was missing from `IsAmbiguousCreateOutcome`, so the transaction was marked Failed despite the provider outcome being unknown.
- Corrected the classification so oversized provider responses remain Pending for reconciliation and cannot trigger automatic duplicate creation. The existing theory case covers the regression. CI must rerun on the new commit before this fix is considered validated.
- The user also reported a C# syntax error at line 222 in `SadadPaymentGatewayTests.cs`; the current branch content at that line is syntactically valid. The current pipeline failure trace identifies the application test above instead, so inspect the exact commit/job associated with the user's separate compile log if that syntax error recurs.


## Consolidated Sadad CI correction (2026-10-11)
- The latest failed pipeline `2935720459` passed `dotnet_build` and `dotnet_migration_verify`; `dotnet_test` failed in the ambiguous-create theory for `GATEWAY_RESPONSE_TOO_LARGE` (expected Pending, observed Failed). The user separately reported a C# syntax error at SadadPaymentGatewayTests.cs line 222; the checked-in line is now syntactically valid and the current pipeline failure is a runtime assertion, not a compile error.
- Commit for this correction normalizes gateway error codes (trim + case-insensitive matching) before classifying ambiguous create outcomes and adds regression cases for casing/whitespace variants. This is deliberately conservative: oversized/truncated provider responses must remain Pending for reconciliation and must not invite a duplicate gateway Create.
- Run the latest branch pipeline and local `.\ci-local.ps1` before treating this batch as green. PHP live payment flow and `master` remain untouched.


## Current CI and Priority Shift (2026-10-12)
- Latest branch head: `e29c8b3f4917bb70bb23686c9c5207e89b760497` (`test: cover Sadad verification reconciliation on cancellation`).
- Pipeline `2938317613` completed successfully; all five required jobs passed: `site_validate`, `php_validate`, `dotnet_build`, `dotnet_migration_verify`, and `dotnet_test`. https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2938317613
- The payment foundation's current safety tests are green. Do not keep expanding payment work without a concrete blocker; leave `Payment:Enabled=false` and preserve the working live PHP flow.
- Priority is shifting to the core brokerage platform and regulatory readiness. See `docs/REGULATORY_COMPLIANCE_ROADMAP.md` for a source-grounded separation of software work, external integrations, licensing evidence, and unresolved regulatory interpretations.
- The official source text distinguishes a brokerage operating system from the existing informational/legacy website and separates the brokerage license prerequisites from the activity-permit prerequisites. Do not claim legal compliance from code or this roadmap alone.
- Production launch remains blocked by unconfigured approved organization/Sana/Shahkar adapters, production authentication, approved domestic hosting/deployment controls, and external approvals. Do not replace development mocks with guessed production protocols.
- Merge to `master` remains prohibited without explicit approval; review the complete branch diff first because it includes a wide range of website and application changes.


## Latest verified continuation (2026-10-12)
- Branch `feat/payment-foundation-sadad-ready` HEAD: `7f124dca64a5c2c4404b0491a9fc2bff791b9578`.
- Pipeline `2938398693` for that exact SHA completed successfully: `site_validate`, `php_validate`, `dotnet_build`, `dotnet_migration_verify`, and `dotnet_test` all passed. Pipeline: https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2938398693.
- `master` remains at `625c03dcadb47182e41edc62d74afd4c56e877e5`. Compare reports 118 commits and 53 changed files; do not merge without explicit user approval and a reviewed diff.
- A core workflow inventory was added in `docs/CORE_WORKFLOW_INVENTORY.md`. It describes existing routes and separates implemented application behavior from mocks and external approval blockers.
- Important review finding: `GET /service-requests/{id}/organization-status` currently performs a remote status lookup and writes organization status, completes/creates workflow stages, and writes audit events. This is a state-changing operation behind GET; assess and test idempotency/concurrency before changing its public method or behavior. Do not silently break clients.
- Payment remains disabled by default (`Payment:Enabled=false`). Keep the live PHP payment flow untouched. Do not infer service mappings/tariffs or Sadad merchant contract details.
- Next engineering batch should prioritize a focused review/test of organization-status workflow side effects and concurrency, then core service workflow authorization/data-minimization. Avoid expanding the payment foundation unless a concrete release-blocking defect is demonstrated.


## Latest core workflow fix (2026-10-12)
- Branch head: `4f23b9b8d794e1cdec2ad4a999c348e588997151` (`fix: persist generic service requests and initial stages`).
- Fixed `POST /service-requests`: the use case now persists the service request and its `INITIAL` workflow stage instead of returning a transient in-memory entity. The response also includes `CurrentWorkflowStageId`; undefined numeric service codes are rejected.
- Added API regression tests for retrieving the created generic request and its initial stage, and for rejecting an unsupported service code.
- Pipeline for this exact commit was queued at the last check: https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2938818686. Do not call the fix verified until the pipeline passes.
- Additional verified risks are recorded in `docs/CORE_WORKFLOW_INVENTORY.md`: organization submission retries lack a documented idempotency contract; S01 audit writes and local request persistence are not atomic; organization-status GET mutates state and still needs concurrent/partial-failure tests.
- Keep `Payment:Enabled=false`, leave the live PHP payment flow untouched, and do not merge to `master` without explicit approval.


## Verified core-workflow continuation (2026-10-12, latest check)
- Branch HEAD: `59b971df4a306a25c37461981c9be03cd1e1b302` (`test: update rate limiting request code`).
- Pipeline `2938914890` passed for that exact SHA. All five required jobs passed: `site_validate`, `php_validate`, `dotnet_build`, `dotnet_migration_verify`, and `dotnet_test`. https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2938914890
- The S01-specific route guard now prevents the generic `POST /service-requests` path from bypassing S01 identity verification and organization-submission workflow. The rate-limit regression test was updated to exercise the generic route with S02. New persistence and S01-bypass API tests passed in the successful pipeline.
- Next priority remains organization-status concurrency/partial-failure behavior and the S01 audit/persistence boundary. The route currently writes request/stage state and audit events from a GET; do not change the public HTTP method without client-compatibility review.
- Production submission retries remain unsafe to enable until the organization adapter's idempotency contract is documented. Do not invent request keys or external fields.
- `Payment:Enabled=false`; live PHP payment paths untouched; no merge to `master` without explicit approval.


## Latest verified core workflow and privacy batch (2026-10-12)
- Branch head before this batch: `23330b0f2f6bc543ab475f7497189652d679dafb`. Pipeline `2939200020` passed all five jobs (`site_validate`, `php_validate`, `dotnet_build`, `dotnet_migration_verify`, `dotnet_test`): https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2939200020.
- Organization status responses that report success without a non-empty status now fail closed; a regression test verifies the API does not advance the S01 workflow in that case.
- Follow-up privacy batch removes the organization-returned status value from general application logs and adds a marker-based regression test. The returned status remains available to the workflow caller.
- Compliance gap remains: repository evidence does not establish a deployed microservices architecture or a completed modern SPA frontend. Layered Domain/Application/Infrastructure code is not proof of microservice deployment. See `docs/REGULATORY_COMPLIANCE_ROADMAP.md`.
- Outstanding workflow risks: external organization submission retry/idempotency contract; atomic local persistence of request/stages/audit; and concurrent/partial-failure behavior of the state-changing organization-status GET. These require explicit design and targeted tests, not assumptions.
- Keep `Payment:Enabled=false`, preserve the live PHP payment flow, and do not merge to `master` without explicit approval. CI success is not production or legal readiness.


## Verified continuation — privacy and consistency review (2026-10-12)
- Current branch HEAD at this review: `5808352357792b0ff1ae14e623a2403fbdf785d9`. Pipeline `2939258054` passed all five configured jobs, including the status-log redaction regression test: https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2939258054.
- `OrganizationIntegrationService.GetStatusAsync` rejects a successful response with a missing/blank status and no longer logs the returned status value. Tests also assert that a tracking ID is not included in status-request logs.
- Review of the current persistence boundary confirms that `AuditEventWriter.WriteAsync` saves each event immediately, while S01 request and workflow-stage persistence occurs later. The S01 flow also performs the external organization submission before local persistence. This permits partial local audit state and a remote submission with no corresponding durable local request if a later step fails.
- The organization-status GET mutates request state, workflow stages and audit records. Its read/modify/write sequence is not protected by an explicit concurrency token or transaction boundary in the inspected path. Sequential repeat tests are not evidence of concurrency safety.
- Next safe step is a design-and-test batch for transaction boundaries, concurrency, and external-submit reconciliation. Do not introduce a new stage code, status enum, external idempotency field, or database uniqueness constraint until its business semantics and existing-data impact are confirmed.
- The returned organization status is persisted in the service request and included in audit state fields by the current workflow. Log redaction does not resolve data-minimization or retention requirements for those database fields; confirm the approved classification/retention policy before changing storage.
- Preserve `Payment:Enabled=false`, leave the live PHP payment flow untouched, and do not merge to `master` without explicit approval.

 
## Verified S01 local transaction follow-up — 2026-10-12
- Branch commit `530c87f97cdfbfe516adf769e3416a59df1b273a` passed pipeline `2939464325`: `site_validate`, `php_validate`, `dotnet_build`, `dotnet_migration_verify`, and `dotnet_test` all succeeded. https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2939464325
- S01 now wraps local service-request, workflow-stage and audit writes in a scoped EF transaction; the organization submission remains outside that local transaction because a local database rollback cannot undo a remote side effect. The API's `ServiceRequestCreated` audit event is now emitted within the use case before commit.
- The current test before this follow-up used a fake unit of work and only proved orchestration. A new SQLite-backed failure-injection test is being added to force failure after local request/stage rows are saved and assert that request, stage, and audit tables remain empty after rollback. Do not treat this stronger test as verified until its own pipeline passes.
- The remote-success/local-failure reconciliation and external submit idempotency contract remain release blockers. Organization-status GET now has an optimistic concurrency guard and HTTP 409 conflict path in code, but the new concurrent-refresh regression has not yet passed CI; provider-specific and client retry behavior remain unverified.
- Keep `Payment:Enabled=false`, do not change the live PHP payment flow, and do not merge to `master` without explicit approval.

 
## Organization-status local transaction follow-up — 2026-10-12
- The SQLite rollback regression test for S01 local creation passed with pipeline `2939513854`: all five configured jobs succeeded. https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2939513854
- The organization-status GET now starts a scoped EF transaction after the external status call and commits the request status, workflow-stage changes, and audit writes together. A regression test injects failure at the final `OrganizationStatusReceived` audit write and asserts the local status/stages/audit additions are rolled back. This new change must pass its own pipeline before it is considered verified.
- This transaction does not prove race-free concurrent refresh: the remote status fetch is outside the transaction, no optimistic concurrency token or stage uniqueness rule has been introduced, and provider-specific concurrent-write behavior still needs a dedicated test/design.
- Remote-success/local-failure on S01 submit still needs an approved reconciliation/idempotency policy. Keep payment disabled, PHP live payment untouched, and do not merge without explicit approval.


## Next status-refresh hardening batch — 2026-10-12
- Pipeline `2939580658` passed all five configured jobs for commit `51eac5102ffa98dce2cf542aa2757511826691dc`.
- Strengthen the existing sequential repeat-refresh test to assert audit behavior as well as stage count: every successful refresh produces a status-received audit event, while result-stage creation and follow-up completion occur once.
- Keep concurrent refresh marked unresolved. The request is loaded before the external call and transaction; there is no concurrency token or unique `(ServiceRequestId, StageCode)` constraint. Do not implement a speculative schema/locking change without provider and existing-data review.
- Keep `Payment:Enabled=false`, preserve the live PHP payment path, and do not merge without explicit approval.


## Latest verified CI and S01 external-side-effect boundary — 2026-10-13
- Pipeline [2941032895](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2941032895) passed all five configured jobs for commit `6c72b0ebd0f12e9081f90ef6578e2ede2ab512b5`: site validation, PHP validation, .NET build, migration verification, and .NET tests. This verifies the deterministic concurrent-refresh regression in that commit; it does not prove production-provider behavior or external API readiness.
- The SQLite S01 rollback regression now asserts the organization submission was invoked exactly once and returned a tracking identifier before the injected local persistence failure. It verifies local request/stage/audit rows roll back while the completed remote side effect cannot be rolled back by the local transaction; the latest pipeline passed.
- The external-success/local-failure and timeout-after-acceptance cases remain unresolved business/integration blockers. Do not add blind retries, invent external idempotency keys or query fields, or claim reconciliation is implemented without the authoritative organization API contract and an approved recovery process.
- Keep `Payment:Enabled=false`, leave the live PHP payment flow untouched, and do not merge to `master` without explicit approval.


## Latest verified pipeline and external-submit retry safety — 2026-10-13
- Pipeline [2941145121](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2941145121) passed all five jobs for commit `0bf0dab425a937dbd8697c044577e48121b8e0d9`, including .NET tests for the S01 remote-success/local-rollback failure window.
- Retry review found that the shared executor could retry S01 submissions on timeout/server/rate-limit errors when `MaxRetryCount` was configured above zero. Since the official organization API idempotency/correlation contract is not established here, an accepted submission with a lost response could be duplicated.
- The next change adds an explicit per-operation retry switch, disables automatic retries for submission, and retains configured retries for status reads. Regression tests now assert submission makes only one attempt for timeout/server/rate-limit failures while existing status-read retry tests remain in place. This change remains unverified until its own pipeline passes.
- Do not treat a timeout as proof the organization rejected the submission. Keep recovery manual/contract-driven until authoritative lookup/idempotency semantics are approved. Keep `Payment:Enabled=false`, the live PHP flow untouched, and do not merge without explicit approval.


## Latest verified retry-safety state — 2026-10-13
- Pipeline [2941232829](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2941232829) passed all five configured jobs for commit `0c8998db8d9b16943bc35ea927d0319c42d5a09e`, including .NET tests.
- `OrganizationIntegrationService.SubmitAsync` now explicitly disables automatic retries; timeout, server-error and rate-limit tests assert a single submission attempt. Read-only status lookup retains retry behavior for timeout/server-error/rate-limit cases.
- This does not resolve remote-success/local-failure or timeout-after-acceptance. No automated replay/reconciliation is claimed; obtain the authoritative organization API contract and approve a recovery procedure first.
- A follow-up test checks caller cancellation is propagated and is not converted into a retryable timeout. It is unverified until the next pipeline completes.
- Older sections above are historical snapshots; use this latest section and current source/tests for branch state. Keep `Payment:Enabled=false`, preserve the live PHP flow, and do not merge without explicit approval.


## CI failure and correction — 2026-10-13
- Pipeline [2941371827](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2941371827) failed in `dotnet_build` before test execution: the follow-up commit inserted a second `CancellationOrganizationApiClient` and a redundant cancellation test even though the file already contained both cancellation coverage and its helper.
- The corrective change removes only the duplicate test/helper, preserving the pre-existing `SubmitAsync_WhenCancellationIsRequested_PropagatesCancellation` regression test. The existing cancellation test remains the source of truth; no runtime behavior change is intended.
- The next pipeline must verify the correction. The previous failed pipeline is not evidence that the cancellation behavior failed; it exposed compile-time duplicate definitions.


## Caller-cancellation retry guard — 2026-10-13
- Pipeline `2941486180` passed all five configured jobs for commit `a066667beba65b9140e50fa74a71a2023a966692`: site validation, PHP validation, .NET build, migration verification, and .NET tests.
- The next focused hardening change makes `OrganizationRetryExecutor` check the caller token after a failed operation result and before deciding whether to retry. This covers adapters that return a failure result despite caller cancellation rather than throwing `OperationCanceledException`.
- A regression test cancels the caller token inside an adapter that returns a transient server-error result and asserts cancellation propagates after exactly one attempt. This change remains unverified until the new commit's pipeline passes.
- Submission retries remain disabled; status-read retry coverage remains. Remote-success/local-failure reconciliation still requires the official organization API contract and an approved recovery procedure. Keep `Payment:Enabled=false`, leave live PHP payment untouched, and do not merge without explicit approval.


## Persist known remote submission despite caller cancellation — 2026-10-13
- Pipeline `2941591856` passed all five jobs for commit `239cd8216fc11f1c49086cd7ffe50ba2e291d6ac`, including .NET build, migration verification and tests.
- Follow-up hardening addresses a specific cancellation window: after organization submission returns a tracking ID, request cancellation must not cancel the local transaction/repository save/commit that records this known remote side effect. A SQLite-backed regression cancels the caller token immediately after the external adapter returns, then asserts the request, three stages, and seven audit events are persisted.
- This reduces one avoidable orphan-case path; it does not protect against process crashes, database failures, or timeouts with unknown remote acceptance. Those still require the authoritative organization API contract and an approved reconciliation procedure.
- Verify this change in its own pipeline. Keep `Payment:Enabled=false`, leave live PHP payment untouched, and do not merge without explicit approval.


## Latest branch checkpoint — 2026-10-13
- Exact branch HEAD: `f46e7607352840d9b0288a90e1da96ba08ec475f` (`docs: reconcile current workflow risks and CI evidence`). Pipeline [2942086092](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2942086092) passed all five configured jobs: `site_validate`, `php_validate`, `dotnet_build`, `dotnet_migration_verify`, and `dotnet_test`.
- This latest commit is documentation-only. The code behavior described above was already covered by pipeline `2941850499` for commit `00927789eed4a206fc29b9888895502023780178`; the current pipeline revalidated the exact latest branch commit.
- Open release boundary: the external organization submit and local database commit are not atomic. A process crash, local database failure after remote acceptance, or timeout with unknown remote acceptance still requires the authoritative organization API contract and an approved reconciliation procedure. Do not invent keys, headers, statuses, or retry semantics.
- Keep `Payment:Enabled=false`, preserve the live PHP payment flow, and do not merge without explicit approval.


## Release blockers reviewed — 2026-10-13
- Migration verification is being hardened to compare all timestamped migration source files with EF-discovered migration IDs, not just a fixed list. The CI result for the commit containing that check is the required verification.
- Website display: the hard-coded 5,000-toman amount label in `sabzroyanuraman-site/brokerage-details.html` was replaced with tariff-based wording, and `site_validate` checks that the fixed display does not return. This does not prove backend amount correctness: the form posts to `/payment/order.php`, which is not tracked in this repository tree.
- Sadad remains blocked on the current merchant-specific integration contract, approved service-code/tariff mapping, and authorized sandbox evidence. Keep `Payment:Enabled=false`; do not modify the live PHP handler or activate .NET payment in parallel.
- ZDR, production database/provider, persistent Data Protection keys and secret-manager operations, production Sana/Shahkar/organization adapters, distributed rate limiting, WMS/WFS, complete frontend, SIEM/DR, and independent SAST/DAST/penetration testing remain open evidence gates. Do not represent these as completed from CI alone.


## Continuation checkpoint — 2026-10-13 (after pipeline 2942405294)
- Branch head at this checkpoint is `ffcddc69bdc65f4e96b4406f9e27ca1760697fab`; pipeline [2942405294](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2942405294) completed with status **success**. All five configured jobs passed: `site_validate`, `php_validate`, `dotnet_build`, `dotnet_migration_verify`, and `dotnet_test`.
- Migration verification is now CI-verified on that exact commit: required baseline migration IDs are present, and all timestamped migration source files are compared against EF-discovered migration IDs. This proves discovery consistency in CI, not that a production database has been migrated or restored.
- The site amount-display check and PHP syntax validation passed. The display no longer advertises a fixed 5,000-toman amount; this does not validate the absent deployed `/payment/order.php` handler.
- Do not make a new commit until the current pipeline is terminal; it is now terminal and successful. Continue in coherent batches and run one pipeline after each batch.
- Still blocked by external evidence/architecture decisions: merchant-specific Sadad contract and authorized sandbox, approved service/tariff mapping, deployed PHP handler review and payment ownership decision, production database provider and migration/restore procedure, secret manager/persistent key ring/rotation, distributed rate limiter provider, official Sana/Shahkar/organization contracts, ZDR data-flow evidence, official WMS/WFS, completed frontend, SIEM/DR, and independent SAST/DAST/penetration testing.
- Keep `Payment:Enabled=false`. Do not merge. Never claim compliance or production readiness from green CI alone.


## Repository secret hygiene batch — 2026-10-13
- Added a pinned Gitleaks directory scan to the required CI pipeline. It scans the checked-out working tree for detectable secret patterns; it does not scan full Git history, deployment hosts, CI variable stores, backups, or external secret managers.
- Root .gitignore now excludes common local environment files (while allowing .env.example), SQLite database/WAL files, PFX/P12/key files, secrets directories, and Data Protection key directories. Ignore rules prevent accidental addition but do not remove already tracked files or revoke exposed credentials.
- Treat scanner findings as security incidents to investigate. Verify context without printing secret values into logs; revoke/rotate any credential confirmed exposed and scan history separately.
- Keep production credentials out of repository configuration. Keep Payment:Enabled=false; no merge or production-readiness claim follows from this control alone.


## Secret-scan triage and GPS viewer containment — 2026-10-13
- The first enforced Gitleaks run on commit `eefc1ee5cb96b60b5a671f030f21a29ca90b8c81` failed with three generic-api-key findings: a shared GPS live-view key duplicated in the API and public viewer page, plus a MapTiler-style tile-service key in the GIS page. Finding locations were inspected with redaction; raw values were not copied into the handoff.
- The shared GPS viewer key was embedded in client-delivered HTML and therefore could not be treated as a secret. As immediate containment, the live GPS read action now fails closed with HTTP 503 and the public viewer no longer requests or renders live coordinates until server-side authenticated access/authorization is implemented. GPS ingestion actions were not intentionally changed by this containment.
- The keyed MapTiler tile layer was removed from the GIS page; the other map layers remain. This removes the in-repository key but does not revoke it at the provider. If the key is a real provider credential, rotate/restrict it in the provider console and review usage.
- Gitleaks runs with redacted verbose findings so file/line/rule details can be triaged without printing raw secret values. A green scan is still limited to detectable patterns in the current tree, not historical commits or deployment secrets.
- This is a privacy containment, not a completed GPS authentication feature. Do not re-enable live location output until session-based authentication, authorization, abuse controls, and regression tests are reviewed.
