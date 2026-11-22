# Specifications

## Document map

| Area | Purpose |
|---|---|
| [Project overview](00-project/overview.md) | Goals, scope and constraints |
| [Legacy architecture](01-discovery/legacy-architecture.md) | Confirmed source structure |
| [Database schema audit](01-discovery/database-schema-audit.md) | DDL, keys, constraints and JPA mismatches |
| [Screen inventory](01-discovery/screen-inventory.md) | Swing views and navigation |
| [Domain inventory](01-discovery/domain-inventory.md) | Models, controllers and DAOs |
| [Business rules](01-discovery/business-rules.md) | Verified rules and open questions |
| [Risks and findings](01-discovery/risks-and-findings.md) | Security, data and design findings |
| [Target architecture](02-architecture/solution-architecture.md) | Proposed web architecture |
| [Architecture decisions](02-architecture/architecture-decisions.md) | Company/Shop structure, Shop session, superseded historical employee ADRs and financial decisions |
| [Target domain model](02-architecture/domain-model.md) | Conceptual modules, entities and invariants |
| [Reports, receipts and document printing](02-architecture/reports-and-document-printing.md) | Document catalogue, PDF generation, A4/80 mm print layouts, history and reprints |
| [Legacy report inventory](01-discovery/legacy-report-inventory.md) | Verified JRXML/Jasper files, ReportGenerator methods and active Swing triggers |
| [Document generation lifecycle](02-architecture/document-generation-lifecycle.mmd) | Snapshot, PDF render, print and reprint audit flow |
| [Document history ERD](02-architecture/er-diagram-documents.svg) | GeneratedDocument and DocumentReprintEvent data model |
| [Employee Management](04-features/employee-management.md) | Approved EMPLOYEE/MANAGER/ADMIN profiles, Company-wide Shop access, staff administration and credential lifecycle |
| [Employee access and roles — superseded](02-architecture/employee-access-and-roles.md) | Historical role matrix only; not authoritative for current profiles or permissions |
| [Relational database model](02-architecture/database-model.md) | Proposed logical/physical schema and constraints |
| [Financial model — Ireland](02-architecture/financial-model.md) | Service deposits, weekly refurbished instalments, partial/total refunds, tender configuration, EUR and cash rounding |
| [Cash closing policy](02-architecture/cash-closing-policy.md) | Opening float, daily Shop cash formula, physical count, electronic tender separation, variance and immutable corrections |
| [Refurbished condition and complaints](02-architecture/refurbished-condition-and-complaints.md) | A/B/C cosmetic grades, functional inspection, 12-month commercial guarantee and Irish customer complaint/remedy workflow |
| [Image upload policy](02-architecture/image-upload-policy.md) | File-size/count limits, allowed formats, secure processing and PostgreSQL BYTEA storage |
| [PostgreSQL image storage architecture](02-architecture/postgresql-image-storage.md) | Shared stored_image table, BYTEA content, Shop-scoped streaming and DB backup/retention |
| [Stock and refurbished equipment](02-architecture/stock-and-refurbishment.md) | Whole-unit stock, reservations, settlement, selected returns, Shop transfers and serialized refurbished products |
| [Service parts procurement — superseded](02-architecture/service-parts-procurement.md) | Historical discovery only; supplier purchasing, request rounds, receipts and tracking are out of scope |
| [Service parts purchase queue — superseded](02-architecture/service-procurement-queue.md) | Historical screen proposal; do not implement; see Parts to Buy in SO-01 |
| [Service parts procurement lifecycle — historical](02-architecture/service-parts-procurement-lifecycle.mmd) | Superseded supplier workflow diagram; retained for discovery history |
| [Inventory lifecycle](02-architecture/inventory-lifecycle.mmd) | Reserve/release/consume/return flow |
| [Stock transfer lifecycle](02-architecture/stock-transfer-lifecycle.mmd) | Dispatch, in-transit and receipt flow |
| [Service deposit lifecycle](02-architecture/service-deposit-lifecycle.mmd) | Receive at booking, hold, apply, refund or forfeit |
| [Refurbished installment lifecycle](02-architecture/refurb-installment-lifecycle.mmd) | Weekly schedule, layaway/credit-sale gate and handover |
| [ER diagram — overview](02-architecture/er-diagram.svg) | Rendered proposed relational DER |
| [ER diagram — Company/Shop/access/catalog/inventory](02-architecture/er-diagram-core.svg) | Readable detail view 1/3 |
| [ER diagram — stock, reservations, transfers and refurbishment](02-architecture/er-diagram-inventory.svg) | New inventory/refurbished detail view |
| [ER diagram — sales/finance/closing](02-architecture/er-diagram-financial.svg) | Readable detail view including deposits, instalments, refunds and tender configuration |
| [ER diagram — service parts procurement — historical](02-architecture/er-diagram-service-procurement.svg) | Superseded procurement data model; not part of the target schema |
| [ER diagram — after-sales/complaints/cash closing (SVG)](02-architecture/er-diagram-after-sales.svg) | Refurbished inspection, condition grades, complaint cases and Shop cash close/corrections |
| [Cash closing lifecycle](02-architecture/cash-closing-lifecycle.mmd) | Opening float, cash movements, count, variance and immutable close |
| [ER diagram — service parts procurement (PNG) — historical](02-architecture/er-diagram-service-procurement.png) | Superseded procurement model preview; retained as historical artifact |
| [ER diagram source — service parts procurement (Mermaid) — historical](02-architecture/er-diagram-service-procurement.mmd) | Superseded procurement model source; not authoritative for implementation |
| [ER diagram source — Mermaid](02-architecture/er-diagram.mmd) | Editable diagram source |
| [Authentication flow — Mermaid](02-architecture/authentication-flow.mmd) | OAuth Authorization Code + PKCE Shop login, JWT Bearer API access and fresh direct employee credential validation per protected operation |
| [API authentication and token security](02-architecture/api-authentication-and-token-security.md) | OAuth 2.0/OIDC profile, JWT validation, Shop-only claims, refresh/revocation and prohibited flows |
| [Authentication and signing-key persistence](02-architecture/authentication-persistence.md) | DATA-05 physical design for Shop credentials/sessions and `system_services` signing-key registry; old employee-action authorization table is superseded |
| [Backend guidelines](02-architecture/backend-guidelines.md) | .NET conventions, OAuth/OIDC and JWT API enforcement |
| [Frontend guidelines](02-architecture/frontend-guidelines.md) | React conventions |
| [Design system](03-design/design-system.md) | UX, visual direction and application shell |
| [Application shell and primary navigation](04-features/application-shell-and-navigation.md) | PC House header, active-Shop address, Home/Catalogue navigation and Settings hamburger |
| [SO-01 Service Orders](04-features/service-orders.md) | Shop-scoped repair workflow, device/customer, faults, items, status lifecycle and integration with parts, stock and finance |
| [SO-XFER Cross-Shop ServiceOrder Transfer](04-features/service-order-shop-transfer.md) | Shop-to-Shop transfer workflow, custody acceptance, financial-liability handoff, parts, authorization and audit |
| [Feature template](04-features/feature-template.md) | Template for each feature SPEC |
| [Testing strategy](05-testing/testing-strategy.md) | Quality strategy |
| [Migration backlog](06-migration/migration-backlog.md) | Phased implementation plan |

## Status vocabulary

- `DISCOVERY`: source analysis in progress.
- `DRAFT`: specification drafted, not approved.
- `APPROVED`: requirements and acceptance criteria reviewed.
- `IN_PROGRESS`: implementation started.
- `VALIDATION`: tests and acceptance checks in progress.
- `DONE`: acceptance criteria satisfied.

## Rule

A feature SPEC must cite Java source paths and methods wherever behavior is claimed to be inherited from the legacy application. Proposed changes must be explicitly marked REDESIGN.

## Expanded discovery reports

- [Deep-dive findings](01-discovery/deep-dive-findings.md)
- [Legacy company/query audit (target model now uses Shop scope)](01-discovery/multi-company-and-query-audit.md)
- [Financial workflows](01-discovery/financial-workflows.md)
- [Relationship validation matrix](01-discovery/relationship-validation.md)
- [Security and authentication](01-discovery/security-and-authentication.md)
- [Service-order lifecycle](01-discovery/service-order-lifecycle.md)
- [Regression matrix](05-testing/regression-matrix.md)

## Domain decisions and target data model

The decision register is recorded in [Architecture Decision Records](02-architecture/architecture-decisions.md). See [Financial Model — Ireland](02-architecture/financial-model.md) for deposits, instalments, refunds and monetary localization. Financial behavior for the Ireland deployment is described in [Financial Model](02-architecture/financial-model.md). The target context is one Company with multiple Shops. A ShopSession identifies the active Shop; Company-wide employee accounts use exactly one active profile from EMPLOYEE/MANAGER/ADMIN across every Shop. Each protected operation defined by its feature SPEC requires fresh direct employee credential validation; no EmployeeActionAuthorization token is used. Product stock is integer-only and may become negative; deductions occur at full settlement, with reservations, explicit selected returns and inter-Shop transfers. Refurbished equipment uses configurable categories and serialized physical units. The model remains a draft where explicitly stated. The approved Parts to Buy list combines calculated uncovered ServiceOrder requirements and separate manual reminders; it is not a supplier procurement, purchase, delivery or receipt workflow.


## Current owner decisions — authoritative summary

- One Company with multiple Shops; ShopSession authenticates only the active Shop.
- Active Company-wide employee profiles are exactly `EMPLOYEE`, `MANAGER` and `ADMIN`; no dual-role assignment. `ADMIN` inherits `MANAGER` permissions except where a feature explicitly restricts an action.
- Every protected operation defined by its owning feature SPEC validates the acting employee's credentials directly and freshly in the backend. Do not use `EmployeeActionAuthorization`.
- Payment refunds and deposit refunds are permitted to `MANAGER` and `ADMIN`; deposit forfeiture is `MANAGER`-only. Refunds return to the original tender in v1.
- Parts to Buy is a visibility/reminder list only. Supplier selection, purchases, request rounds, receipt, ETA and shipment tracking are superseded and must not be implemented.
- Product quantities are whole units; `qty` means physical on-hand stock. There is no `min_qty` or minimum-stock threshold.
- See [Product-owner decisions](02-architecture/product-owner-decisions.md), [Employee Management](04-features/employee-management.md), [SO-01 Service Orders](04-features/service-orders.md), [Financial Model](02-architecture/financial-model.md) and [Stock and Refurbishment](02-architecture/stock-and-refurbishment.md) for normative details.

Older versioned decision summaries below this point have been removed from the active index to avoid presenting superseded decisions as current.
