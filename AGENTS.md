# Agent Instructions — pcHouseStore Web

## Mission

Rebuild the legacy pcHouseStore desktop application as a modern web application using React, TypeScript and ASP.NET Core.

## Source of truth

- Legacy repository: https://github.com/hermanCosta/pchousestoremvn
- Discovery documentation: `specs/01-discovery/`
- Approved feature specifications: `specs/04-features/`

## Mandatory workflow

For every feature:

1. Inspect the corresponding Java Swing view, controller, DAO, model, enum and persistence mapping.
2. Record source-file and method references in the feature specification.
3. Separate confirmed legacy behavior, unresolved questions and proposed redesign.
4. Do not infer missing business rules.
5. Update or create the SPEC before implementation.
6. Implement only against an approved SPEC.
7. Add unit, integration and frontend tests where applicable.
8. Verify acceptance criteria and update the migration backlog.

## Technical constraints

- Backend: .NET 10, ASP.NET Core Web API, EF Core, MySQL.
- Frontend: React, TypeScript strict mode, Vite.
- Architecture: modular monolith; avoid premature microservices.
- Monetary values: C# `decimal` and MySQL `DECIMAL`, never binary floating point.
- Use DTOs for API contracts; do not expose persistence entities.
- Use asynchronous I/O and propagate CancellationToken.
- Centralize validation and exception handling.
- Use secure password hashing and established authentication mechanisms; never reproduce plaintext or reversible password handling from the legacy system.
- Model one Company with multiple Shops. The persistent session authenticates the Shop, not an employee.
- Require an individual employee credential challenge for each protected committed operation and consume a short-lived, single-use EmployeeActionAuthorization atomically.
- Keep secrets out of source control; use environment configuration or secret stores.
- Do not execute destructive SQL or change the legacy repository.

## Evidence labels

- **CONFIRMED** — directly verified in source code or configuration.
- **INFERRED** — plausible interpretation that still requires validation.
- **OPEN** — insufficient evidence; requires product-owner clarification.
- **REDESIGN** — proposed new behavior, not a legacy rule.

## Scope control

Do not implement a feature while its business-critical rules remain OPEN. Record the blocker and continue with independent discovery tasks.

## Current domain decision register

Use `specs/02-architecture/product-owner-decisions.md` as the consolidated product-owner decision ledger. A decision is not approved merely because it appears in a proposed role/action matrix.
