# AI HANDOFF

## Project
Brokerage Platform / Sabz Royan Orouaman

## Purpose
Shared handoff between ChatGPT accounts; GitLab is the source of truth.

## Current Status
- Active branch: `feat/payment-foundation-sadad-ready`.
- User reports local build/tests are green; the assistant does not execute tests on the user's machine.
- Latest commit before this batch: `41634a1e278a7ca2800e57b6aa44e39c7a9ff4c9` (`test: fix migration assertion and cover stale verifying cutoff`).
- Pipeline `2932945351` for that commit was still running / queued when checked; do not report it as green until GitLab confirms completion: https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2932945351
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
- Hardened audit export against caching and added regression assertions for successful, forbidden, and invalid date-range responses.

## Remaining / Release Gates
- Confirm exact Sadad timestamp format, TerminalKey encoding and encryption details against the current merchant-issued integration pack before production enablement of the separate .NET adapter.
- Real provider credentials and end-to-end tests are environment-dependent; never commit secrets.
- Define non-overlapping PHP/.NET payment ownership before production activation of .NET payment flow.
- Real Sana/Shahkar adapters, document integration, production security verification, and broader scaling/monitoring remain outside the current payment foundation.
- Rate-limit audit export separately from OTP/identity verification before production exposure; keep its response limits bounded.
- Keep ambiguous payment outcomes in reconciliation/manual review; never infer settlement from callback alone or automatically mark ambiguous transactions succeeded/failed.
- Do not expose gateway tokens/provider raw bodies from operational endpoints.
- Do not merge to master without explicit approval.

## Workflow Rules
- Inspect current repository and history before changing files.
- User runs local build/tests and reports results; do not claim to have run them locally.
- The live PHP website payment flow is working and must not be reimplemented as part of .NET foundation work.
- Pushes trigger GitLab pipelines; avoid unnecessary commits and group related work.
- Update this file after significant work and record the latest branch/commit.
