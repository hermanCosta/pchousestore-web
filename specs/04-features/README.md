# Feature Specifications

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

- [Shop authentication and API access](authentication-and-shop-context.md) — DRAFT; product/technical approval and DATA-02 are pending.
