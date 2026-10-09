# AI HANDOFF

## Project
Brokerage Platform / Sabz Royan Orouaman

## Purpose
This file is the shared handoff point between different ChatGPT accounts working on this repository. GitLab is the source of truth.

## Current Status
- Active development; do not assume a green local build unless the user confirms it.
- Last confirmed green GitLab pipeline before the latest commits: pipeline 2929715203, SHA f19103e12004af9714fad62fff701c3ce4e40339.
- Pipeline was not triggered for the subsequent commits; their CI status is not yet confirmed.

## Current Branch
feat/payment-foundation-sadad-ready

## Last Known Green Pipeline Commit
f19103e12004af9714fad62fff701c3ce4e40339

## Completed Work Relevant To Current Track
- Sadad payment gateway adapter foundation and configuration validation.
- Idempotent payment transaction persistence and conservative handling of ambiguous gateway outcomes.
- Read-only operational endpoints for stale verification and reconciliation candidates.
- Payment operations endpoints require PaymentOperations authorization; only TechnicalSecurity and Administrator roles are authorized.
- Operational payment endpoint responses set Cache-Control: no-store before validation or database work.

## Currently In Progress
- Payment Foundation: operational reconciliation safety, endpoint coverage, and Sadad adapter validation.

## Remaining Tasks
- Continue reviewing payment state transitions and gateway ambiguity/recovery paths.
- Add reconciliation behavior tests, especially duplicate/concurrent callbacks and interrupted verification.
- Validate Sadad request timestamp/signature format against the provider's current official contract before production enablement.
- Real provider credentials and end-to-end gateway tests remain environment-dependent; never commit secrets.
- Real Sana/Shahkar adapters, document integration, production security verification, and microservices separation remain broader project gaps.

## Known Errors / Issues
- SQLite DateTimeOffset ordering/comparison is handled by loading status-filtered records and filtering timestamps in memory; this can become unbounded as payment volume grows and needs a deliberate indexed storage/query design.
- The local CI script is ci-local.ps1; the user runs local build/tests and reports the result. Do not claim local tests passed unless confirmed.
- Do not trigger GitLab pipelines unless the user requests it.

## Important Architecture Decisions
- Keep ambiguous gateway outcomes in reconciliation/manual review; do not infer settlement from a callback alone.
- Do not automatically mark an ambiguous payment succeeded or failed.
- Do not expose gateway tokens or raw provider response bodies in operational endpoints.
- Do not merge to master without explicit approval.

## Files Recently Changed
- src/Brokerage.Api/Endpoints/PaymentOperationalEndpoints.cs
- tests/Brokerage.Api.Tests/PaymentOperationalEndpointAuthorizationTests.cs
- tests/Brokerage.Api.Tests/PaymentPersistenceTests.cs
- docs/AI_HANDOFF.md

## Next Recommended Step
Review Sadad timestamp/signature compatibility and add tests for ambiguous verification outcomes and duplicate callback/replay behavior; later address bounded/indexed stale-candidate querying.

## Important Rules
- Inspect the current repository before making changes.
- Do not overwrite or revert existing work without checking Git history.
- Do not change architecture unnecessarily.
- Group related edits into one commit when practical.
- Run relevant tests after changes.
- Update this file after completing a significant task.
- Record the latest commit and branch here.
