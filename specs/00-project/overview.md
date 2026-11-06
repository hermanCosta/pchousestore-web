# pcHouseStore Web — Project Overview

**Status:** DRAFT  
**Product:** pcHouseStore Web  
**Legacy source:** `hermanCosta/pchousestoremvn`

## 1. Context

The current product is a Java desktop application built with Swing, Maven, Jakarta Persistence/Hibernate and MySQL. The new product will replace the desktop experience with a browser-based application.

## 2. Goals

- Provide access through modern web browsers.
- Redesign navigation and workflows for desktop, tablet and mobile.
- Separate presentation, application logic, domain rules and persistence.
- Preserve validated business behavior where it remains relevant.
- Improve security, observability, testability and maintainability.
- Establish a modular foundation for sales, service orders, refurbishment and financial operations.

## 3. Target stack

### Frontend
- React
- TypeScript with strict mode
- Vite
- React Router
- TanStack Query

### Backend
- .NET 10
- ASP.NET Core Web API
- Entity Framework Core
- FluentValidation (subject to final dependency review)

### Persistence
- MySQL
- EF Core migrations
- Monetary precision with DECIMAL columns and C# decimal values

### Tests
- xUnit
- ASP.NET Core integration tests
- Vitest
- React Testing Library

## 4. Architecture decisions

Start as a modular monolith. Do not introduce microservices unless a concrete operational or domain need is demonstrated.

The following domain choices have been recorded; detailed consequences and open points are in [Architecture Decision Records](../02-architecture/architecture-decisions.md):

- Organizational model: one Company with multiple operational Shops (stores/branches); shared master data, branch-specific operations.
- Authentication: the persistent session belongs to the Shop. Each protected business operation requires the executing employee's own credential and a single-use authorization.
- Inventory: only Product catalogue entries are stock-managed; quantities are whole units and balances are per Shop. Reservations are explicit and cancellable; sale settlement and service-order full payment post stock. Transfers are dispatched and received between Shops. Refurbished equipment has configurable categories, technical attributes and serialized physical units.
- Payments: one transaction may contain multiple tender allocations (for example, cash and card).
- Returns: for REFUNDED/CANCELLED operations, the operator selects the consumed product lines/quantities physically returned to stock; monetary refund does not automatically restock goods.
- Refurbishment: configurable categories (PCs, consoles, TVs, etc.), category-specific technical attributes and one record per physical unit.
- Cash closing: one independent close per Shop and business date, using only that Shop’s transactions and cash movements.

The proposed conceptual and relational models are DRAFT, not yet approved for production migrations.

## 5. Migration strategy

- Treat the Java repository as an immutable reference during discovery.
- Create a new database schema; no production data needs to be migrated.
- Preserve domain knowledge, not necessarily the old physical schema.
- Reassess entity relationships and constraints before designing the new schema.
- No dual-running requirement has been specified.

## 6. Out of scope for initial discovery

- Production deployment.
- Real payment-provider integration unless found in source and explicitly approved.
- Importing historical production records.
- Automatic code translation from Java to C#.

## 7. Success criteria

- Each retained legacy capability has a documented destination or an explicit retirement decision.
- Each implemented feature has an approved SPEC and acceptance tests.
- Business-critical workflows are validated before release.
- Secrets are not stored in source code.


## Additional product-owner decisions (v13)

- Employee passwords and Company-wide `MANAGER`/`TECHNICIAN` roles work across all Shops. Each protected operation still requires a fresh employee password challenge; the ShopSession remains employee-free.
- Quantity-tracked product stock may be negative; Sale/ServiceOrder stock is deducted at full settlement, not partial payment or pickup.
- Company VAT enable/disable flag is preimplemented and defaults off with tax setup `NOT_REVIEWED`; the flag is not a legal VAT determination.
- Sold refurbished units receive a 12-month commercial guarantee from handover, without limiting Irish statutory rights.
- Supplier purchases need no second manager approval; multiple private image uploads are supported for procurement requests and refurbished units.
- The stock deduction trigger is full payment settlement (balance due reaches zero); quantity-tracked inventory may become negative.
- Employee password credentials and MANAGER/TECHNICIAN roles are global across all Shops; the employee is re-authenticated for each protected operation.


See [Product-owner decisions and remaining questions](../02-architecture/product-owner-decisions.md) for the authoritative consolidated register.
