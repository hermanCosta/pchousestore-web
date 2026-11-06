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
| [Architecture decisions](02-architecture/architecture-decisions.md) | Company/Shop structure, Shop session, employee action authorization and financial decisions |
| [Target domain model](02-architecture/domain-model.md) | Conceptual modules, entities and invariants |
| [Reports, receipts and document printing](02-architecture/reports-and-document-printing.md) | Document catalogue, PDF generation, A4/80 mm print layouts, history and reprints |
| [Legacy report inventory](01-discovery/legacy-report-inventory.md) | Verified JRXML/Jasper files, ReportGenerator methods and active Swing triggers |
| [Document generation lifecycle](02-architecture/document-generation-lifecycle.mmd) | Snapshot, PDF render, print and reprint audit flow |
| [Document history ERD](02-architecture/er-diagram-documents.svg) | GeneratedDocument and DocumentReprintEvent data model |
| [Employee access and roles](02-architecture/employee-access-and-roles.md) | Company-wide employee password and MANAGER/TECHNICIAN roles across all Shops |
| [Relational database model](02-architecture/database-model.md) | Proposed logical/physical schema and constraints |
| [Financial model — Ireland](02-architecture/financial-model.md) | Service deposits, weekly refurbished instalments, partial/total refunds, tender configuration, EUR and cash rounding |
| [Cash closing policy](02-architecture/cash-closing-policy.md) | Opening float, daily Shop cash formula, physical count, electronic tender separation, variance and immutable corrections |
| [Refurbished condition and complaints](02-architecture/refurbished-condition-and-complaints.md) | A/B/C cosmetic grades, functional inspection, 12-month commercial guarantee and Irish customer complaint/remedy workflow |
| [Image upload policy](02-architecture/image-upload-policy.md) | File-size/count limits, allowed formats, secure processing and MySQL-only BLOB storage |
| [MySQL image storage architecture](02-architecture/mysql-image-storage.md) | Shared stored_image table, MEDIUMBLOB content, bounded BLOB thumbnails, Shop-scoped streaming and DB backup/retention |
| [Stock and refurbished equipment](02-architecture/stock-and-refurbishment.md) | Whole-unit stock, reservations, settlement, selected returns, Shop transfers and serialized refurbished products |
| [Service parts procurement](02-architecture/service-parts-procurement.md) | Supplier/source links, Shop-wide purchase queue, multiple request rounds per service, reference images, ETA/tracking, partial receipts, allocations and AWAITING_PARTS state |
| [Service parts purchase queue screen](02-architecture/service-procurement-queue.md) | Queue screen, ServiceOrder ID search, create/manage requests, reference-image upload, buyer assignment and acceptance criteria |
| [Service parts procurement lifecycle](02-architecture/service-parts-procurement-lifecycle.mmd) | Required part → supplier order → partial/full receipt → reservation → resume repair |
| [Inventory lifecycle](02-architecture/inventory-lifecycle.mmd) | Reserve/release/consume/return flow |
| [Stock transfer lifecycle](02-architecture/stock-transfer-lifecycle.mmd) | Dispatch, in-transit and receipt flow |
| [Service deposit lifecycle](02-architecture/service-deposit-lifecycle.mmd) | Receive at booking, hold, apply, refund or forfeit |
| [Refurbished installment lifecycle](02-architecture/refurb-installment-lifecycle.mmd) | Weekly schedule, layaway/credit-sale gate and handover |
| [ER diagram — overview](02-architecture/er-diagram.svg) | Rendered proposed relational DER |
| [ER diagram — Company/Shop/access/catalog/inventory](02-architecture/er-diagram-core.svg) | Readable detail view 1/3 |
| [ER diagram — stock, reservations, transfers and refurbishment](02-architecture/er-diagram-inventory.svg) | New inventory/refurbished detail view |
| [ER diagram — sales/finance/closing](02-architecture/er-diagram-financial.svg) | Readable detail view including deposits, instalments, refunds and tender configuration |
| [ER diagram — service parts procurement](02-architecture/er-diagram-service-procurement.svg) | Service-order-linked purchase requests, line-level reference images, suppliers, purchase allocations and actual receipts |
| [ER diagram — after-sales/complaints/cash closing (SVG)](02-architecture/er-diagram-after-sales.svg) | Refurbished inspection, condition grades, complaint cases and Shop cash close/corrections |
| [Cash closing lifecycle](02-architecture/cash-closing-lifecycle.mmd) | Opening float, cash movements, count, variance and immutable close |
| [ER diagram — service parts procurement (PNG)](02-architecture/er-diagram-service-procurement.png) | Image preview of request rounds, request lines, photos, supplier orders and receipt allocations |
| [ER diagram source — service parts procurement (Mermaid)](02-architecture/er-diagram-service-procurement.mmd) | Editable focused DER source including request rounds and images |
| [ER diagram source — Mermaid](02-architecture/er-diagram.mmd) | Editable diagram source |
| [Authentication flow — Mermaid](02-architecture/authentication-flow.mmd) | OAuth Authorization Code + PKCE Shop login, JWT Bearer API access, employee authorization per protected operation |
| [API authentication and token security](02-architecture/api-authentication-and-token-security.md) | OAuth 2.0/OIDC profile, JWT validation, Shop-only claims, refresh/revocation and prohibited flows |
| [Backend guidelines](02-architecture/backend-guidelines.md) | .NET conventions, OAuth/OIDC and JWT API enforcement |
| [Frontend guidelines](02-architecture/frontend-guidelines.md) | React conventions |
| [Design system](03-design/design-system.md) | UX and visual direction |
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

The decision register is recorded in [Architecture Decision Records](02-architecture/architecture-decisions.md). See [Financial Model — Ireland](02-architecture/financial-model.md) for deposits, instalments, refunds and monetary localization. Financial behavior for the Ireland deployment is described in [Financial Model](02-architecture/financial-model.md). The target context is one Company with multiple Shops. A ShopSession identifies the active Shop; Company-wide employee passwords and MANAGER/TECHNICIAN roles work across every Shop; each protected operation requires a fresh employee password and one-time action authorization. Product stock is integer-only and may become negative; deductions occur at full payment settlement, with optional reservations, explicit selected returns and inter-Shop transfers. Refurbished equipment uses configurable categories and serialized physical units. The model remains a draft: unresolved policies are listed explicitly in the domain and database model documents. Supplier procurement is operationally modelled through a Shop-wide queue with multiple request rounds per ServiceOrder and private reference images; accounts payable, supplier returns and marketplace integrations are outside initial scope.


## Current owner decisions (v14)

- One Company with multiple Shops; ShopSession authenticates only the active Shop.
- Company-wide Employee account/password and MANAGER/TECHNICIAN roles work across all Shops; each protected committed operation requires a fresh employee password authorization.
- Product quantities are whole units. Negative quantity-tracked balances are allowed; stock is deducted when the sale/service balance reaches zero through settled payments.
- Application starts with VAT disabled and adds no VAT amount while off; legal VAT status remains a separate Ireland review.
- Every sold refurbished unit has a 12-month commercial guarantee from handover.
- Service-parts purchases do not require a second manager approval; multiple private image uploads are supported for procurement request lines and refurbished units. MANAGER has full access; TECHNICIAN is restricted to service/technical/procurement and inspection actions. See employee-access-and-roles.md.


- [Product-owner decisions and remaining questions](02-architecture/product-owner-decisions.md)


## v15 product-owner updates

- [Payment methods/refunds/deposits](02-architecture/financial-model.md) now defines CASH/CARD-only, external-terminal manual card capture, Manager-only original-tender refunds and the no-auto-expiry deposit policy.
- [MySQL image storage](02-architecture/mysql-image-storage.md) stores image bytes directly in MySQL using MEDIUMBLOB/BLOB; no external file/object storage.
- Refurbished asset codes/serial numbers and proposed commercial-guarantee exclusions are documented.
- Installment arrears beyond `OVERDUE`, late fees and renegotiation are deferred; accounting reconciliation is not a v1 blocker.

## v14 product-owner updates

- [Cash closing policy](02-architecture/cash-closing-policy.md) defines the daily Shop formula and opening float.
- [Refurbished condition and complaints](02-architecture/refurbished-condition-and-complaints.md) defines A/B/C grades and the claim workflow.
- [Image upload policy](02-architecture/image-upload-policy.md) defines initial file limits and secure upload requirements.


## v16 — brand identity and public business profile

- [PC House brand identity](03-design/brand-identity.md)
- [Logo/source asset references](03-design/brand-assets/README.md)
- [Website-derived Shop, product and contact information](01-discovery/business-profile-from-website.md)
- [CSS theme tokens](03-design/pc-house-theme.css)
- [v16 changelog](../CHANGELOG-v16.md)


## v18 — reports, receipts and document printing

- [Reports, receipts and document printing](02-architecture/reports-and-document-printing.md)
- [Legacy report and print inventory](01-discovery/legacy-report-inventory.md)
- [Document generation lifecycle](02-architecture/document-generation-lifecycle.mmd)
- [Document history ERD — SVG](02-architecture/er-diagram-documents.svg)
- [Document history ERD — PNG](02-architecture/er-diagram-documents.png)
