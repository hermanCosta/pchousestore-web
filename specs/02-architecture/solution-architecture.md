# Target Solution Architecture

**Status:** DRAFT

## 1. Style

Modular monolith with explicit module boundaries.

## 2. Logical components

- React SPA — presentation and client-side navigation.
- ASP.NET Core API — OAuth 2.0 resource server, JWT Bearer validation, HTTP contracts and composition.
- OpenID Connect authorization server — OpenIddict hosted in the .NET solution; authenticates the Shop principal and issues OAuth access tokens.
- Application — use cases, orchestration, validation and authorization policies.
- Domain — entities, value objects, invariants and domain services.
- Infrastructure — EF Core mappings, persistence, file/report generation and external adapters.
- MySQL — relational persistence.

## 3. Approved repository layout

The approved repository layout is:

```text
pchousestore-web/
├── PcHouseStore.sln
├── src/
│   ├── PcHouseStore.Api/
│   ├── PcHouseStore.Application/
│   ├── PcHouseStore.Domain/
│   └── PcHouseStore.Infrastructure/
├── frontend/
│   └── pchousestore-web/
├── tests/
│   ├── PcHouseStore.UnitTests/
│   ├── PcHouseStore.IntegrationTests/
│   └── frontend/
├── infrastructure/
├── specs/
├── .github/
├── AGENTS.md
└── README.md
```

This layout records the repository organization approved in ADR-033. The solution and application/test projects will be physically created in Phase 2 — Technical Foundation; this decision does not start application implementation.

Feature-oriented Application organization:

- Company & Shops (one Company, multiple branches)
- Shop Access (ShopAccessCredential and ShopSession; session identity is the Shop)
- Employee Operations (Company-wide Employee credentials, EmployeeRoleAssignment and one-time EmployeeActionAuthorization)
- People & Customers (shared at Company level)
- Catalog & Inventory (shared catalogue; Product and Service subtypes; integer ShopInventory, reservations, transfers and selected returns)
- Sales (Shop-scoped)
- ServiceOrders (Shop-scoped)
- Refurbishment & Customer Claims (configurable categories, typed technical attributes, condition grades A/B/C, inspections, serialized physical units, 12-month commercial guarantee and tracked complaint/remedy cases)
- Finance (Shop-scoped PaymentTransaction + PaymentAllocation)
- CashManagement (one physical till per Shop in the initial release; opening float, Shop-scoped CashMovement, DailyClosing unique per Shop/business date, variance and immutable corrections)
- Reporting

### Module boundaries, layers and communication

- The feature areas above are the first-release modules. Each module owns its use cases, domain rules and tables; another module must not read or change those tables directly.
- A module exposes only the application operations other modules need. Cross-module work uses those in-process contracts synchronously. The Application layer orchestrates a use case that spans modules.
- Use one MySQL database and one EF Core `DbContext` in the initial release, with entity configurations grouped by owning module. A multi-module use case that must be atomic uses one database transaction.
- Do not add message brokers, distributed transactions or cross-module domain-event infrastructure in the initial release.
- Dependencies point inward: Domain has no project dependencies; Application depends on Domain; Infrastructure depends on Application and Domain; API depends on Application and Infrastructure and is the composition root. Frontend communicates with backend only through versioned HTTP API contracts.
- Domain contains business entities, value objects and invariants. Application contains use cases, cross-module orchestration, input validation and authorization. Infrastructure contains EF Core, database configuration and external adapters. API contains HTTP endpoints, DTO mapping, authentication/token validation and dependency composition.
- Keep persistence entities out of API contracts. Use asynchronous I/O and propagate `CancellationToken`. Return consistent Problem Details errors and centralize exception handling.
- Backend uses xUnit; frontend uses Vitest and React Testing Library. Unit tests cover domain/application behavior; integration tests cover API, persistence and module boundaries. Use MySQL-compatible integration coverage for persistence behavior.
- Apply the repository's .NET formatting and frontend lint/format checks in CI. Do not introduce additional architecture tooling before it solves a concrete problem.

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

- OAuth 2.0 access tokens protect `/api/v1/**`; OpenID Connect provides the Shop sign-in protocol. The default server implementation is OpenIddict with EF Core persistence.
- React uses Authorization Code + PKCE (`S256`) as a public SPA client. The API validates JWT Bearer tokens (signature, issuer, audience, expiry, not-before, token ID and scope).
- A token represents the Shop context only (`company_id`, `shop_id`, `shop_session_id`); it never contains EmployeeId or employee roles. The API rejects operations tied to revoked/expired ShopSessions.
- Employee password challenges and single-use EmployeeActionAuthorization remain separate business authorization controls for every protected operation.
- No Implicit grant, Resource Owner Password Credentials grant, hand-built JWTs, browser client secrets or token storage in localStorage/sessionStorage.

- Use a proven password hashing implementation.
- Do not log passwords, tokens or sensitive payment data.
- Store secrets outside source control.
- Apply least privilege to database accounts.
- Resolve the active Shop from the validated ShopSession; do not use a persistent Employee login identity.
- Employee accounts use one Company-wide password and the same credentials work in every Shop belonging to that Company. Roles MANAGER and TECHNICIAN are Company-wide.
- Require a fresh employee password challenge for every protected committed operation; consume a single-use EmployeeActionAuthorization atomically.
- Validate employee active state, Company ownership and Company-wide `EmployeeRoleAssignment` for the action in the active Shop context. The same password works in every Shop; no per-Shop employee assignment is required.
- Record a combined cash/card payment as one PaymentTransaction with multiple PaymentAllocation rows.
- Restrict inventory operations to Product records; Service has no stock balance. Product quantities are whole units.
- Model reserve/release separately from on-hand stock; signed quantity-tracked balances may be negative; consume sale stock at full settlement and service-order parts at full payment without waiting for physical PICKED/collection.
- Use StockTransfer dispatch/in-transit/receipt for inter-Shop movement; use selected StockReturn lines after REFUNDED/CANCELLED.
- Track each refurbished physical unit by RefurbishedUnit and category-driven typed technical attributes; support private unit images and a 12-month commercial guarantee beginning at handover.
- Persist one DailyClosing per `(ShopId, BusinessDate)`. Company-wide cash reporting, when authorized, is a read aggregation over individual Shop closings, not a consolidated close record.
- Audit sensitive operations.

## 8. Decisions still open

- ShopSession idle and absolute timeout values must be set and validated before production; access tokens remain short-lived with the 10-minute configurable default.
- Refresh tokens are disabled for the v1 SPA client. Reconsider only through a later ADR that includes rotation and reuse detection.
- Final OpenIddict package versions and signing-certificate deployment/rotation approach are Phase 2 security setup decisions.
- MANAGER full-access / TECHNICIAN restricted service-and-technical permission baseline is defined in [Employee Access and Roles](employee-access-and-roles.md); visibility within service context remains to be refined.
- Hosting and deployment provider.
- Report generation library.
- All image bytes are stored in MySQL `MEDIUMBLOB` with compact `BLOB` thumbnails. No external object storage or filesystem is used. See [MySQL Image Storage Architecture](mysql-image-storage.md). Exact GDPR retention/purge schedule remains pending.
- Reservation expiry automation and multiple stock locations are intentionally out of initial scope. One stock pool per Shop is the baseline. Refurbished grades A/B/C, inspection and complaint workflow are specified; serial uniqueness, acquisition/source details and guarantee coverage/exclusions still need review.
- VAT rates, legal VAT registration/status, tax-inclusive/exclusive prices and accountant-approved configuration; the Company VAT feature flag is part of the model.
- v1 tender methods are CASH and CARD only; card is recorded after external-terminal approval with no processor API integration. Refunds and deposit forfeitures are Manager-only, with original-tender refund and deposit rules in [Financial Model](financial-model.md). Accounting/bookkeeper reconciliation is deferred.

The one-Company/multiple-Shops model, OAuth/OIDC Shop token boundary and employee-action authentication flow are recorded in [Architecture Decision Records](architecture-decisions.md). See [API Authentication and Token Security](api-authentication-and-token-security.md).
