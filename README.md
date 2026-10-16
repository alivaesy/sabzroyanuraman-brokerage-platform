# Brokerage Platform

## Project status

This repository contains the brokerage service API, its application/domain/infrastructure layers, the public website, and CI validation. It is an engineering work in progress—not evidence of regulatory approval or production readiness.

**Source of truth:** GitLab `master`. Work on a separate branch and review the complete diff before merging.

## Solution layout

- `src/Brokerage.Api` — HTTP endpoints, authentication/authorization, middleware, rate-limit policies, telemetry, and application startup.
- `src/Brokerage.Application` — use cases, application services, contracts, validation, and integration-facing models.
- `src/Brokerage.Domain` — core entities and enums.
- `src/Brokerage.Infrastructure` — persistence, EF Core migrations, external-service adapters, and Sadad payment gateway integration.
- `tests/Brokerage.Api.Tests` — API/integration behavior and security regressions.
- `tests/Brokerage.Application.Tests` — application-level behavior.
- `sabzroyanuraman-site` — public website and legacy PHP endpoints.
- `docs` — workflow inventory, setup notes, payment gates, and regulatory-readiness tracking.
- `scripts` — website validation and EF migration consistency checks.

## Local verification

Use the .NET 10 SDK. Run commands from the repository root.

```sh
dotnet restore BrokeragePlatform.slnx
dotnet build BrokeragePlatform.slnx --configuration Release --no-restore
dotnet test BrokeragePlatform.slnx --configuration Release --no-build --no-restore
```

The migration check also needs the repository's EF tool version (CI installs `dotnet-ef` 10.0.12):

```sh
dotnet tool install --global dotnet-ef --version 10.0.12
export PATH="$PATH:$HOME/.dotnet/tools"
sh scripts/verify-ef-migrations.sh
sh scripts/validate-site.sh
```

CI additionally runs PHP syntax validation over every `.php` file under `sabzroyanuraman-site` and a Gitleaks scan. On Windows, run the shell scripts from a compatible shell such as WSL or Git Bash; CI is the authoritative check for the exact pushed commit.

## Critical boundaries

- **Do not enable .NET payment initiation** (`Payment:Enabled=false` by default) until the merchant-specific Sadad contract, approved service/tariff mapping, sandbox evidence, operational recovery, and release checklist are signed off. See `docs/PAYMENT_SERVICE_MAPPING_APPROVAL.md` and `docs/PAYMENT_ROLLOUT_CHECKLIST.md`.
- The website references `/payment/order.php`, but that handler is not present in this repository. Its server-side amount calculation and payment behavior cannot be verified here.
- Do not change the live PHP payment path or allow PHP and .NET to initiate/finalize the same business transaction without an explicit migration decision.
- Development authentication and mock integrations are not production identity/integration adapters. Production authentication and approved external-service adapters must be configured and verified independently.
- SQLite is currently registered by the API. Production database selection, multi-instance concurrency, backup/restore, and controlled production migration evidence remain release gates.
- Rate limiting is currently in-process; multi-replica deployment needs an approved shared limiter or equivalent ingress enforcement.
- Do not put real identity, registry, cadastral, payment, or other sensitive production data into test fixtures, logs, telemetry, or this repository.

## Release-readiness evidence

A green pipeline proves only the checks executed for that commit. It does not prove legal compliance, production connectivity, provider approval, Zero Data Retention, a successful disaster-recovery exercise, or penetration-test completion. Track these separately in `docs/REGULATORY_COMPLIANCE_ROADMAP.md`.

Before proposing a merge:
1. Confirm the source branch is based on current `master`.
2. Review the entire branch diff for unrelated changes, secrets, and coupling to the live payment path.
3. Verify the exact commit's CI pipeline and investigate every failed or stuck job.
4. Keep external approvals and production-only evidence as explicit blockers; do not infer missing contracts or business rules.
