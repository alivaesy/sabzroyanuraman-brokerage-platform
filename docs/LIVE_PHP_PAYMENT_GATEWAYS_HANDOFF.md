# Live PHP Payment Gateways — Operational Handoff (Redacted)

> Last confirmed from user-provided, read-only server output: 2026-10-25. This document records observed deployment facts to avoid repeating investigations. It is not a source-code audit or a merchant-contract certification.
>
> **Do not modify the live payment flow casually.** The owner reports that Sadad and AsanPardakht have both been operating successfully. No credentials or transaction reference values belong in Git.

## Live deployment boundary

- Website payment implementation is in PHP on the cPanel host; sensitive/live PHP files are intentionally not tracked in this repository.
- Payment directory: `/home/sabzroyanuraman/public_html/payment`.
- Order database: `/home/sabzroyanuraman/payment-data/orders.sqlite`.
- Live callbacks observed by server-side inspection:
  - Sadad: `payment/callback-sadad.php`
  - AsanPardakht: `payment/callback.php`
- Related live files include `order.php`, `SadadGateway.php`, and `sadad-config.php`. Do not copy live configuration or handler source into Git.
- The separate .NET payment foundation is not the live PHP flow. Do not enable it or let it initiate/finalize the same transactions unless an explicit migration/ownership plan is approved.

## Actual SQLite order schema observed

Database table: `orders`

- `id` — integer primary key
- `invoice_id` — unique local invoice identifier
- `service_code`, `service_title`
- `amount_toman`, `amount_rials`
- `customer_name`, `mobile`
- `ref_id`, `paygate_tran_id`
- `status` — default `CREATED`
- `created_at`, `verified_at`, `settled_at`
- `gateway` — default `asan`; observed values include `asan` and `sadad`

Do not publish customer details, invoice IDs, provider references, or payment tokens in documentation or issue threads.

## Confirmed live test evidence (2026-10-25)

The owner queried the database read-only after making two real test payments. Both most recent orders were stored with `status = SETTLED`, and both had non-null `verified_at` and `settled_at` timestamps:

- AsanPardakht (`gateway=asan`): 5,000 toman; status `SETTLED`.
- Sadad (`gateway=sadad`): 5,000 toman; status `SETTLED`.

Invoice IDs, payment references, and provider transaction IDs are intentionally omitted. This confirms the local database recorded these two observed transactions as settled; it does not replace a full provider reconciliation or guarantee every failure path is correct.

## Recent safe checks and limits

- `php -l callback-sadad.php` returned `No syntax errors detected` during the 2026-10-25 investigation.
- The Sadad callback file reported modification time `2026-09-19 21:19:34 +0300` and size 4,838 bytes in that inspection.
- No file named `callback-sadad.php.bak-amount-check` was found in the payment directory. No `callback-sadad.php.bak*` files were found by the subsequent check.
- These observations do **not** prove the full command history or all historical file changes. The assistant did not directly execute edits on the server. Do not claim that an attempted terminal command definitely made no changes based only on timestamps or a missing backup.
- No payment code was changed as part of this documentation commit.

## Operational rules

1. Keep both currently working PHP gateways untouched unless a specific defect is reproduced and a rollback plan exists.
2. Before proposing a code edit, inspect the current deployed file, its exact validation logic, amount units, and the official merchant contract; make a backup and compare before/after.
3. Verify a payment using the local `orders` record and authoritative provider evidence. A browser success page alone is insufficient.
4. Preserve the distinction between toman and rial: the database stores both `amount_toman` and `amount_rials`; validate the exact unit expected by each provider.
5. Never store MerchantId/TerminalId values, TerminalKey, passwords, tokens, database credentials, card data, or raw provider payloads in GitHub.
6. The existing detailed Sadad protocol notes are in [SADAD_GATEWAY_TECHNICAL_PROFILE.md](SADAD_GATEWAY_TECHNICAL_PROFILE.md). They include unverified protocol claims that must be checked against the current merchant-issued integration pack before implementation.
7. AsanPardakht protocol details, current merchant-specific contract, exact verification rules, and full failure/replay behavior have not been independently documented or audited here. Do not invent them.

## Next time before touching payment code

- Re-read this handoff and the Sadad technical profile.
- Inspect the live deployed PHP files privately and confirm the live database schema.
- Reproduce the specific issue without printing secrets or customer data.
- Prefer read-only diagnostics first; document intended behavior and rollback before any edit.
- After a narrowly scoped change, run syntax checks and an approved end-to-end test for both gateways; confirm local state and provider result. Do not infer provider correctness from PHP syntax alone.
