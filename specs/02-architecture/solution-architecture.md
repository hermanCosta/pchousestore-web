# Target Solution Architecture

**Status:** DRAFT

## 1. Style

Modular monolith with explicit module boundaries.

## 2. Logical components

- React SPA — presentation and client-side navigation.
- ASP.NET Core API — authentication boundary, HTTP contracts and composition.
- Application — use cases, orchestration, validation and authorization policies.
- Domain — entities, value objects, invariants and domain services.
- Infrastructure — EF Core mappings, persistence, file/report generation and external adapters.
- MySQL — relational persistence.

## 3. Backend project proposal

```text
src/
  PcHouseStore.Api/
  PcHouseStore.Application/
  PcHouseStore.Domain/
  PcHouseStore.Infrastructure/
tests/
  PcHouseStore.UnitTests/
  PcHouseStore.IntegrationTests/
frontend/
  pchousestore-web/
```

Feature-oriented Application organization:

- Authentication
- Companies
- People
- Catalog
- Sales
- ServiceOrders
- Refurbishment
- Finance
- CashManagement
- Reporting

## 4. Frontend organization

```text
frontend/pchousestore-web/src/
  app/
  components/
  layouts/
  features/
  services/
  hooks/
  types/
  utils/
```

Each feature owns its screens, API functions, schemas, hooks and feature-specific components.

## 5. API principles

- Version endpoints under `/api/v1`.
- Use DTOs and explicit request/response contracts.
- Validate on the server.
- Apply authorization policies per operation.
- Use pagination/filtering on collections.
- Use consistent Problem Details responses.
- Use idempotency controls for sensitive retryable operations when needed.

## 6. Persistence principles

- Use EF Core configuration classes.
- Use decimal for money with explicit precision/scale.
- Define indexes, FK constraints and delete behavior intentionally.
- Use transactions for composite sales, orders, payments and cash operations.
- Avoid generic repository layers unless they add domain value.

## 7. Security

- Use a proven password hashing implementation.
- Do not log passwords, tokens or sensitive payment data.
- Store secrets outside source control.
- Apply least privilege to database accounts.
- Enforce company scoping in every query where appropriate.
- Audit sensitive operations.

## 8. Decisions still open

- Cookie authentication versus bearer tokens.
- Single company versus multi-company/tenant model.
- Hosting and deployment provider.
- Report generation library.
- File/image storage strategy.
