# Brokerage Audit, OTP, CI and Handoff Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bound SQLite work in audit export and OTP cleanup, align local checks with GitLab CI, and refresh handoff status without changing payment or production operations.

**Architecture:** Keep API behavior and the provider-neutral audit path intact. Add SQLite-specific database-side filtering/deletion using UTC DateTimeOffset parameters and existing indexes. Extract shared site and EF-migration checks for GitLab CI and the PowerShell mirror, then refresh only the current-status sections of the two handoff documents.

**Tech Stack:** .NET 10, EF Core 10, SQLite, xUnit, PowerShell, GitLab CI.

**Spec:** `docs/superpowers/specs/2026-10-14-audit-otp-ci-handoff-hardening-design.md`

## Global Constraints

- Preserve audit endpoint authorization, rate limiting, NDJSON shape, no-store headers, date inclusivity, descending timestamp/EventId order, and limit bounds.
- Preserve OTP lifetime (2 minutes), five-attempt lockout, user binding, and verification behavior.
- Keep `Payment:Enabled=false`; do not touch or execute the live PHP payment flow.
- Do not merge, change GitLab merge settings, deploy, modify production infrastructure, or request secrets.
- Preserve timestamp precision and confirm application timestamp producers remain UTC before relying on SQLite text ordering.
- Add no unrelated cleanup or documentation rewrite.
- Do not manually create or retry intermediate pipelines. GitLab has auto-started branch/documentation pipelines. If the integrated validation run exposes a test defect, correct the defect and let that corrective commit trigger one final-head validation pipeline.

## Review Focus

- Non-UTC audit query bounds must select the same instants as equivalent UTC bounds; pin with a boundary test in Task 1.
- Equal audit timestamps must retain deterministic descending EventId ordering; pin with a tie test in Task 1.
- Audit export must apply `WHERE`, `ORDER BY`, and `LIMIT` in SQLite rather than materializing the table; pin by capturing the executed SQLite command in Task 1.
- Expired OTP rows must be deleted while unexpired rows remain, without a table-wide SELECT; pin by capturing the SQLite command in Task 2.
- Local checks must catch a newly added source migration missing from EF discovery and a returned fixed payment amount string; add those parity checks in Task 3.

---

### Task 1: Bound SQLite audit export

**Files:**
- Modify: `src/Brokerage.Api/Endpoints/AuditExportEndpoints.cs`
- Modify: `src/Brokerage.Infrastructure/Persistence/AuditEventWriter.cs` only if UTC normalization is required by confirmed producer/storage evidence.
- Test: `tests/Brokerage.Api.Tests/AuditComplianceTests.cs`

**Interfaces:**
- Consumes: current `AuditEventRecord.OccurredAt` mapping and existing audit index.
- Produces: unchanged `GET /audit/events/export` response, with SQLite date predicates/order/limit executed by the database.

- [x] Add a test for non-UTC bounds and equal timestamp tie ordering; include a larger fixture with a small requested limit.
- [ ] Capture SQLite command logging and assert that the export query includes its timestamp predicates, descending ordering, and SQL limit, and does not issue an unbounded audit-table SELECT.
- [x] Implement a SQLite raw SQL query with parameterized nullable UTC bounds and requested limit; normalize incoming bounds to UTC. Keep the current LINQ behavior for non-SQLite providers.
- [ ] Run the single final pipeline only after all tasks; expect the new audit tests and existing suite to pass.

### Task 2: Delete expired OTP rows in the database

**Files:**
- Modify: `src/Brokerage.Infrastructure/Persistence/PersistentOtpService.cs`
- Test: `tests/Brokerage.Api.Tests/PersistentAuthenticationStateTests.cs` (existing SQLite persistence tests)

**Interfaces:**
- Consumes: `BrokerageDbContext.OtpChallenges`, current SQLite DateTimeOffset mapping, and existing `UserId` index.
- Produces: unchanged `IssueAsync`/verification contract; expired-row cleanup is a targeted SQL delete on SQLite and a translated filtered delete on other providers.

- [x] Add a persistence test that seeds expired and unexpired challenges and captures SQL to assert a predicate-based DELETE, with no table-wide challenge SELECT.
- [x] Replace full-table materialization with provider-specific targeted deletion; retain cancellation propagation and the current two-minute expiry.
- [ ] Confirm the final pipeline passes the new and existing OTP tests.

### Task 3: Align local CI with GitLab CI

**Files:**
- Modify: `.gitlab-ci.yml`, `ci-local.ps1`
- Create: `scripts/validate-site.sh`
- Create: `scripts/verify-ef-migrations.sh`

**Interfaces:**
- Consumes: current `.gitlab-ci.yml` shell checks for site copy and migration source/discovery equality.
- Produces: local Docker-based checks that detect the same targeted regressions; no Git or remote mutations.

- [x] Extract the EF migration source/discovery comparison into a shared script invoked by GitLab and local CI.
- [x] Extract the GitLab site-copy and fixed-amount assertions into a shared script invoked by GitLab and local CI.
- [x] Inspect the CI commands; both run shared shell checks without GitLab mutations or repository writes.

### Task 4: Refresh handoff status without rewriting history

**Files:**
- Modify: `docs/AI_HANDOFF.md`
- Modify: `docs/WORKFLOW_CONSISTENCY_REVIEW.md`

**Interfaces:**
- Consumes: final branch commit and the single final Pipeline result.
- Produces: concise current-status headers with older checkpoint notes retained as historical.

- [x] Update only current-status sections to identify `codex/audit-otp-ci-handoff-hardening` and the associated branch pipeline list; preserve prior checkpoints as historical.
- [x] Preserve historical implementation notes and payment safety statements.
- [ ] Confirm the written commit and pipeline URL match live GitLab after the final pipeline is created.

### Final verification (corrected final-head pipeline)

- [x] Inspect the integrated change list; it is limited to this plan, its design, targeted code/tests/CI scripts, and the two handoff docs.
- [x] Review initial integrated pipeline 2945622710; build and migration jobs passed, while the new audit SQL test exposed an assertion that counted EF diagnostic log entries. Corrected the assertion to target the executed-command log entry.
- [ ] Review the corrective commit’s final-head validation pipeline.
- [ ] Review all jobs and test output for the corrected final HEAD; report any remaining failure without another retry.
- [ ] Confirm payment remains disabled and no merge/production action occurred.
