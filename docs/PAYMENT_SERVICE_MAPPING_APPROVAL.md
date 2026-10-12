# Payment Service Mapping and Tariff Approval Record

This record must be completed from the authoritative business tariff source and the current Sadad merchant integration pack. Do not infer codes, prices, currency units, or service meanings from legacy form labels.

## Approval metadata

- Business owner: TBD
- Technical reviewer: TBD
- Authoritative tariff source (document/version/date): TBD
- Sadad merchant integration pack/version/date: TBD
- Approval date: TBD
- Approved deployment environment: TBD

## Service mapping

For each billable website option, record the exact submitted legacy value and the API service code. API tariff configuration expects a positive integer amount in Iranian rials (IRR). If any mapping or amount is unapproved, leave that tariff unset so payment initiation remains fail-closed.

| Website field / option | Exact legacy value | API ServiceCode (S01–S05) | Approved amount (IRR) | Source reference | Business approver |
| --- | --- | --- | ---: | --- | --- |
| TBD | TBD | TBD | TBD | TBD | TBD |

Add one row per distinct billable option. Do not combine options unless the authoritative source explicitly defines them as the same service and tariff.

## Sadad contract sign-off

| Contract item | Evidence / confirmed value | Reviewer | Status |
| --- | --- | --- | --- |
| Request endpoint and HTTP method | TBD | TBD | Not verified |
| Request fields, casing, amount unit | TBD | TBD | Not verified |
| Terminal key encoding, 3DES mode/padding, SignData input | TBD | TBD | Not verified |
| LocalDateTime format and timezone | TBD | TBD | Not verified |
| Successful and rejected ResCode values | TBD | TBD | Not verified |
| Verify request/response fields and order/amount semantics | TBD | TBD | Not verified |
| Callback method, field names, and order identifier | TBD | TBD | Not verified |
| Test-environment credentials and callback reachability | TBD | TBD | Not verified |

## Release gate

- [ ] All service mapping rows are complete and approved.
- [ ] Every configured tariff is traceable to an authoritative source and explicitly denominated in IRR.
- [ ] Sadad contract items are verified against the merchant-specific current integration pack.
- [ ] Provider sandbox tests cover successful payment, rejection, duplicate initiation/callback, amount/order mismatch, timeout, and reconciliation.
- [ ] Database migration, backup, rollback, monitoring, and operator runbook are reviewed.
- [ ] A single payment implementation is selected for each transaction path; PHP and .NET must not both initiate or finalize the same payment.
- [ ] The exact release commit has a green CI pipeline and its full diff is reviewed.

Do not enable Payment:Enabled=true until every release gate is signed off. This document is a blank approval record, not evidence that the integration has been approved.
