# Project Readiness Checkpoint — 2026-10-17

## Live continuation update — 2026-10-18

- Working branch `codex/project-readiness-baseline` has advanced beyond the historical baseline below; `master` remains untouched.
- Pipelines for the latest grouped identity/workflow/OTP changes were subsequently observed as successful: `2959753669` (identity no-store tests), `2959766131` (workflow stage stability), and `2959766415` (in-memory OTP input guards). These are not evidence for any later, uncommitted change.
- Persistent OTP still needed matching blank-input guards and regression coverage; the current continuation addresses that gap. Verify the pipeline for its resulting commit separately.

## Verified repository baseline

- Default branch: `master`, commit `54e224d8b09eb9dbd882a0ba61e97d153af0136b`.
- Master pipeline `2954049140`: **success** for that exact commit.
- Working branch: `codex/project-readiness-baseline`, based on that master commit.
- README documentation commit: `41e5049572e9fc560ceffa03bc93b5752aba5701`.
- Pipeline `2955142185`: **success** for the README commit; all four jobs succeeded: `site_validate`, `secret_scan`, `php_validate`, and `dotnet_verify`.
- Pipeline `2954999694` on `codex/local-test-isolation`: **failed before tests ran** because jobs remained pending without a matching runner (`stuck_pending_no_matching_runners`). This is runner/scheduling evidence, not a code-test failure. Do not call that branch's tests green.

## Regulatory requirements that must be evidenced

The repository's `الزامات_کارگزاری.html` contains the supplied three-article licensing/activity-permit guideline. This checkpoint records implementation and evidence implications; it does not replace legal review of the authoritative, currently applicable documents.

### License / technical test access — Article 2

- Corporate/legal application documents, required undertakings, named technical liaison, and organizational evidence are non-code deliverables.
- The brokerage system must demonstrate at least three services implemented according to their own approved requirements for the relevant licensing gate, subject to the guideline's stated exception for qualifying existing system-development entities.
- Receiving a brokerage license alone does not authorize commercial service delivery; activity permission is a separate gate.

### Commercial activity permission — Article 3

- Qualified registration/legal and surveying specialists, branch evidence, approved penetration-test lab report, and software technical approval are external release gates.
- Production and test systems must be hosted exclusively in approved domestic data centers, with evidence for the actual deployment.
- Payment must be a direct gateway connected to the brokerage legal entity's account; intermediary accounts are prohibited by the supplied text.
- NDA, SLA evidence (the supplied guideline states at least 99% monthly uptime), read-only regulatory dashboards, support/call center, required security deposit, and prescribed corporate-purpose wording require documented evidence and accountable owners.
- A green software pipeline cannot prove any of these organizational, contractual, hosting, or regulatory conditions.

## Engineering status and release gates

| Priority | Area | Evidence / next action |
|---|---|---|
| Immediate | CI baseline | README commit's four-job pipeline passed. Continue checking the exact head pipeline after each grouped change. |
| Immediate | Sadad payment | Keep `Payment:Enabled=false`. Obtain the current merchant integration pack, approved tariff mapping and named approvals; execute authorized sandbox scenarios. |
| Immediate | PHP/.NET boundary | The public page references `/payment/order.php`, which is absent from this repository. Preserve the live PHP flow; establish transaction ownership before any .NET activation. |
| High | Official integrations | Obtain versioned contracts and authorized test access for registry/Sana/Shahkar and any geospatial APIs. Development mocks are not evidence of production connectivity. |
| High | Persistence and deployment | SQLite is currently configured. Approve the production database, multi-instance concurrency, migration, backup/restore and recovery evidence. |
| High | Privacy / ZDR | Produce a data-flow and retention inventory covering database, audit, logs, telemetry, caches, exports and backups; do not claim ZDR from CI results. |
| High | Security approval | Obtain the required approved-lab penetration-test report and software technical approval; track findings through remediation and retest. |
| Next | Operations | Implement/approve distributed rate limiting for multi-instance deployment, read-only monitoring dashboards, SIEM integration, support and a tested DR plan. |
| Next | Product readiness | Map each of the minimum three services to approved requirements, stage transitions, authorization, data handling, integration contract and acceptance tests. |

## Ordered continuation plan

1. **Repository evidence:** keep this checkpoint and `docs/AI_HANDOFF.md` aligned with the live branch/commit/pipeline state.
2. **Workflow traceability:** source-derived route matrix now exists in `docs/CORE_WORKFLOW_TRACEABILITY_2026-10-17.md`. It identifies S01 as the only service-specific creation route currently visible and does not count generic creation as proof of three approved services. Continue by mapping each approved service requirement to state transitions, authorization, persistence, adapter and tests.
3. **Security and schema regressions:** the empty-database migration test now asserts both discovery/application of `20261014120000_AddOtpChallengeExpiresAtIndex` and creation of `IX_otp_challenges_ExpiresAt`. The change needs the new commit's CI result before it is considered verified. Continue reviewing ownership/authorization, no-store, rate-limit, audit export and OTP behavior against exact source and tests.
4. **Deployment readiness:** document approved target database, hosting, secret management, distributed throttling, backup/restore, monitoring and rollback responsibilities before implementing environment-specific choices.
5. **Payment readiness:** do not activate the .NET path until tariff/business approval, merchant contract, sandbox evidence and single-owner transaction routing are complete.
6. **Review:** inspect the complete branch diff and pipeline; propose a merge request for review but do not merge into protected `master` without explicit authorization.

## Safety and scope boundaries

- No changes to the live PHP payment handler, payment secrets, production credentials, or production transactions.
- No invented tariffs, official API behavior, regulatory interpretations, or service workflow rules.
- No production-readiness claim based solely on a green pipeline.
- Prefer grouped commits to avoid unnecessary pipeline runs; do not repeatedly retry jobs that have no matching runner.
