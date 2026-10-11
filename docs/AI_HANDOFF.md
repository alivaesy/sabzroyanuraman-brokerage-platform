# AI HANDOFF

## Project
Brokerage Platform / Sabz Royan Orouaman

## Purpose
Shared handoff between ChatGPT accounts; GitLab is the source of truth.

## Current Status
- Active branch: `feat/payment-foundation-sadad-ready`.
- User reports local build/tests are green; the assistant does not execute tests on the user's machine.
- Latest confirmed GitLab pipeline before the next hardening batch: pipeline `2933610985` for commit `ef316878e2e07e4b1c28b756e00e7604c3d9bf8d` (`test: verify OTP rate limit isolation by user`) completed successfully. `dotnet_build`, `dotnet_test`, `dotnet_migration_verify` (successful retry), `php_validate`, and `site_validate` all passed. The first migration-verification attempts failed from runner network/DNS/TLS issues; the successful retry completed the gate: https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2933610985.
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
- Added a shared per-user/IP limit of 60 reads per minute to protected operational metrics and status endpoints, with regression coverage for 429 and no-store on rejection. This protects the status endpoint's database readiness probe from unbounded polling.
- Organization integration failures no longer forward adapter-supplied error text to the API response; responses use stable generic messages. Integration logs no longer include organization tracking IDs, reducing exposure of case-linked identifiers.
- Added a restrictive API Content-Security-Policy (`default-src 'none'`, no framing, no base URI, no form submissions) and expanded response-header tests to cover it on success and authorization failure.
- Next hardening batch: exception logs record correlation ID and exception type only, not the exception object/message/stack; regression test asserts exception details are not emitted to the application logger. Added regression coverage for security headers on both successful and authorization-failure responses.
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
