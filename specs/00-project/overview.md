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

## 4. Architecture decision

Start as a modular monolith. Do not introduce microservices unless a concrete operational or domain need is demonstrated.

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
