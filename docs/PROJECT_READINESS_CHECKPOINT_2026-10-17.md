# Project Readiness Checkpoint — 2026-10-18

## Live branch and CI status

- Working branch: `codex/project-readiness-baseline`.
- Current HEAD at this checkpoint update: `0203dde2504a41326af747ecbd300529c9baedfd` (`fix: require transactional audit for generic requests`).
- Pipeline [2959886605](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2959886605): **success** for that exact SHA. Pipeline [2959878699](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2959878699): **success** for parent `5c3b38e5`.
- Pipeline [2959874595](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2959874595) failed during NuGet restore due to `api.nuget.org` download timeouts; evidence points to package retrieval, not a reported source compile/test failure. Pipeline `2959875562` was canceled after a later push superseded it.
- `master` remains at `54e224d8b09eb9dbd882a0ba61e97d153af0136b`; no merge has been made. The branch is not merged.
- These results describe the exact commits listed; later changes require their own CI evidence. Work may continue without waiting for CI, but test success must not be inferred.

## Verified engineering progress

- Generic `POST /service-requests` now requires audit writer and unit of work and commits request, initial stage and audit events in one local transaction. Regression coverage includes SQLite persistence and rollback on audit failure.
- Persistent OTP rejects blank/incomplete inputs before database mutations; regression tests were added.
- Workflow stage identifiers are trimmed and completion timestamps remain stable on repeated completion.
- Identity error responses have `Cache-Control: no-store` regression coverage.
- S01 workflow tests cover local rollback after persistence failure and preservation of a known remote tracking ID after caller cancellation.

## Immediate engineering gap

`CreateS01ServiceRequest` still declares its repository, audit writer and unit of work as optional. API dependency injection currently registers these services, but direct/manual construction can silently skip durable persistence, transaction and persisted audit. This is inconsistent with the hardened generic request use case. The next code step is to make those dependencies mandatory, adapt test fixtures, and retain tests for remote submission followed by local failure. The organization accepts the remote submission outside the local transaction, so local rollback cannot undo remote success.

## Regulatory and production gates

The supplied `الزامات_کارگزاری.html` requires evidence beyond CI, including at least three services built against their own approved requirements for the relevant gate (subject to the stated exception), approved-lab penetration testing/software technical approval, approved domestic hosting, direct legal-entity payment gateway, uptime evidence, read-only regulator dashboard and organizational support/contractual evidence.

The repository currently shows S01 as the only dedicated service-specific creation route. Generic creation does not prove S02–S05 are complete. Obtain approved service requirements before inventing stages, fields or validations.

## Prioritized continuation

| Priority | Workstream | Required outcome |
|---|---|---|
| Immediate | S01 reliability | Make persistence, audit and transaction dependencies mandatory; retain rollback and cancellation regressions. |
| Immediate | Payment safety | Keep `Payment:Enabled=false`; complete `docs/PAYMENT_SERVICE_MAPPING_APPROVAL.md` from authoritative tariff and Sadad merchant documents. |
| Immediate | PHP/.NET boundary | Website points to `/payment/order.php`, which is absent from this repository. Do not touch the live PHP path or allow two implementations to finalize one transaction. |
| High | Service acceptance | Map at least three approved service requirements end-to-end; do not treat generic creation as service completion. |
| High | Deployment | Approve production database, multi-instance concurrency, migrations, backups/restoration and recovery runbook. SQLite is currently registered. |
| High | Security/privacy | Evidence for approved penetration test, ZDR/data-flow and retention, secret management, logs/telemetry/export handling. |
| Next | Operations | Shared rate limiting, production monitoring/SIEM, alerts, read-only regulator dashboard and tested disaster recovery. |
| External gate | Integrations | Obtain versioned official identity/organization contracts, approved adapters and authorized sandbox evidence. Development mocks are not production proof. |

## Boundaries

- Do not change live PHP payment handling, credentials, secrets or production transactions.
- Do not enable .NET payments until the merchant contract, tariff approval, sandbox tests, single-owner transaction routing and operations checklist are signed off.
- Do not invent external API behavior or business workflow rules.
- Keep grouped changes on the working branch; no merge to `master` without explicit authorization.
