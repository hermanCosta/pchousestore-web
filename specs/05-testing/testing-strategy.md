# Testing Strategy

**Status:** DRAFT

## Backend

- Unit tests for domain invariants, calculations, validation and state transitions.
- Application tests for use-case orchestration and authorization decisions.
- Integration tests against a disposable PostgreSQL instance or equivalent CI-managed database.
- API tests for HTTP contracts, validation errors, pagination, Shop isolation and employee action authorization.

## Frontend

- Component tests with Vitest and React Testing Library.
- Form validation and accessibility checks.
- Tests for loading, empty, success and error states.
- Critical end-to-end workflows after the feature flows are approved.

## Regression

Create a traceability matrix mapping each retained legacy workflow to its source evidence, SPEC requirement, test case and approval.

## Financial and transactional tests

- Decimal precision and rounding rules.
- Partial payments and remaining balances.
- Transaction rollback when any part of composite sale/order creation fails.
- Duplicate submissions and idempotency behavior where relevant.
- Cash closing reconciliation rules after they are discovered.

## Exit criteria

All acceptance criteria pass; high-risk workflows have automated regression coverage; security review is complete; unresolved business-critical questions are closed or explicitly excluded by approval.

## Accepted domain decisions — mandatory coverage

- One Company with multiple Shops; Shop-scoped operations and attempts to tamper with the active Shop context.
- ShopSession never includes an Employee identity; different employees can use the same active Shop session.
- Each protected operation requires a fresh employee credential challenge; a single-use EmployeeActionAuthorization cannot be replayed or reused for a different action/Shop.
- Product-only inventory, including rejection of Service stock movements.
- One PaymentTransaction with multiple PaymentAllocation rows for combined tender; no duplicate gross recognition.
- Positive-magnitude refund transactions linked to original transactions and protected against cumulative over-refund.
- Shop/date DailyClosing behavior: concurrent duplicate-close protection within one Shop and independent closes across different Shops on the same date.
