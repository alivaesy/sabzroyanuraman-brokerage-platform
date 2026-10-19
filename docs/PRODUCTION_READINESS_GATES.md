# Production Readiness Gates — Working Record

This is an implementation tracking record, not a replacement for the approved brokerage requirements or external contracts. It must not be used as evidence of regulatory approval.

## Service scope

The project has three required services in its current scope:

- S01 — تهیه نقشه
- S02 — درج گواهی اقدام
- S03 — درج ادعای موضوع ماده ۱۰ قانون

Do not infer service-specific form fields, legal conditions, workflow transitions, fees, organization API codes, or acceptance rules from the service names. For each service, mark an item approved only after linking it to the corresponding approved requirement clause or an authoritative organization contract.

| Concern | S01 | S02 | S03 | Evidence required before approval |
| --- | --- | --- | --- | --- |
| Required input fields and validation | Not yet traceably approved here | Not yet traceably approved here | Not yet traceably approved here | Exact requirement clause and approved field schema |
| Authentication and authorization | Shared application controls exist; service-specific mapping needs evidence | Same | Same | Role/claim and MFA policy mapped to the approved service rule |
| Workflow stages and legal transitions | Existing S01-specific use case; compare to approved process | Not yet evidenced here | Not yet evidenced here | Approved stage/state diagram and transition acceptance tests |
| Organization integration | Development mocks exist; production adapter not proven | Not yet evidenced here | Not yet evidenced here | Current API contract, IAM access, sandbox evidence and error semantics |
| Persistence and retention | Shared persistence exists; data policy needs approval | Same | Same | Data classification, purpose, retention period and deletion/archive authorization |
| Audit events | Shared audit infrastructure exists | Shared audit infrastructure exists | Shared audit infrastructure exists | Event catalogue and proof that sensitive payloads are not logged |
| Failure and recovery | External/local partial-failure recovery requires contract-specific proof | Same | Same | Retry/idempotency/reconciliation contract and tested recovery scenarios |
| Payment | Do not infer a fee or activate .NET payment | Same | Same | Approved service tariff mapping and signed Sadad contract; PHP path remains unchanged |
| Acceptance criteria | Existing tests are not proof of complete service compliance | Not yet evidenced here | Not yet evidenced here | Requirement-to-test mapping and passing tests on the reviewed commit |

## Current technical gates

1. **Audit export:** SQLite query filtering, ordering and limit are performed in SQL. LoadRecordsAsync now rejects limits outside 1–10,000 even if called outside the HTTP endpoint. This is a query bound, not a retention/deletion policy.
2. **Audit retention:** Audit:RetentionDays and X-Audit-Retention-Target-Days describe a target only. Do not represent them as proof that old records are archived or deleted. The approved legal/business retention interpretation and lifecycle implementation remain open.
3. **OTP:** expiry cleanup is executed by targeted database deletes rather than loading all challenges into application memory. Production provider behavior and concurrent issuance/verification still require verification on the approved database provider.
4. **Production database:** the application deliberately refuses non-Development startup until an approved production provider is configured. SQLite remains the development/test provider; do not weaken this guard to make deployment start.
5. **Distributed controls and operations:** in-process rate limiting is not a distributed limit. Production deployment still needs an approved shared limiter, key/Data Protection management, monitoring/SIEM, backup/restore and DR evidence, plus independent security testing.
6. **Payment:** keep Payment:Enabled=false. The payment approval record is incomplete. Do not modify the live PHP payment path or allow PHP and .NET to initiate/finalize the same transaction.
7. **Hosting and regulatory evidence:** code alone cannot prove approved domestic Production/Test hosting, software technical approval, penetration-test clearance, business permits, NDA, SLA, or support-center readiness. Track signed artifacts separately.

## Release decision

A green CI pipeline establishes only that the configured jobs passed for its exact commit. It does not approve external API contracts, service-specific legal rules, payment tariffs, production infrastructure, or regulatory documents.

Before any production release, record the exact commit SHA, successful pipeline URL, reviewed diff, approved service requirement references, external sandbox evidence, database/restore evidence, security test report, and the named business/technical approvers. If an item has no authoritative evidence, leave it open rather than filling it with an assumption.