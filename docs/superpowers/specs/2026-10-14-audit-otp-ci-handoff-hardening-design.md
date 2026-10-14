# Audit, OTP, CI and Handoff Hardening — Design

**Date:** 2026-10-14  
**Target branch:** `codex/audit-otp-ci-handoff-hardening`, based on `feat/payment-foundation-sadad-ready` at `7d76a81b`  
**Status:** Proposed; implementation must wait for review of this design and its implementation plan.

## Goal

Bound database work for SQLite audit exports and expired OTP cleanup, bring local CI checks into line with GitLab CI, and correct stale handoff metadata. Preserve current endpoint contracts, payment safeguards, and historical documentation.

## Confirmed baseline

- `AuditExportEndpoints` materializes the full SQLite audit table before filtering, ordering, and applying the requested limit. The audit table already has an `OccurredAt` index, but SQLite's current `DateTimeOffset` representation is not safely translated by the present EF query.
- `PersistentOtpService.IssueAsync` materializes every OTP challenge to remove expired rows. It creates challenges using UTC time; OTP expiry is two minutes and verification semantics must remain unchanged.
- `.gitlab-ci.yml` compares migration source files with EF-discovered IDs and checks the website's payment amount copy. `ci-local.ps1` only checks a fixed migration baseline and does not make the same website copy assertion.
- `docs/AI_HANDOFF.md` and `docs/WORKFLOW_CONSISTENCY_REVIEW.md` identify `f46e7607` as the latest checkpoint, while the live payment branch is at `7d76a81b` with passing pipeline `2943436079`.

## Proposed changes

1. **Audit export:** add a SQLite-specific bounded SQL query that applies optional date bounds, descending timestamp/event ordering, and `LIMIT` in the database. Keep the existing non-SQLite LINQ path equivalent. Normalize request bounds and persisted audit timestamps to UTC and use the provider's existing DateTimeOffset text mapping consistently; current application audit producers supply UTC instants. Retain the existing `OccurredAt` index. Do not introduce a new timestamp schema or silently lose precision. If repository evidence reveals existing non-UTC stored values, stop and revise this design before migrating or interpreting them. Do not turn the 180-day response header into an unstated export cutoff or deletion policy.
2. **OTP cleanup:** use a targeted SQLite `DELETE` for expired challenges, with the same UTC DateTimeOffset representation; keep verification and expiry behavior unchanged. Use a provider-translatable filtered delete for other providers. Preserve unexpired challenges and avoid loading the challenge table into application memory. No schema migration is expected because the current challenge writer uses UTC and the existing `UserId` index remains unchanged.
3. **Local CI parity:** extend `ci-local.ps1` so its migration source/discovery check and website amount-copy guard match the intent of `.gitlab-ci.yml`. Keep both paths read-only with respect to Git and remote services.
4. **Handoff freshness:** update the current-status sections in `docs/AI_HANDOFF.md` and `docs/WORKFLOW_CONSISTENCY_REVIEW.md` to cite the exact current branch head and successful pipeline once available, and clearly label older checkpoints as historical. Do not rewrite or remove the historical review record.

## Constraints and non-goals

- Keep payment initiation disabled. Do not modify, execute, or inspect secrets for the live PHP payment flow.
- Do not merge branches, alter GitLab merge-protection settings, deploy, or change production infrastructure.
- Do not change public API contracts, OTP expiry or attempt limits, audit retention semantics, or legal interpretations.
- Avoid unrelated site, GIS, payment, identity, or workflow changes.
- Do not claim regulatory or production readiness from repository checks.

## Acceptance criteria

- SQLite audit export returns the correct filtered, ordered, limited results without materializing the full table in application memory; verification covers date boundaries, non-UTC request bounds, tie ordering, and a small limit over a larger fixture.
- OTP issue removes expired rows with a targeted database operation, without enumerating/materializing the challenge table; verification proves expired rows are removed and unexpired rows remain.
- Local migration and site checks detect the same classes of regression guarded by GitLab CI.
- Handoff current-status text matches the exact branch head and successful pipeline at the time of implementation; older evidence remains visibly historical.
- The final diff is limited to these behaviors and documentation. Verification results and any provider/data limitation are reported explicitly.

## Implementation order

1. Confirm the UTC timestamp invariant against all current audit producers and the existing SQLite storage mapping; stop before data/schema changes if that invariant is not supported.
2. Implement and verify the bounded Audit query.
3. Implement and verify targeted OTP cleanup.
4. Align local CI checks.
5. Refresh handoff status from live GitLab evidence.
6. Review the integrated diff and report remaining limitations.
