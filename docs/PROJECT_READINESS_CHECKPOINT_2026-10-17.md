# Project Readiness Checkpoint — 2026-10-18

## Live branch and CI status

- Working branch: `codex/project-readiness-baseline`.
- Current HEAD at this checkpoint update: `f06918746097bc7abf9ac3be998c37f262f2e614` (`fix: validate required S01 workflow inputs`).
- Pipeline [2959925854](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2959925854): **success** for that exact SHA, including .NET verification, PHP validation, and secret scan.
- Pipeline [2959908797](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2959908797): **success** for `49fd9a1f`.
- `master` remains at `54e224d8b09eb9dbd882a0ba61e97d153af0136b`; no merge has been made. The branch is not merged.
- Pipeline results apply only to the exact SHAs above; subsequent commits need their own evidence. Work continues without waiting for CI, but test success is never inferred.

## Verified engineering progress

- Generic `POST /service-requests` requires audit writer and unit of work and commits request, initial stage, and audit events in one local transaction. SQLite regression coverage includes successful persistence, invalid input rejection, and rollback on audit failure. Commit `cf4b43db` adds the latest generic-request input validation.
- S01 requires repository, audit writer, and unit of work; rejects a null request model and blank applicant ID before identity or organization calls. Its local persistence is transactional, but remote organization acceptance cannot be rolled back locally. Commit `f0691874` passed pipeline `2959925854`.
- Persistent OTP rejects blank/incomplete inputs before database mutations; workflow stage identifiers are trimmed and completion timestamps remain stable on repeated completion; identity and audit export error paths use `Cache-Control: no-store`.
- Payment initiation remains disabled by default. The live PHP payment path remains outside this work.

## Regulatory and production gates

The supplied `الزامات_کارگزاری.html` requires evidence beyond CI, including at least three services built against their own approved requirements for the relevant gate (subject to the stated exception), approved-lab penetration testing/software technical approval, approved domestic hosting, a direct legal-entity payment gateway, uptime evidence, a read-only regulator dashboard, and organizational support/contractual evidence.

- S01 is the only dedicated service-specific creation route currently evidenced. Generic creation does not prove S02–S05 are complete. Obtain approved service requirements before adding stages, fields, or validations.
- SQLite remains the configured persistence provider. Production multi-instance concurrency, migration rollout, backup, restore, and disaster-recovery evidence are absent. The application now explicitly fails closed outside Development rather than allowing SQLite to be mistaken for production-ready persistence.
- Production organization, Sana, and Shahkar adapters are not configured; startup must remain fail-closed until approved adapters and environment evidence exist.
- Sadad merchant-specific contract, approved tariff mapping, sandbox evidence, and the PHP/.NET transaction-ownership decision remain mandatory before payment activation. Do not change the live PHP path.

## Ordered continuation

1. Obtain approved requirements for at least three services and map each end-to-end: entry point, authorization/identity, validation, stages/state, persistence/audit, external adapter, failure/recovery, and tests.
2. Obtain the organization integration's documented idempotency/correlation/status-lookup contract before implementing automated submission retries or recovery.
3. Approve and implement a production database provider with migration, concurrency, backup/restore, and recovery verification.
4. Complete Sadad service/tariff approval and sandbox sign-off while keeping payment disabled and preserving PHP/.NET ownership boundaries.
5. Produce evidence for hosting, security testing, data retention/ZDR, monitoring/SIEM, read-only regulator access, 99% uptime, and support/contractual prerequisites.

## Boundaries

- Do not merge into `master`, delete branches, or change the live PHP payment path without explicit authorization.
- Do not invent missing service requirements, organization API fields, Sadad contract details, or business tariff values.
- A green pipeline is engineering evidence for its checks, not a licensing or production-readiness certificate.
