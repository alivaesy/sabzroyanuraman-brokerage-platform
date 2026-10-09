# AI HANDOFF

## Project
Brokerage Platform / Sabz Royan Orouaman

## Purpose
This file is the shared handoff point between different ChatGPT accounts working on this repository. GitLab is the source of truth.

## Current Status
- Active development on the payment foundation branch.
- User reports local build and tests are green; this is user-reported and was not executed by the assistant.
- Pipeline 2930344778 for SHA 642a7679 initially had a runner-system failure pulling `mcr.microsoft.com/dotnet/sdk:10.0`; retry job 17059397567 succeeded.
- Pipeline 2930346858 for SHA 34e7912f completed successfully with all five jobs green.
- Pipeline 2930436428 for SHA e0287bfe completed successfully with all five jobs green.

## Current Branch
feat/payment-foundation-sadad-ready

## Latest Known Commit
e0287bfe44c4969c5ecebf0b169fa3c8eca2cc69 — test: cover payment verification mismatch and replay paths

## Completed Work Relevant To Current Track
- Sadad payment gateway adapter foundation and configuration validation.
- Idempotent payment transaction persistence and conservative handling of ambiguous gateway outcomes.
- Read-only operational endpoints for stale verification and reconciliation candidates.
- Payment operations endpoints require PaymentOperations authorization; only TechnicalSecurity and Administrator roles are authorized.
- Operational payment endpoint responses set Cache-Control: no-store before validation or database work.
- Reconciliation candidate query test includes ReconciliationRequired state.
- Payment service tests cover verify amount mismatch, missing reference, and replay of an already-succeeded transaction.
- Sadad adapter request timestamp format aligned to the legacy Shaparak VPG format `MM/dd/yyyy h:mm:ss tt`, with culture-invariant formatting and a regression test.
- Sadad adapter tests cover missing verification reference and provider rejection.

## Currently In Progress
- Payment Foundation: callback concurrency/replay safety and operational reconciliation behavior.

## Remaining Tasks
- Review duplicate/concurrent callback behavior and interrupted verification.
- Confirm timestamp timezone and exact wire format against the merchant's current official Sadad integration pack before production enablement.
- Confirm TerminalKey encoding/encryption details against the merchant's issued credentials and official contract; never commit secrets.
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
- src/Brokerage.Infrastructure/Payments/SadadPaymentGateway.cs
- tests/Brokerage.Api.Tests/SadadPaymentGatewayTests.cs
- tests/Brokerage.Application.Tests/PaymentServiceTests.cs
- docs/AI_HANDOFF.md

## Next Recommended Step
Review atomic callback/verification claims against the repository implementation and tests. Then design bounded/indexed stale-candidate queries without changing ambiguous-payment safety behavior.

## Important Rules
- Inspect the current repository before making changes.
- Do not overwrite or revert existing work without checking Git history.
- Do not change architecture unnecessarily.
- Group related edits into one commit when practical.
- Run relevant tests after changes.
- Update this file after completing a significant task.
- Record the latest commit and branch here.
