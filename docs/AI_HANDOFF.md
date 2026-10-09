# AI HANDOFF

## Project
Brokerage Platform / Sabz Royan Orouaman

## Purpose
This file is the shared handoff point between different ChatGPT accounts working on this repository. GitLab is the source of truth.

## Current Status
- Active development on the payment foundation branch.
- User reports local build and tests are green; this is user-reported and was not executed by the assistant.
- Pipeline 2930344778 for SHA 642a7679 had a runner-system failure in `dotnet_test` while pulling `mcr.microsoft.com/dotnet/sdk:10.0` due DNS/network resolution. This was infrastructure failure, not a test assertion failure. The failed job was retried as job 17059397567 and was pending at last check.
- Pipeline 2930346858 for SHA 34e7912f: `site_validate`, `php_validate`, `dotnet_build`, and `dotnet_migration_verify` succeeded; `dotnet_test` was running at last check. Recheck before treating the pipeline as green.

## Current Branch
feat/payment-foundation-sadad-ready

## Latest Known Commit
34e7912f12721e849f37f6909f0b5cbb8ea2feb2 — test: cover reconciliation-required payment candidates

## Completed Work Relevant To Current Track
- Sadad payment gateway adapter foundation and configuration validation.
- Idempotent payment transaction persistence and conservative handling of ambiguous gateway outcomes.
- Read-only operational endpoints for stale verification and reconciliation candidates.
- Payment operations endpoints require PaymentOperations authorization; only TechnicalSecurity and Administrator roles are authorized.
- Operational payment endpoint responses set Cache-Control: no-store before validation or database work.
- Reconciliation candidate query test includes ReconciliationRequired state.

## Currently In Progress
- Payment Foundation: verification failure paths, callback replay safety, and Sadad adapter contract validation.

## Remaining Tasks
- Add tests for amount mismatch and missing gateway reference on Verify, plus successful replay not calling the gateway again.
- Review duplicate/concurrent callback behavior and interrupted verification.
- Validate Sadad request timestamp/signature format against the provider's current official contract before production enablement.
- Real provider credentials and end-to-end gateway tests remain environment-dependent; never commit secrets.
- Real Sana/Shahkar adapters, document integration, production security verification, and microservices separation remain broader project gaps.

## Known Errors / Issues
- SQLite DateTimeOffset ordering/comparison is handled by loading status-filtered records and filtering timestamps in memory; this can become unbounded as payment volume grows and needs a deliberate indexed storage/query design.
- The local CI script is ci-local.ps1; the user runs local build/tests and reports the result. Do not claim local tests passed unless confirmed by the user.
- GitLab pipelines are triggered automatically by pushes in this repository. Avoid unnecessary commits; group related edits into one commit when practical.

## Important Architecture Decisions
- Keep ambiguous gateway outcomes in reconciliation/manual review; do not infer settlement from a callback alone.
- Do not automatically mark an ambiguous payment succeeded or failed.
- Do not expose gateway tokens or raw provider response bodies in operational endpoints.
- Do not merge to master without explicit approval.

## Files Recently Changed
- src/Brokerage.Api/Endpoints/PaymentOperationalEndpoints.cs
- tests/Brokerage.Api.Tests/PaymentOperationalEndpointAuthorizationTests.cs
- tests/Brokerage.Api.Tests/PaymentPersistenceTests.cs
- tests/Brokerage.Application.Tests/PaymentServiceTests.cs
- docs/AI_HANDOFF.md

## Next Recommended Step
After the current CI jobs finish, verify the payment-service tests. Then inspect the Sadad adapter timestamp/signature contract and design bounded/indexed stale-candidate queries without changing ambiguous-payment safety behavior.

## Important Rules
- Inspect the current repository before making changes.
- Do not overwrite or revert existing work without checking Git history.
- Do not change architecture unnecessarily.
- Group related edits into one commit when practical.
- Run relevant tests after changes.
- Update this file after completing a significant task.
- Record the latest commit and branch here.
