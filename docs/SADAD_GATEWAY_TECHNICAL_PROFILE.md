# Sadad PSP Gateway Technical Profile (Redacted)

> **Evidence status:** This file preserves the Sadad gateway notes supplied by the project owner on 2026-10-25. It is an integration handoff, **not a source-code audit or merchant-contract sign-off**. Endpoint details, field names, status codes, cryptographic format, 15-minute reversal behavior, duplicate semantics, and amount units must be verified against the current merchant-issued Sadad integration pack and the deployed PHP handler before implementation or production use.
>
> **Security:** No real credentials are stored here. Never add MerchantId/TerminalId values, TerminalKey, tokens, passwords, database credentials, full card data, or unredacted provider payloads to this document or source control. Use `[REDACTED]` for sensitive values.

## 1. Provider and API style

- Provider reported by owner: Sadad Electronic Payment (Sadad PSP / Shaparak; associated with Bank Melli Iran).
- Reported API style: server-to-server REST/JSON. The supplied notes describe a TripleDES/Base64 `SignData` value.
- Verification status: provider identity is reported; exact contract and cryptographic requirements are **not yet independently verified**.

## 2. Reported payment flow

1. Server creates a local payment/order record and sends a Create request (`PaymentRequest`) to Sadad.
2. Sadad returns a `Token`; server redirects the customer to the provider's hosted payment page using that token.
3. Customer completes or cancels payment at the provider.
4. Sadad sends an HTTP POST to the configured `ReturnUrl` callback.
5. Server validates the callback against the local order, then calls provider Verify server-to-server. Callback alone is not proof of settlement.
6. On authoritative successful Verify and matching order/amount, persist the successful result and provider references.
7. The supplied notes claim a transaction not verified within 15 minutes is automatically reversed. Treat this as **unverified merchant-contract behavior**, not a recovery guarantee.
8. Inquiry/reconciliation is described as available through the merchant portal (`mportal.sadadpsp.ir`) or provider-supported services. No confirmed Inquiry/Reverse/Refund endpoint or implementation has been identified in the supplied material.

## 3. Files and implementation boundary

The following paths were examples in the supplied report, **not verified repository paths**:
- `Services/SadadPaymentService.cs`: proposed provider API client, signing and Verify.
- `Controllers/PaymentController.cs`: proposed create, redirect and callback controller.
- `Utils/TripleDesCrypto.cs`: proposed signing/encryption helper.

Project context separately records a .NET Payment Foundation with `PaymentEndpoints.cs`, `SadadPaymentGateway.cs`, contracts/services and tests. The live PHP handler (reported as `/payment/order.php`) is not tracked in the repository tree and must remain untouched unless an explicitly approved migration plan says otherwise. Do not run PHP and .NET as competing initiators/finalizers for the same transaction. Exact current source paths and behavior must be checked in the relevant branch and deployment.

## 4. Create Payment contract (reported; verify against merchant pack)

- URL: `https://sadad.shaparak.ir/api/v0/Request/PaymentRequest`
- Method: `POST`
- Header: `Content-Type: application/json`
- Reported request fields:
  - `MerchantId`: `[REDACTED]`
  - `TerminalId`: `[REDACTED]`
  - `Amount`: integer amount, claimed to be in IRR
  - `OrderId`: locally generated unique order identifier
  - `LocalDateTime`: provider-required timestamp format/time zone TBD
  - `ReturnUrl`: public callback URL
  - `SignData`: `[REDACTED]`
  - `AdditionalData`: optional, if supported
  - `UserId`: `[REDACTED]` where required by the contract
- Reported signing input: `TerminalId;OrderId;Amount`, encrypted using `TerminalKey` and Base64-encoded. Exact key encoding, 3DES mode, padding, character encoding and signing input are **not confirmed**.
- Response: JSON; reported fields `ResCode`, `Token`, `Description`. Notes claim `ResCode == 0` means Create succeeded.
- Never log or return raw `SignData`, credentials, or provider tokens to the browser/operations API.

Illustrative shape only (not a ready-to-run request):
```json
{
  "MerchantId": "[REDACTED]",
  "TerminalId": "[REDACTED]",
  "Amount": 100000,
  "OrderId": "[LOCAL_ORDER_ID]",
  "LocalDateTime": "[PROVIDER_FORMAT_REQUIRED]",
  "ReturnUrl": "https://example.invalid/api/payment/callback",
  "SignData": "[REDACTED]",
  "AdditionalData": "[OPTIONAL]",
  "UserId": "[REDACTED]"
}
```

## 5. Callback contract (reported; verify)

- Route: the exact configured `ReturnUrl`; `/api/payment/callback` was only an example, not a confirmed live route.
- Method: reported as HTTP POST with form/body fields.
- Reported fields: `OrderId`, `Token`, `ResCode`, `PrimaryAccNo` (potentially masked card data), `HashedCardNo`, `SwitchResCode`.
- The supplied notes interpret `ResCode == 0` as a successful payment return, but this is only a signal to attempt server-side verification; it must not mark the local payment settled by itself.
- The notes say the retrieval reference and system trace are obtained from Verify, not callback: `RetrivalRefNo` (provider spelling as supplied) and `SystemTraceNo`.
- Callback processing must validate local order/token association, expected amount and current state, be safe on repeats, and avoid trusting user-controlled callback data.

## 6. Verify contract (reported; verify)

- URL: `https://sadad.shaparak.ir/api/v0/Advice/Verify`
- Method: `POST`
- Header: `Content-Type: application/json`
- Reported request: `Token` plus `SignData`; both sensitive values are `[REDACTED]`. Supplied notes say signing input is the token encrypted with TerminalKey and Base64-encoded; exact details remain unverified.
- Reported JSON response fields: `ResCode`, `Amount`, `RetrivalRefNo`, `SystemTraceNo`, `OrderId`.
- Supplied notes claim `ResCode == 0` is success and `ResCode == 100` means previously verified. This mapping must be checked against the merchant's current response-code table.
- Treat Verify as successful only when the documented provider success/replay semantics are met **and** order, amount, currency/unit and provider reference match the persisted local payment. A transport timeout or malformed/ambiguous response is not proof of failure or success; move to reconciliation.

## 7. Amount and currency

- Supplied notes claim amounts are integer Iranian rials (IRR), with a .NET-friendly integral representation such as `long`; the actual deployed handler's type is not confirmed.
- Create amount and authoritative Verify amount must match the persisted amount exactly in the provider's documented unit. Never silently convert IRR and toman or accept a mismatch.
- The Payment Foundation mapping record also expects positive integer IRR tariffs; service prices require an authoritative approved tariff source.

## 8. Identifiers and persistence

| Identifier | Meaning / source | Storage requirement |
| --- | --- | --- |
| Internal payment `Id` | Generated by local application | Persist as primary key |
| `ServiceRequestId` | Local service request being paid | Persist foreign-key association |
| `OrderId` | Local order/payment correlation ID sent to Sadad | Persist; enforce appropriate uniqueness |
| Sadad `Token` | Returned from Create | Store securely only if needed for Verify; never expose/log |
| `RetrivalRefNo` | Reported provider retrieval reference from Verify | Persist after authoritative verification |
| `SystemTraceNo` | Reported provider/switch trace from Verify | Persist if returned |
| `ResCode` | Provider response code | Preserve as provider metadata/audit detail, not the sole domain status |
| Idempotency key | Locally generated stable key for a payment initiation | Persist with a unique constraint appropriate to business scope |

The supplied report proposed tables named `Payments`/`PaymentTransactions` and `Orders`, with fields such as `Id`, `OrderId`, `Amount`, `Token`, `RetrivalRefNo`, `SystemTraceNo`, `Status`, `CreatedAt`, `VerifiedAt`. These were generic suggestions, **not confirmed live database table/column names**. Inspect the actual PHP schema and current .NET EF migrations before mapping them.

## 9. Statuses (reported model; compare with current code)

- `Pending`: local payment created, Create/redirect underway or awaiting provider outcome.
- `Paid_Unverified`: callback indicates a potentially successful payment, Verify not yet authoritative.
- `Successful`: authoritative Verify succeeded and local order/amount/reference checks passed.
- `Failed`: definitive Create/provider rejection or verified terminal failure, only where the contract makes the result conclusive.
- `Cancelled`: customer/provider cancellation confirmed by reliable provider result.
- .NET foundation additionally documents `Verifying` and `ReconciliationRequired` for an in-flight Verify or ambiguous result. Preserve these conservative states where applicable; do not force a timeout into Failed.

These names and transitions are a proposed/reported vocabulary, not proof of the live PHP implementation's exact enum.

## 10. Duplicate and idempotency behavior

- Supplied notes claim Sadad rejects duplicate `OrderId` with `ResCode 1011`; this code and behavior require confirmation.
- Locally, enforce idempotent Create using a persisted unique `IdempotencyKey` and stable request fingerprint; same key with different order/amount/service details must be rejected.
- Repeated callback: load by trusted local correlation, acquire concurrency protection, and do not issue concurrent/redundant Verify calls for a payment already successful or currently verifying.
- Repeated Verify: notes claim `ResCode 100` can mean already verified. Confirm the response details and only treat as success if the returned order/amount/reference agree with local data.
- Do not assume provider-side duplicate handling replaces local database constraints or concurrency controls.

## 11. Database, audit and logging

- Actual live PHP order/payment schema and constraints are **not supplied**.
- The .NET foundation uses application persistence and audit/reconciliation workflows; confirm exact migration/entity names on the active branch before implementation.
- Useful audit events: initiation requested, provider Create outcome, redirect issued, callback received (redacted), Verify started/completed, state transition, mismatch, timeout/ambiguous outcome, reconciliation opened/resolved.
- Log correlation/payment IDs, provider operation, timestamps, HTTP/result category, sanitized response code, duration and exception type. Do not log TerminalKey, secrets, authorization headers, raw `SignData`, full tokens, full card numbers, database credentials or full unredacted payloads. If card metadata is required, retain only the minimum approved masked/hashed form.
- The supplied notes suggested logging headers, request/response payloads and client IP; do **not** log them wholesale. Use allowlisted, redacted structured fields and retention controls.

## 12. Reconciliation

- Supplied notes describe manual merchant-portal comparison of local `OrderId` and amount with provider records including `RetrivalRefNo`.
- Project documentation includes read-only operational candidate endpoints:
  - `GET /ops/payments/reconciliation-candidates?olderThanMinutes=10&limit=100`
  - `GET /ops/payments/stale-verifications?olderThanMinutes=10&limit=100`
- These endpoints list candidates only; they do not contact Sadad or mutate payment state. Follow `docs/PAYMENT_RECONCILIATION_RUNBOOK.md`. Automated provider settlement/report import has not been established by the supplied evidence.

## 13. Failure handling

| Case | Safe handling |
| --- | --- |
| Create accepted, callback absent | Keep unresolved/pending; inspect provider record after the approved threshold. Do not assume 15-minute reversal unless confirmed. |
| Duplicate callback | Idempotent state transition; no concurrent duplicate Verify; already-successful result remains stable. |
| Verify returns definitive failure | Apply only documented terminal mapping; preserve redacted audit evidence. |
| Verify timeout/network failure | Mark `ReconciliationRequired` when outcome is ambiguous; do not blindly repeat Create/Verify. |
| Provider paid, local write failed | Alert and reconcile using authoritative provider evidence; recover via approved procedure, not ad hoc SQL. |
| Amount/order mismatch | Do not mark successful or deliver paid service; flag for review. |
| Already verified | Follow documented replay semantics and match local order/amount/reference before accepting. |
| Create response lost/timeout | Outcome may be ambiguous; do not blindly create another transaction with a new order ID. |
| Callback forged or malformed | Reject/ignore safely; require local correlation and server-to-server verification. |

## 14. Configuration key names (values intentionally omitted)

Names supplied by the report:
- `SADAD_MERCHANT_ID=[REDACTED]`
- `SADAD_TERMINAL_ID=[REDACTED]`
- `SADAD_TERMINAL_KEY=[REDACTED]`
- `SADAD_PAYMENT_URL=[REDACTED]`
- `SADAD_CALLBACK_URL=[REDACTED]`

These are candidate names from the supplied notes, not verified names in the deployed PHP or .NET configuration. The .NET foundation's actual configuration schema must be inspected separately. Keep values in the approved secret store/environment, not source control.

## 15. Recommended PaymentTransaction Mapping

| Target field | Suggested mapping | Rule |
| --- | --- | --- |
| `Id` | Local payment primary key | Generate locally; immutable |
| `ServiceRequestId` | Local service request ID | Foreign key; authorize ownership |
| `Amount` | Persisted integer amount in IRR | Immutable once initiation starts; exact Verify match |
| `Currency` | `IRR` | Explicit currency code; do not infer from display text |
| `Status` | Domain payment state | Use guarded transitions; callback alone never means Successful |
| `IdempotencyKey` | Stable local initiation key | Unique within the defined business scope |
| `GatewayReference` | `RetrivalRefNo` after Verify | Store only from authoritative provider response; optionally retain SystemTraceNo separately |
| `CreatedAt` | Local UTC creation timestamp | Generate on insert |
| `UpdatedAt` | Local UTC update timestamp | Update on each committed state transition |
| `VerifiedAt` | Local UTC time successful Verify is durably committed | Null until verified; do not substitute an unconfirmed provider transaction date |

Keep provider token, order correlation, provider response code and trace number in dedicated fields where justified. Avoid overloading `GatewayReference` with multiple identifiers.

## 16. Information still required for a real adapter

1. Current merchant-issued Sadad integration pack and version/date, including authoritative Create/Verify URLs and schemas.
2. Confirmation of test vs production environment and authorized sandbox credentials supplied through a secret manager (never in chat/repository).
3. Exact TerminalKey encoding, 3DES mode/padding, input byte encoding, Base64 rules and signing strings for Create and Verify.
4. Exact timestamp format, timezone, allowed clock skew, and required/optional request fields.
5. Complete current `ResCode` catalogue, including duplicate, cancelled, already-verified, reversed and rejected cases.
6. Confirmed callback method/content type, exact callback field names, signature/authenticity controls, and official return URL requirements.
7. Authoritative definitions for `Amount`, currency/unit, minimum/maximum, Verify amount semantics, and handling of mismatch.
8. Actual deployed PHP handler source reviewed privately, actual schema/table/constraints, and evidence of which code currently initiates/finalizes payments. Do not copy sensitive files into the repository.
9. Confirmed current .NET configuration key names, DI registration, endpoint routes, persistence entities/migrations and test status on the intended integration branch.
10. Provider-approved IP allowlisting and callback reachability requirements.
11. Authoritative Inquiry/reversal/refund/report APIs and a documented recovery/reconciliation process, including whether the 15-minute claim applies to this merchant.
12. Approved service-code/tariff mapping and business owner sign-off.
13. Authorized sandbox evidence for success, decline, cancellation, duplicate initiation/callback, replay Verify, timeout, mismatch and provider-paid/local-write-failure.
14. Explicit ownership/migration plan so the live PHP flow and .NET adapter never process the same payment concurrently.
15. Operational decisions for alerts, retention, secret rotation, audit access and payment incident escalation.

## Related project records

- `docs/PAYMENT_SERVICE_MAPPING_APPROVAL.md` — merchant contract and service/tariff sign-off checklist.
- `docs/PAYMENT_RECONCILIATION_RUNBOOK.md` — safe manual investigation and recovery boundaries.
- `docs/PAYMENT_ROLLOUT_CHECKLIST.md` — release gates.
- `docs/AI_HANDOFF.md` — broader project handoff and live PHP/.NET separation.

**Release rule:** Keep `Payment:Enabled=false` until merchant-specific contract verification, approved tariff mapping, authorized sandbox tests, and explicit PHP/.NET payment ownership are complete. Green unit tests alone do not prove provider or production readiness.
