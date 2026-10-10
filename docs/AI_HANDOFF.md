# AI HANDOFF

## Project
Brokerage Platform / Sabz Royan Orouaman

## Purpose
Shared handoff between ChatGPT accounts; GitLab is the source of truth.

## Current Status
- Active branch: `feat/payment-foundation-sadad-ready`.
- User reports local build/tests are green; the assistant does not execute tests on the user's machine.
- Pipeline `2932874002` failed because `AuditComplianceTests.Migrations_ApplyToEmptyDatabase_AndCreateAuditImmutabilityTriggers` hard-coded the applied migration list and did not include `20261010100000_AddPaymentStatusUpdatedAtIndex`; the concurrent callback test itself passed.
- Latest pipeline to check before the next commit: https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2932891700
- Added SQLite stale reconciliation cutoff-boundary and non-UTC-offset coverage; extending equivalent strict-cutoff coverage to stale `Verifying` payments.

## Completed Payment Foundation Work
- Sadad adapter/configuration foundation, idempotency, conservative ambiguous-outcome handling, read-only operations endpoints.
- Operations endpoints require PaymentOperations authorization, limited to TechnicalSecurity and Administrator, and use Cache-Control: no-store.
- Payment service tests cover amount mismatch, missing provider reference, successful replay, and transport failure requiring reconciliation.
- Sadad timestamp request format uses invariant `MM/dd/yyyy h:mm:ss tt` based on legacy Shaparak VPG examples; verify against current merchant-specific integration pack before production.
- Stale payment queries are bounded in SQLite with SQL cutoff/order/limit and a composite Status+UpdatedAt index/migration.
- Added service-level concurrency test to ensure a second callback while Verify is in flight does not issue another Verify request.
- Added SQLite timestamp boundary tests for stale reconciliation and stale `Verifying` queries: timestamps strictly older than the cutoff are included; equal/newer timestamps are excluded; a non-UTC cutoff representing the same instant is normalized to UTC.

## Live PHP Payment Boundary (User-Reported)
- The live PHP website calls Sadad Verify after gateway return and records the result.
- Sensitive/live PHP payment files remain outside GitLab and must stay private. Do not request or commit credentials, TerminalKey, raw secrets, or sensitive PHP code.
- Do not modify the live PHP path unless explicitly requested.
- Before production activation of .NET payment flow, define which component owns final verification/recording. PHP and .NET must not independently finalize the same transaction.

## Latest Work
- Fixed migration compliance test to assert required migrations without treating the list as permanently closed; explicitly checks the payment Status+UpdatedAt index migration.
- Added strict cutoff and UTC-offset regression coverage for stale `Verifying` recovery candidates.

## Remaining / Release Gates
- Confirm exact Sadad timestamp format, TerminalKey encoding and encryption details against the current merchant-issued integration pack before production enablement.
- Real provider credentials and end-to-end tests are environment-dependent; never commit secrets.
- Define non-overlapping PHP/.NET payment ownership before production activation.
- Real Sana/Shahkar adapters, document integration, production security verification, and broader scaling/monitoring remain outside the current payment foundation.
- Keep ambiguous payment outcomes in reconciliation/manual review; never infer settlement from callback alone or automatically mark ambiguous transactions succeeded/failed.
- Do not expose gateway tokens/provider raw bodies from operational endpoints.
- Do not merge to master without explicit approval.

## Workflow Rules
- Inspect current repository and history before changing files.
- User runs local build/tests and reports results; do not claim to have run them locally.
- Pushes trigger GitLab pipelines; avoid unnecessary commits and group related work.
- Update this file after significant work and record the latest branch/commit.
