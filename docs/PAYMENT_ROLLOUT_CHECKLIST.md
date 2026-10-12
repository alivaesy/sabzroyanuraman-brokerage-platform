# Sadad Payment Rollout Checklist

## Current safety posture

- [x] Payment initiation is disabled by default with `Payment:Enabled=false`.
- [x] Amounts are loaded server-side; client-submitted amounts are not trusted.
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
- [ ] Confirm all CI jobs pass on the exact release commit and review the complete merge-request diff for unrelated changes.

## Activation procedure

1. Keep `Payment:Enabled=false` while any required gate above is incomplete.
2. Deploy the reviewed build and schema changes to the approved environment.
3. Complete provider sandbox verification and reconcile every test transaction.
4. Obtain explicit operational approval for the rollout window and monitoring coverage.
5. Enable `Payment:Enabled=true` only in the target deployment configuration after the gates are signed off.
6. Monitor callback errors, duplicate/idempotency conflicts, pending age, and reconciliation candidates; disable initiation immediately if outcomes become ambiguous at an unexpected rate.

## Known integration blocker

The existing website form uses legacy values such as `duplicate` and `partition`, while the .NET API resolves tariffs using `ServiceCode` values such as `S01`–`S05`. A reviewed mapping and approved tariff table are prerequisites for a safe integration. The legacy PHP payment implementation and live payment page must remain unchanged until a separate migration decision is approved.
