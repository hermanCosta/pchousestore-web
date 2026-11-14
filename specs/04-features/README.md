# Feature Specifications

- [Employee administration](employee-administration.md) — approved and implemented as a local foundation 2026-11-12; bootstrap runbook and end-to-end validation remain.
- [CAT-01 Customers and Shared Catalogues](customer-and-catalog.md) — DRAFT; legacy audit recorded, product rules remain open; no implementation authorized.

Feature folders are created only when the corresponding legacy behavior has been investigated.

Each feature SPEC must contain:

1. Goal and scope.
2. Legacy source references (files and methods).
3. Confirmed existing behavior.
4. Proposed redesign behavior, clearly separated.
5. Business rules and invariants.
6. Data model and relationships.
7. API request/response contracts.
8. UI flows and screen states.
9. Authorization requirements.
10. Acceptance criteria.
11. Unit/integration/UI test scenarios.
12. Open questions and approval status.

Do not use guessed requirements to fill gaps.

## Initial feature candidates

- Authentication and company context
- Customers and employees
- Product/service/fault catalog
- Sales and payments
- Service orders and status lifecycle
- Refurbished equipment and sales
- Deposits and refunds
- Cash in/out and daily closing
- Reports and receipts

Feature scopes must be approved after discovery. A Java class is not automatically a distinct web feature.

## Current drafts

- [Shop authentication and API access](authentication-and-shop-context.md) — approved and implemented foundation; session-grant revocation and end-to-end validation remain.
- [Authentication and signing-key persistence](../02-architecture/authentication-persistence.md) — DATA-05 logical/physical design approved for implementation; PostgreSQL/Npgsql provider validation remains.
- [Cross-Shop ServiceOrder transfer](service-order-shop-transfer.md) — DRAFT; custody, parts, eligibility and authorization rules remain OPEN.
- [Shop printable identity fields](shop-print-identity.md) — data and print-snapshot requirements approved 2026-11-11; production branch contact values remain OPEN.
