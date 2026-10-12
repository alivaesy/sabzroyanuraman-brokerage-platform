# Core Workflow Inventory

Status: repository-derived engineering inventory, not a compliance certification or proof that external services are production-ready.

## API workflow entry points observed on `feat/payment-foundation-sadad-ready`

| Capability | Route / component | Observed behavior | Evidence still required |
| --- | --- | --- | --- |
| Generic service request | `POST /service-requests` | Requires Applicant authorization and verified identity; creates a request with status `INITIAL`. | Confirm each service code has an approved workflow definition and that the generic path cannot bypass service-specific required fields/stages. |
| S01 request | `POST /service-requests/s01` | Requires Applicant authorization and verified identity state; executes the S01 use case, performs identity verification, submits to the organization adapter, records workflow stages and audit events. | Confirm adapter is an approved sandbox/production integration rather than a development mock; review failure atomicity between external submission, local persistence and audit writes. |
| Read own/request-authorized case | `GET /service-requests/{id}` | Loads a request and checks applicant ownership or an explicitly privileged role. | Verify every returned field is necessary for the caller and role grants match the approved authorization matrix. |
| Workflow-stage history | `GET /service-requests/{id}/workflow-stages` | Uses the same request access predicate before reading stage history. | Verify stage metadata does not reveal unnecessary organization or personal information. |
| Organization status refresh | `GET /service-requests/{id}/organization-status` | Reads remote organization status, mutates the stored status, may complete/create workflow stages, and writes audit events. | GET has state-changing side effects. Add regression coverage for repeated and concurrent requests, duplicate stage prevention, partial failures, and authorization. Consider an explicit refresh command only after compatibility review. |
| Payment creation | `POST /payments` | Disabled by default; requires Applicant, MFA, ownership, server-side tariff and idempotency key. | Merchant-specific Sadad contract, authoritative service/tariff mapping, sandbox evidence, and explicit PHP/.NET ownership decision remain rollout gates. |
| Sadad callback | `GET/POST /payments/sadad/callback` | Treats callback token as untrusted and invokes server-to-server verification; ambiguous results are not automatically settled. | Verify the implementation against the current merchant-issued integration pack and sandbox callback behavior. |
| Payment reconciliation views | `GET /ops/payments/*` | Protected operational endpoints expose bounded stale-verification/reconciliation candidates and recommended manual checks. | Validate role assignments, operator runbook, alerting, and recovery process in the deployment environment. |

## Boundary and release rules

- A green CI pipeline proves only the checks in that pipeline passed for that commit. It does not establish legal compliance, production connectivity, merchant approval, or end-to-end readiness.
- The organization, Sana/Shahkar and geospatial adapters must be classified individually as mock, contract-defined, sandbox-tested, or production-approved.
- Do not invent missing service stages, service-code mappings, tariffs, external API behavior, or legal interpretations.
- Keep sensitive identity/registry payloads out of logs, telemetry, caches, fixtures and durable storage unless the approved data policy explicitly permits them.
- Preserve the live PHP payment flow and keep .NET payment initiation disabled until the payment rollout checklist is signed off.
- Before any merge, review the complete branch diff; current branch scope includes payment, site, CI, audit, organization integration and operational changes.
