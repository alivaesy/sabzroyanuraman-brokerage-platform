# Core Release Gates — 2026-10-18

This is an execution checklist derived from the repository's requirements and current implementation. A code test or green pipeline is not, by itself, external approval or production-readiness evidence.

## Gate A — Core applicant workflow

- [x] Applicant authorization and verified-identity checks for request creation.
- [x] Request ownership checks for request details, workflow history, and organization-status refresh.
- [x] Sensitive responses use `Cache-Control: no-store`; rate-limit rejection is also non-cacheable.
- [x] OTP challenge issue/consume, mandatory audit, and MFA grant are transaction-bound.
- [x] S01 workflow stage ordering is deterministic when persisted timestamps tie.
- [ ] Obtain approved S02/S03 service definitions and map required fields, stages, error states, and recovery to acceptance tests. Until then, the generic endpoint is only a request-creation foundation.

## Gate B — Organization integration

- [x] Failed status refresh is audited without provider payloads or exception text and does not advance local workflow state.
- [x] Unapproved service codes are rejected before using S01-specific status transitions.
- [ ] Obtain the official organization API contract, including correlation/idempotency, status lookup, timeout and ambiguous-result handling.
- [ ] Run authorized sandbox tests and approve the recovery process for remote acceptance followed by local persistence failure. Do not add automatic submission retries before this gate is met.

## Gate C — Audit and data protection

- [x] Audit export applies date filters, stable ordering, and a bounded limit in the database query.
- [x] Audit export rejects malformed date/limit query values and invalid ranges/limits with non-cacheable client errors.
- [x] Rate-limited audit export responses are non-cacheable and provide a bounded `Retry-After` interval.
- [x] Export output omits national identifiers and export access is itself audited without copying exported payloads.
- [x] The response names retention configuration as a target, not proof of enforced retention.
- [ ] Approve and implement the actual retention/archival policy, including protection against premature deletion, then retain operational evidence.
- [ ] Confirm audit storage integrity, access controls, backup/restore and any required SIEM forwarding in the deployment environment.

## Gate D — Production operations

- [x] Startup fails closed outside Development while only the development SQLite persistence setup is available.
- [ ] Select and approve the production database/provider; then verify migrations, multi-instance concurrency, backup and restore.
- [ ] Replace or validate process-local rate limiting for a multi-instance deployment, including shared-store failure behavior.
- [ ] Evidence persistent protected Data Protection keys, secret rotation, monitoring/alerting, incident response, and RTO/RPO through a deployment exercise.
- [ ] Complete independent SAST/DAST and penetration testing, remediation and retest.

## Gate E — Payment and public site boundary

- [x] .NET payment initiation remains disabled by default.
- [x] The existing PHP payment path is intentionally outside this change scope.
- [ ] Obtain the current Sadad merchant integration package and complete sandbox initiation, callback, server-side verification, and reconciliation tests.
- [ ] Approve the authoritative tariff/service mapping and business owner sign-off.
- [ ] Decide transaction ownership and migration between the existing PHP handler and .NET before enabling any competing live flow.

## Exit rule

Only close a gate when its listed evidence exists on the relevant deployment or authority-approved environment. Never infer missing service rules, payment fields, tariff values, or operational approvals from the current code.
