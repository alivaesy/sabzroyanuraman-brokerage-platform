# Payment Reconciliation Runbook

## Purpose and safety rules

Use this runbook for payments whose provider result is ambiguous, whose callback did not complete, or whose local state is stale. It is a manual investigation procedure, not an automated settlement mechanism.

- Never mark a payment successful based only on a browser redirect, callback query string, screenshot, or customer statement.
- Never mark an ambiguous payment failed solely because the application timed out.
- Never repeat Sadad Create or Verify for a payment in `ReconciliationRequired` unless the current merchant contract explicitly defines a safe recovery operation and the responsible reviewer approves it.
- Never edit payment rows directly in the database as a routine recovery method.
- Do not paste terminal keys, access tokens, full customer records, or unnecessary personal data into tickets, chat, or logs.
- Keep `Payment:Enabled=false` if a systemic mismatch, unexpected duplicate, or unexplained spike in ambiguous outcomes is detected.

## Find candidates

The following read-only, rate-limited endpoints require the `PaymentOperations` authorization policy and return `Cache-Control: no-store` responses:

- `GET /ops/payments/reconciliation-candidates?olderThanMinutes=10&limit=100`
- `GET /ops/payments/stale-verifications?olderThanMinutes=10&limit=100`

`olderThanMinutes` must be 1–1440; `limit` must be 1–500. The endpoints list candidates only; they do not mutate payment state or contact Sadad.

## Investigation procedure

1. Record the payment ID, service-request ID, amount, currency, local status, creation/update timestamps, and correlation ID from authorized internal records.
2. Find the corresponding transaction in Sadad's authoritative merchant portal or approved support channel. Use the exact order ID sent by this integration (the payment GUID in `N` format), not a customer-supplied identifier.
3. Compare the merchant-side order ID and amount with the local payment record. Confirm the amount unit is IRR; do not compare toman and rial values without an explicit conversion recorded in the case.
4. Confirm the provider's final status and, where applicable, its system trace/retrieval reference. A successful state requires authoritative provider evidence and a matching order and amount.
5. If the provider's record is missing, pending, contradictory, or cannot be accessed, leave the payment unresolved and escalate to the payment technical owner and merchant support. Do not guess.
6. If the provider confirms a final result but local state differs, preserve the evidence (redacted), record the reviewer and timestamp, and follow the approved recovery procedure for the deployed integration version. Do not improvise SQL updates or re-run verification.
7. After approved recovery, re-query the candidate endpoint and confirm the local state, gateway reference, audit trail, and customer-visible status agree. Close the case only when the discrepancy is explained.

## Escalation triggers

Escalate immediately if any of these occurs:

- The same idempotency key appears associated with different request details.
- A customer reports a debit but the merchant portal has no conclusive transaction record.
- Provider order ID, amount, currency, or reference differs from the local record.
- A payment remains `Verifying` or `ReconciliationRequired` beyond the operational threshold.
- Callback failures, HTTP errors, or ambiguous Create/Verify results increase unexpectedly.
- There is any evidence that PHP and .NET both initiated or finalized the same business transaction.

For a systemic issue, disable new payment initiation at the deployment configuration layer, preserve logs and audit records, and do not alter the live PHP flow unless an explicitly approved incident/migration plan requires it.

## Case record template

- Payment ID / order ID:
- Service-request ID:
- Local amount and currency:
- Local status and timestamps:
- Provider final status:
- Provider amount/currency/reference:
- Evidence source and lookup time:
- Reviewer and approver:
- Recovery action and deployment version:
- Post-recovery verification:

Keep only redacted evidence needed for audit and incident review. This runbook does not authorize activation of payment initiation; all release gates in `PAYMENT_ROLLOUT_CHECKLIST.md` still apply.
