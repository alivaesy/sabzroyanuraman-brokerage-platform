# AI HANDOFF

## Project
Brokerage Platform / Sabz Royan Orouaman

## Purpose
Shared handoff between ChatGPT accounts; GitLab is the source of truth.

## Current Status
- Active branch: `feat/payment-foundation-sadad-ready`.
- User reports local build/tests are green; the assistant does not execute tests on the user's machine.
- Latest code commit: `18fb559123cd7e4cf40a50630fb8a256c4282b7b` (`fix: import dependency injection extensions in rate limit tests`). Pipeline `2933363691` was pending at last check after the compile fix: https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2933363691.
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
- CI build log identified and the branch fixed a compile-only test issue: `RateLimitingTests.cs` uses `IServiceProvider.CreateScope()` and imports `Microsoft.Extensions.DependencyInjection`. The service-request throttling regression test also verifies that the generic service-code endpoint and S01 endpoint share the same ten-per-minute per-user/IP bucket.
- Update this file after significant work and record the latest branch/commit.
