# Regulatory Compliance Roadmap — Brokerage Platform

## Purpose and source boundary

This roadmap translates the repository's supplied `الزامات_کارگزاری.html` source into engineering and delivery work. It is a planning aid, not legal advice, an official interpretation, or proof of compliance. Resolve ambiguities with the competent authority or qualified counsel before treating them as binding implementation decisions.

## Scope boundaries

Keep these deliverables separate:

1. **Existing public website and legacy PHP payment flow** — preserve the currently operating flow; do not change or run a competing .NET payment finalization path against the same transactions without an explicit migration decision.
2. **Dedicated brokerage service system** — the applicant-facing workflow and API integrations for registry services. The public website alone is not evidence that this system is complete.
3. **Hosting and operations** — approved domestic hosting, deployment separation, backup/recovery, monitoring, and secure logs.
4. **License and activity-permit evidence** — organizational documents, qualified personnel, branch/support operations, approvals, and other non-code prerequisites.

## Current release-blocker snapshot — 2026-10-13

This snapshot is the current engineering status; older dated notes later in this file are historical. A green CI pipeline proves only the checks it executes.

| Priority | Work item | Current evidence and remaining gate |
| --- | --- | --- |
| Immediate | Sadad payment | Application-level validation exists for server-owned amount, persisted idempotency, callback-token matching, provider order/amount/reference matching, and ambiguous-result reconciliation. The merchant-specific contract, approved tariff/service mapping, and sandbox end-to-end evidence are not approved; keep initiation disabled. |
| Immediate | Migration verification | CI now compares timestamped migration source filenames against IDs emitted by EF, while retaining the explicit required-migration baseline. Verify the pipeline for the commit that introduces this check. |
| Immediate | Website payment and PHP/.NET boundary | The static 5,000-toman display was replaced with tariff-based wording and CI guards against its return. The HTML points to `/payment/order.php`, but that handler is absent from this repository; its server-side amount logic and transaction ownership cannot be confirmed from this checkout. |
| High | Zero Data Retention | Unverified. Application state, SQLite records, payment tokens/references, audit events, logs, backups and exports require a documented data-flow/retention assessment and environment evidence. Do not claim ZDR based on tests alone. |
| High | Sana/Shahkar/organization APIs | Development uses mocks and non-Development startup fails closed until approved adapters are supplied. Official specifications, credentials, allowlists, and sandbox evidence remain external blockers. |
| High | Production persistence | `Program.cs` registers SQLite. A production-grade database/provider, multi-instance transaction/concurrency tests, controlled migrations, backup and restore evidence are still required. |
| High | Secrets and Data Protection | Environment/configuration inputs exist for Sadad and OTP HMAC, and production startup fails closed for a missing OTP hash key. A persistent protected Data Protection key ring, secret-manager integration, rotation and access policy are not evidenced. |
| Next | Distributed rate limiting | The configured ASP.NET rate-limit policies are process-local. A shared limiter for multi-instance deployment and its failure behavior are not implemented/verified. |
| Next | WMS/WFS, complete frontend, monitoring/SIEM, DR | Official geospatial contracts, complete frontend workflows, production telemetry/SIEM wiring, and a successful restore/DR exercise remain unverified. |
| Before release | SAST/DAST and penetration testing | Independent security assessment and remediation/retest evidence are absent from this repository checkpoint. A green build/test pipeline is insufficient. |

## Work plan and acceptance evidence

### A. Repository and architecture baseline — start here

- [ ] Inventory each implemented service workflow, its state transitions, authorization rules, persistence, and tests.
- [ ] Mark each organization-facing integration as mock, contract-defined, sandbox-tested, or production-approved.
- [ ] Verify production startup fails closed when approved authentication and integration adapters are absent; development mocks must never be used in production.
- [ ] Define explicit data classification and retention rules for identity, ownership, cadastral/geospatial, case, audit, and operational data before adding persistence.
- [ ] Review branch-to-`master` diff for unrelated site changes, duplicate implementations, secrets, and accidental live-payment coupling.

**Acceptance evidence:** reviewed architecture/integration inventory, passing CI on the exact commit, and a tracked list of blockers with owners/evidence. A passing pipeline alone is not a compliance certificate.

### B. Core brokerage workflow and data protection

- [ ] Implement only approved service workflows and stage transitions; do not invent missing registry business rules.
- [ ] Use least-privilege authorization and ownership checks for every case read/write and operational endpoint.
- [ ] Confirm the minimum data needed for each workflow. Do not persist authoritative government/registry payloads, identity documents, property/ownership details, or geospatial records unless the approved legal and technical basis explicitly permits it.
- [ ] If temporary in-memory processing is required by the source, ensure temporary data is cleared after the transaction and is not copied into logs, telemetry, error messages, caches, or test fixtures.
- [ ] Keep audit events privacy-minimal and tamper-resistant; define retention and export access separately from general application logs.
- [ ] Use synthetic test data; do not copy production identity/cadastral data into test environments.

**Acceptance evidence:** data-flow and retention review, authorization/privacy regression tests, and approved workflow definitions.

### C. Organization, Sana, Shahkar and geospatial integration

- [ ] Obtain official API specifications, authentication requirements, test access, approved endpoint allowlists, and versioned contracts from the responsible authority.
- [ ] Implement adapters behind interfaces; keep development mocks isolated to Development.
- [ ] Verify timeouts, retries, idempotency, redaction, correlation, rate limits, and safe handling of malformed or ambiguous responses.
- [ ] Confirm the approved coordinate reference system and permitted WMS/WFS/geospatial operations from the authority; do not infer a mandatory UTM zone from an example.
- [ ] Validate any Zero Data Retention and geospatial processing constraints against the current authoritative text before storing or exporting payloads.

**Acceptance evidence:** contract tests plus authorized sandbox evidence. Production readiness requires authority approval and production credentials managed outside the repository.

### D. Security, hosting and operational readiness

- [ ] Confirm production and test environments are hosted exclusively in approved domestic data centers, with access and deployment boundaries documented.
- [ ] Review TLS, security headers, WAF/firewall, secret management, dependency/license inventory, least privilege, and incident response.
- [ ] Establish encrypted backup and prove restore; document RPO/RTO and recovery ownership.
- [ ] Provide monitoring for availability, API latency, errors, resource consumption, and database readiness.
- [ ] Keep logs structured and privacy-minimal, protected against unauthorized alteration, retained according to the approved policy, and exportable to the authority's SIEM if required.
- [ ] Complete SAST/DAST, independent penetration testing by an accepted laboratory, remediation, and retesting before the relevant release gate.

**Acceptance evidence:** deployment/security review, restore-test record, monitoring/runbook, penetration-test report, and remediation sign-off.

### E. Licensing and activity permit — external evidence, not code-only tasks

Track these separately from software completion:

- [ ] Company/registration documents, shareholder and board information, required criminal-record certificates, approved articles of association, formal application, and required undertakings.
- [ ] Technical liaison with the authority; separately track the technical/security responsible-person role until the authority confirms whether the roles may be combined.
- [ ] Required qualified registration/legal and surveying personnel and supporting credentials.
- [ ] Branch evidence, call center/online support, business license, e-commerce trust mark, and software technical approval where required.
- [ ] Domestic hosting approval, direct payment gateway tied to the legal entity's account (no intermediary account), NDA, SLA, read-only oversight dashboards, and required security deposit/collateral.
- [ ] Obtain formal resolution of source-text differences before representing a requirement as settled.

**Acceptance evidence:** documents and approvals issued by the competent organizations; code commits cannot substitute for them.

## Known unresolved interpretations and external blockers

- The supplied source text contains an SLA difference (99% in the procedure versus 99.5% in the technical document). Do not label either number as the definitive legal requirement without authoritative clarification; the engineering target can be stricter if approved.
- Penetration-test laboratory eligibility is described differently in the procedure and technical document. Obtain written clarification on the accepted laboratory.
- WGS84 / UTM Zone 39N appears as an example; the approved project coordinate system must come from the authority.
- Do not assume the technical liaison and technical/security responsible person are interchangeable.
- Do not infer service-code mappings, tariffs, payment contract fields, or registry API behavior from website labels or mock implementations.
- The Sadad/.NET payment foundation is intentionally separate from the live PHP flow. Its rollout gates remain in `PAYMENT_ROLLOUT_CHECKLIST.md`; keep `Payment:Enabled=false` until every gate is approved.

## Execution order

1. Inventory core service workflow, data flows, and production integration boundaries.
2. Close concrete authorization/privacy defects and add regression tests in a single coherent batch.
3. Implement approved core workflow stages and adapter contracts; avoid guessed production integrations.
4. Complete operational/security controls and evidence.
5. Assemble licensing/permit evidence in parallel with engineering where possible.
6. Review the full release diff, validate in approved test environments, and obtain explicit release approval.

**Definition of done:** implemented behavior is tested, external integrations have authoritative contract evidence, operational controls are verified, external approvals are recorded, and the exact release diff is reviewed. Never equate “CI green” with “ready for production” or “legally compliant.”


## Repository evidence snapshot — 2026-10-12
- The repository currently demonstrates layered Domain/Application/Infrastructure code and a backend API, but that alone does **not** demonstrate that the required microservices architecture is deployed or that a modern frontend SPA (Angular, Vue, or React) is complete. Track architecture topology, service boundaries, deployment evidence, and frontend completeness as explicit gaps until reviewed evidence is available.
- The latest verified core-workflow pipeline at the time of this snapshot is `2939200020`, which passed the five configured CI jobs for commit `23330b0f2f6bc543ab475f7497189652d679dafb`: https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2939200020. Any subsequent code/doc batch requires its own pipeline verification.
- Organization status values are now intended to be excluded from general application logs; the marker-based regression test in the follow-up batch must pass before considering this privacy change verified.
- CI does not establish domestic hosting approval, disaster recovery, SLA, Zero Data Retention, legal authorization, third-party integration approval, or production readiness. Each requires independent documentary or environment evidence.


## Updated repository evidence — 2026-10-12
- Latest verified branch head is `5808352357792b0ff1ae14e623a2403fbdf785d9`; pipeline `2939258054` passed all five configured checks: https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2939258054.
- Status responses missing a usable status now fail closed, and general logs no longer include the organization-returned status or the status-query tracking ID. Regression coverage passed on the current branch head.
- The current workflow still persists the organization status on the service request and copies previous/new status values into audit records. Confirm classification, retention, access and deletion requirements before claiming data-minimization compliance for persisted status.
- S01 request/stage/audit writes are not currently one local transaction; organization submission is an external side effect before local persistence. Status refresh also has a read/modify/write concurrency gap. Track these as release blockers until failure semantics, concurrency behavior and reconciliation are approved and tested.
- Microservice deployment, completed Angular/Vue/React SPA, domestic hosting, disaster recovery, SLA, ZDR, legal authorization and external integration approval remain evidence gaps unless separately proven. A green pipeline does not close these gaps.
- See `docs/WORKFLOW_CONSISTENCY_REVIEW.md` for the staged design and regression-test plan. Keep `Payment:Enabled=false` and preserve the live PHP payment flow.

 
## Current engineering evidence update — 2026-10-12
- S01 local request/stage/audit persistence now uses a scoped EF transaction on branch commit `530c87f97cdfbfe516adf769e3416a59df1b273a`; pipeline `2939464325` passed the five configured CI jobs: https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2939464325
- This code-level improvement does not close the external side-effect gap: the organization submit occurs before the local transaction and cannot be undone by a database rollback. Documented idempotency/reconciliation evidence remains required.
- Organization-status refresh still needs concurrency and partial-failure tests. Hosting, deployment topology, frontend completeness, SLA/DR/ZDR, official integrations, legal licensing and security assessment remain independent evidence gates.
- A SQLite-backed rollback test passed pipeline `2939513854`, verifying local EF/SQLite rollback for S01 persistence. Payment remains disabled and the live PHP flow is out of scope.

 
## Organization-status transaction evidence — 2026-10-12
- S01 local creation rollback is verified by the passing SQLite regression in pipeline `2939513854`: https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2939513854
- Organization-status refresh now wraps local request/stage/audit changes in a scoped EF transaction after the remote status call. A new audit-failure injection test checks rollback of these local changes and passed in pipeline `2939580658` for commit `51eac5102ffa98dce2cf542aa2757511826691dc`.
- This does not establish concurrency safety, provider-independent locking, or resolution of external submit reconciliation/idempotency. Other hosting, legal, integration, SLA/DR/ZDR and security evidence gates remain open. Payment stays disabled.


## Latest repository evidence checkpoint — 2026-10-13
- Exact branch HEAD at this checkpoint is documentation-only commit `f46e7607352840d9b0288a90e1da96ba08ec475f`; pipeline [2942086092](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2942086092) passed all five configured jobs. The underlying code/test checkpoint `00927789eed4a206fc29b9888895502023780178` passed pipeline [2941850499](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2941850499).
- Current code-level evidence now includes S01 request/stage/audit persistence in one local transaction after the external organization adapter returns a tracking ID, persistence after caller cancellation once that ID is known, a SQLite rollback regression for injected local persistence failure, disabled automatic retries for S01 submission, and deterministic organization-status concurrency handling with HTTP 409 on a stale local write.
- These controls do not make remote acceptance and local persistence atomic and do not resolve process crashes, database failure after remote acceptance, or timeout with unknown remote acceptance. Authoritative external idempotency/status-lookup documentation and an approved recovery procedure remain release gates.
- Older dated snapshots above are historical and are superseded wherever they describe local S01 writes as non-transactional or organization-status concurrency as wholly unimplemented. Production-provider behavior, hosting approvals, deployment architecture, legal/licensing evidence, external integration approval, SLA/DR/ZDR, and security assessment remain independent evidence gates. Keep `Payment:Enabled=false` and preserve the live PHP payment flow.


## Evidence update — 2026-10-13

- Pipeline [2942405294](https://gitlab.com/sabz-group1/brokerage-platform/-/pipelines/2942405294) passed all five configured jobs on commit `ffcddc69bdc65f4e96b4406f9e27ca1760697fab`. This confirms the repository's configured site/PHP validation, .NET build/tests, and migration discovery consistency only.
- The migration verification now detects disagreement between timestamped migration source files and EF-discovered IDs. It does not verify a live database's schema, backup, restore, or deployment sequence.
- The website's hard-coded amount display is removed and guarded by CI; the deployed `/payment/order.php` source is absent from this repository, so its calculation and Sadad behavior remain unverified.
- No change to this evidence status closes ZDR, domestic hosting, production database, secret/key management, distributed rate limiting, official integration contracts, WMS/WFS, frontend completeness, SIEM/DR, legal approvals, or independent security testing. Keep each as an explicit release gate until authoritative evidence is attached.
- `Payment:Enabled=false` remains mandatory; a passing pipeline is not a compliance certificate or production release approval.


## Repository secret scanning control — 2026-10-13
- A pinned Gitleaks job is now part of CI to inspect the checked-out repository tree for detectable secret patterns. This is a current-tree check only, not a historical Git scan or an audit of CI variables, deployment hosts, logs, backups, or external secret stores.
- Root ignore rules exclude common local environment, SQLite database/WAL, certificate/private-key, secrets-directory, and Data Protection key artifacts. Ignore rules are preventative convenience, not a remediation for already committed secrets.
- Any confirmed credential exposure requires revocation/rotation and a history review; do not print secret values in logs or issue reports. This control does not establish ZDR or production compliance.


## Security finding and containment — 2026-10-13
- Gitleaks detected a shared live-GPS viewer key duplicated in the API and browser-delivered page, and a keyed GIS tile-service URL. The GPS viewer key was not a safe secret because it was delivered to every browser accessing the page.
- Immediate containment disables the live GPS read action with HTTP 503 and removes the embedded viewer key/UI data fetch. The keyed GIS tile layer was removed; remaining map layers are preserved.
- Before re-enabling live GPS display, implement and test server-side authentication/authorization and abuse controls. If the removed tile-service key is a real credential, revoke/rotate or restrict it with the provider. CI scanning does not revoke already exposed credentials or scan deployment secrets/history.
