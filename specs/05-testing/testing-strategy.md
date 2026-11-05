# Testing Strategy

**Status:** DRAFT

## Backend

- Unit tests for domain invariants, calculations, validation and state transitions.
- Application tests for use-case orchestration and authorization decisions.
- Integration tests against a disposable MySQL instance or equivalent CI-managed database.
- API tests for HTTP contracts, validation errors, pagination and company isolation.

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
