# Sadad Payment Rollout Checklist

## Current safety posture

- [x] Payment initiation is disabled by default with `Payment:Enabled=false`.
- [x] The .NET payment API loads amounts from server-side tariff configuration; client-submitted amounts are not trusted. This does not certify the separate legacy PHP payment handler.
- [x] Payment creation requires Applicant authorization, recent MFA, request ownership, an idempotency key, a configured tariff, and valid gateway configuration.
- [x] Callback tokens are treated as untrusted correlation values; a successful browser return is not proof of payment.
- [x] Verification is server-to-server and ambiguous outcomes are left pending or sent to reconciliation rather than automatically retried.
- [x] Callback responses use no-store and restrictive security headers.
- [x] Operational reconciliation endpoints are protected by the PaymentOperations policy.
- [x] The live PHP payment flow is out of scope for this .NET rollout and must not be modified or run in parallel against the same transaction.

## Required gates before enabling payment initiation

- [ ] Confirm the current Sadad merchant integration guide and validate request, verify, callback, signing/encryption, date format, and response fields against the merchant's actual environment.
- [ ] Confirm the authoritative mapping from website service/subservice identifiers to API `ServiceCode` values (`S01`–`S05`). Do not infer this mapping from labels or old form values.
- [ ] Obtain an approved tariff for every billable service from the authoritative business source. Record the amount and unit explicitly; the API configuration expects integer Iranian rials (IRR).
- [ ] Configure merchant ID, terminal ID, terminal key, and public HTTPS callback URL only through the deployment secret/configuration system. Never commit credentials or print them in logs.
- [ ] Verify the callback URL is reachable from Sadad and returns only generic status information. Do not place secrets or unnecessary personal data in query parameters.
- [ ] Apply the payment database migration in a controlled deployment, with a verified backup and a documented rollback/recovery procedure.
- [ ] Run end-to-end tests in the provider's approved test environment for success, provider rejection, duplicate initiation, duplicate callback, invalid/mismatched token, amount/order mismatch, timeout, malformed/oversized responses, and reconciliation recovery.
- [ ] Verify monitoring and an operator runbook for stale pending/verifying/reconciliation-required payments; no job may automatically mark ambiguous payments successful or failed without authoritative provider evidence.
- [ ] Decide explicitly whether the website continues using the existing live PHP flow or migrates to .NET. Never allow both implementations to initiate/finalize the same business transaction.
- [ ] Inspect and test the deployed PHP `/payment/order.php` source and its server-side tariff lookup in the deployment repository/environment; that handler is not present in this Git repository, so its actual amount calculation and Sadad request cannot be verified here.
- [ ] Confirm all CI jobs pass on the exact release commit and review the complete merge-request diff for unrelated changes.

## Activation procedure

1. Keep `Payment:Enabled=false` while any required gate above is incomplete.
2. Deploy the reviewed build and schema changes to the approved environment.
3. Complete provider sandbox verification and reconcile every test transaction.
4. Obtain explicit operational approval for the rollout window and monitoring coverage.
5. Enable `Payment:Enabled=true` only in the target deployment configuration after the gates are signed off.
6. Monitor callback errors, duplicate/idempotency conflicts, pending age, and reconciliation candidates; disable initiation immediately if outcomes become ambiguous at an unexpected rate.

## Known integration blocker

The existing website form uses legacy values such as `duplicate` and `partition`, while the .NET API resolves tariffs using `ServiceCode` values such as `S01`–`S05`. A reviewed mapping and approved tariff table are prerequisites for a safe integration. The website previously displayed a fixed `۵٬۰۰۰ تومان` amount; the display has been changed to say the final amount is determined by the approved tariff/payment system, and CI checks that this fixed display does not return. This is only a display correction: `/payment/order.php` is referenced by the page but is absent from this repository, so its server-side tariff calculation, provider calls, and callback behavior remain unverified. Do not claim the website payment flow is fully confirmed until that deployed source is reviewed. The legacy PHP payment implementation must not be modified or run in parallel with .NET payment finalization without a separate approved migration decision.


## Release blocker status — 2026-10-13

| Priority | Item | Evidence status / next gate |
| --- | --- | --- |
| Immediate | Sadad end-to-end completion | Application-level tests cover amount/order checks, idempotency, callback token validation, and ambiguous outcomes. Merchant-specific signing/date/response contract and authorized sandbox tests are still unverified; keep payment disabled. |
| Immediate | Migration verification | CI now compares the complete set of timestamped migration source files with EF-discovered migration IDs, in addition to checking required baseline IDs. Confirm this exact commit's pipeline before treating the gate as verified. |
| Immediate | Website payment amount and PHP/.NET boundary | The fixed amount display was removed and CI guards against its return. The referenced PHP order handler is not in this repository, so backend amount lookup and PHP/.NET transaction ownership remain unverified. |
| High | Zero Data Retention | Not proven. SQLite persistence, payment transaction records, audit events, and other stateful storage exist. Requires an approved data-flow/retention definition and evidence across logs, database, cache, telemetry, backups, and exports. |
| High | Sana/Shahkar/organization APIs | Production startup currently fails closed because approved adapters are not configured; Development uses mocks. Official contracts, credentials, allowlists, and authorized sandbox/production evidence are missing. |
| High | Production database | Current registration uses SQLite. Production database choice, multi-instance transaction/concurrency behavior, migration and restore procedure remain open. |
| High | Secrets and key management | Sadad/OTP secrets are read from configuration/environment and production OTP hashing fails closed if no key is configured. Persistent Data Protection key storage, secret rotation, and target-environment secret-manager evidence are not established. |
| Next | Distributed rate limiting | Current ASP.NET rate-limit policies are in-process. A shared/distributed limiter and its deployment dependency are not implemented or verified. |
| Next | WMS/WFS, complete frontend, monitoring/SIEM, DR | Official geospatial contracts, completed user-facing frontend, production monitoring/SIEM integration, and tested disaster recovery remain open. |
| Before release | SAST/DAST and penetration test | No independent penetration-test report or complete SAST/DAST evidence is available in this repository checkpoint. CI green is not a substitute. |

These are tracked as release gates, not claims of compliance or production readiness. Do not enable `Payment:Enabled` until the relevant evidence is reviewed and approved.


## Verification update — 2026-10-13

- [x] The exact branch commit `ffcddc69bdc65f4e96b4406f9e27ca1760697fab` passed pipeline [2942405294](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2942405294), including site validation, PHP syntax validation, .NET Release build, EF migration discovery consistency, and the .NET test suite.
- [x] CI migration discovery check now compares every timestamped migration source file with the migration IDs discovered by EF, in addition to requiring the baseline migration IDs.
- [x] Website fixed-amount display regression check passed. This only validates displayed copy and does not validate the absent deployed `/payment/order.php` backend.
- [ ] Production schema application, backup/restore, and recovery remain unverified; a migration-discovery pass is not evidence that a production database was migrated safely.
- [ ] Sadad merchant contract, approved tariff/service mapping, and authorized sandbox end-to-end evidence remain incomplete.

Payment remains disabled. This update records CI evidence only; it does not approve rollout.
