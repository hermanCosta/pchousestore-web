# CAT-01 — Customers and Shared Catalogues

**Status:** APPROVED — product-owner approval received 2026-11-13. Implementation authorized.
**Scope:** Company-shared customer records, Product/Service catalogue, product categories, and Fault catalogue. Employee administration is delivered separately as EMP-01. Sales, service-order lifecycle, inventory movements and reports are consuming features, not implemented by this SPEC.
**Language:** All user-facing text is English.

## 1. Goal

Define the customer and shared catalogue foundation used by every Shop in one Company, with Company scoping enforced on reads and writes. Preserve historical references when customer or catalogue records are deactivated. This draft does not authorize implementation until all business-critical OPEN items are resolved and the SPEC is approved.

## 2. Legacy source references

See the detailed method inventory in [`customer-catalog-feature-audit.md`](../01-discovery/customer-catalog-feature-audit.md). Primary sources:

- `views/CustomerView.java`, `views/modals/CustomerModal.java`
- `controllers/CustomerController.java`, `dao/CustomerDAO.java`, `models/Customer.java`
- `controllers/PersonController.java`, `dao/PersonDAO.java`, `models/Person.java`
- `views/ProductServiceView.java`, `views/modals/ProductServiceModal.java`
- `controllers/ProductServiceController.java`, `dao/ProductServiceDAO.java`, `models/ProductService.java`
- `views/FaultView.java`, `controllers/FaultController.java`, `dao/FaultDAO.java`, `models/Fault.java`
- `views/NewSaleView.java`, `views/NewServiceOrderView.java`, `models/SaleProdServ.java`, `models/ServiceOrderProdServ.java`, `models/ServiceOrderFault.java`

## 3. Confirmed legacy behavior

- Customer form requires first name, last name and contact number; email is optional. The UI normalizes names to uppercase, strips contact formatting, and lowercases email.
- Customer search supports person fields and contact lookup. A customer can be selected for sale, service order and refurbished sale flows.
- Legacy customer persistence links `Customer` to `Person` and Company. Customer deletion hard-deletes the Customer row, leaving Person.
- ProductService stores name, price (`double`), integer quantity/minimum, free-text category and note. UI choices are `PRODUCT`, `SERVICE`, `OTHER`; quantity/minimum default to 1; add/update/delete are available and delete is hard.
- Legacy minimum-stock query uses a fixed quantity threshold of 2, despite storing minimum quantity.
- Fault form requires description only; duplicate detection is exact-description; deletion is hard. The Java model has no Fault code although the supplied DDL contains a nullable unique code.
- Existing customer/catalogue searches have inconsistent or missing Company scoping. These are defects, not target behavior.

## 4. Approved target constraints

- Customers and catalogues are shared by all Shops in the Company.
- Customer profile fields: required first name, last name and phone; optional email. The target should use C# `decimal` and PostgreSQL `NUMERIC` for money; no floating-point money.
- Customer email may repeat; do not enforce email uniqueness.
- Catalogue model separates `CatalogItem` kind `PRODUCT`/`SERVICE`; `OTHER` is removed. SKU is optional. When present, it matches the approved format and uniqueness rules. Only Products have stock. Product categories are configurable Company data, not a hard-coded enum.
- A Customer with the same Company phone in E.164 form cannot be created twice; a match blocks the create, including when the existing Customer is soft-deleted. Phone input defaults to Ireland; an “International number” checkbox switches the phone text field to international input. No automatic customer merge is performed.
- Fault description remains the primary display/search/selection value. Fault code is generated automatically as `FLT-` plus a Company sequence padded to at least six digits (for example `FLT-000001`); it is immutable, unique within the Company and never reused.
- SKU is optional for Products and Services. When present, it must match `SKU-[A-Z0-9]{8}`, is normalized to uppercase and unique per Company, including inactive records.
- Shop inventory is separate, per Shop, and quantity tracking uses whole units.
- Minimum stock is set per Shop; low-stock reporting compares that Shop's on-hand Product quantity with its configured minimum.
- Technician can read the full Company customer directory. Customer/catalogue records marked inactive remain visible in existing records and history but cannot be selected for new operations. Legacy `OTHER` entries are excluded and not migrated.
- An authenticated Shop session may create/edit/deactivate/reactivate customer records, Products, Services, categories, sale prices and Fault catalogue entries without an employee challenge. Refunds remain Manager-only and employee-protected.
- V1 uses soft deletion. Historical sale/service-order lines must retain stable snapshots/references as defined in their feature specifications.
- All queries and mutation targets must be scoped to the authenticated Company. Shop-specific inventory is scoped to the active Shop.

## 5. Proposed redesign (pending CAT-01 approval)

- Customer directory with Company-wide search, details, create/edit and deactivate actions.
- Product and Service catalogue screens share search and lifecycle controls while showing type-specific fields. Product forms manage category and stock-tracking configuration; per-Shop quantities/minimums are managed in inventory workflows.
- Fault catalogue is a Company-shared list for service-order selection. Service-order technical findings remain a separate operational record referencing a catalogue fault when applicable.
- Normalize phone input for matching while preserving a formatted display value. Do not merge customer identities automatically.
- Store immutable descriptive and price snapshots on committed Sale/ServiceOrder lines so later catalogue edits do not rewrite history.

Inactive records remain visible on existing records and history, but cannot be selected in new operations. Committed order-line snapshots and references remain unchanged.

## 6. Business rules and invariants

### Confirmed

- Company owns shared customers and catalogue entries; a Shop is not the owner of a customer/catalogue row.
- Product stock is per Shop and not stored as a shared catalogue quantity.
- Managers and Technicians may manage catalogue prices/categories under the approved role matrix.
- Soft deletion is the v1 baseline.

### Resolved business rules

1. Customer phone defaults to Irish input. The “International number” checkbox switches the field to international input. Store and compare as E.164. A matching Company phone blocks Customer creation, including when the existing record is inactive; no automatic merge.
2. Technician can read the Company customer directory and create/edit customer records.
3. Customer email is optional and may repeat; no email unique constraint.
4. Remove `OTHER` from the target and do not migrate legacy `OTHER` entries.
5. SKU is optional. When present, it matches `SKU-[A-Z0-9]{8}`; normalize uppercase, unique per Company and reserved after deactivation.
6. Product minimum stock is configured per Shop and drives that Shop's low-stock report.
7. Fault descriptions are the primary display/search/selection values. Generate immutable Company-unique `FLT-` sequence codes, padded to at least six digits, with no reuse.
8. Technician may manage Products, Services, categories and prices and create/edit Fault catalogue entries. Refunds remain Manager-only.
9. Inactive records remain visible on existing records/history and cannot be selected for new operations. Existing drafts may display already-linked inactive records but cannot add another reference to them. MANAGER and TECHNICIAN may reactivate records.

### Deferred details

- Phone output formatting and exact English invalid-number/duplicate messages are UI details; canonical storage and matching use E.164.
- Exact customer retention/purge schedule remains deferred in `product-owner-decisions.md`; v1 uses soft deletion without automated purge.

## 7. Data model and relationships

The current logical draft in `specs/02-architecture/database-model.md` contains:

- `customer(company_id, person_id, phone_e164, notes, active)` linked to Company and Person. `phone_e164` is required and unique per Company, including soft-deleted records. Optional email may repeat.
- `catalog_item(company_id, kind, name, optional sku, list_price, active)` with any supplied SKU matching `SKU-[A-Z0-9]{8}`, normalized uppercase, Company-unique and reserved after deactivation; `kind` is PRODUCT or SERVICE. No `OTHER` kind.
- `product(catalog_item_id, product_category_id, inventory_tracking_mode, ...)` for Product-only fields.
- `service(catalog_item_id, description)` for Service-only fields.
- `product_category(company_id, parent_category_id, name, code, description, is_active)`.
- `shop_inventory(shop_id, product_catalog_item_id, quantity_on_hand, quantity_reserved, minimum_quantity)` for Shop stock; minimum quantity is per Shop and drives low-stock reporting.
- `fault(company_id, code, description, active)`; code is automatically generated as `FLT-` plus a Company sequence padded to at least six digits, immutable and never reused. Description is the primary display/search/selection value.

This is a logical draft. Enforce Company and phone uniqueness, case-insensitive SKU uniqueness including inactive rows, and a transactional Company-scoped Fault code sequence. Exact physical indexes, audit fields and foreign-key delete constraints are implementation details to review with the migration.

## 8. API contracts

All endpoints are under `/api/v1`, require the authenticated and active Shop session, derive `companyId` and `shopId` from that session, and use DTOs rather than EF entities. Search/list reads and all Customer/Product/Service/category/Fault writes require no individual employee credential challenge. Mutations remain Company-scoped and audit the ShopSession that performed them.

| Resource | Contract | Authorization / behavior |
|---|---|---|
| Customers | `POST /customers/queries` with `{ search?, includeInactive?, page, pageSize }`; `POST /customers`; `GET /customers/{id}`; `PUT /customers/{id}`; `POST /customers/{id}/deactivate`; `POST /customers/{id}/reactivate` | Active Shop session only; no employee challenge. Input: firstName, lastName, phone, isInternational, email?. API canonicalizes phone to E.164 and returns 409 `duplicate_customer_phone` on collision. DTO exposes `phoneE164`; no cross-Company data is returned. |
| Catalogue items | `POST /catalog-items/queries`; `POST /catalog-items`; `GET /catalog-items/{id}`; `PUT /catalog-items/{id}`; `POST /catalog-items/{id}/deactivate`; `POST /catalog-items/{id}/reactivate` | Active Shop session only; no employee challenge. DTO has kind PRODUCT/SERVICE, name, optional SKU, decimal listPrice, isActive and subtype fields. If supplied, normalize SKU uppercase and validate `SKU-[A-Z0-9]{8}`. |
| Product categories | `POST /product-categories/queries`; `POST /product-categories`; `PUT /product-categories/{id}`; `POST /product-categories/{id}/deactivate`; `POST /product-categories/{id}/reactivate` | Active Shop session only; no employee challenge. DTO contains name, code, optional parentCategoryId, description and isActive. |
| Faults | `POST /faults/queries`; `POST /faults`; `GET /faults/{id}`; `PUT /faults/{id}`; `POST /faults/{id}/deactivate`; `POST /faults/{id}/reactivate` | Active Shop session only; no employee challenge. Create/update accepts description only; server generates and returns immutable code. Description is the primary search/selection field. |

Query DTOs return paged results and include inactive records only when `includeInactive=true`; inactive items remain available to history readers but are omitted from new-operation selectors. Return 404 for IDs outside the active Company. Validation errors use the API's Problem Details format. Per-Shop minimum quantity belongs to ShopInventory/inventory APIs, not the shared catalogue-item endpoint.

## 9. UI flows and states

### Customer Directory

- Search Company-wide by name, entered/canonical phone and email. Results show name, phone and email; inactive records are hidden by default and available through an inactive filter.
- Create/edit requires first name, last name and phone; email is optional and may repeat. These operations use the authenticated Shop session and do not request employee credentials.
- Phone input defaults to Ireland. “International number” checkbox switches the phone field to international input. Validate and canonicalize to E.164; duplicate phone blocks save with an English error.
- Create, edit and deactivate require the active Shop session and do not invoke an employee credential challenge.

### Product/Service Catalogue and Categories

- One catalogue supports Product and Service types only. `OTHER` is not offered.
- SKU is optional. If entered, use placeholder `SKU-AB12CD34`; normalize uppercase and validate the exact eight-character suffix.
- Product fields include name, SKU, sale price and category. Service fields include name, SKU, sale price and description. Product stock/minimum belongs to Shop inventory workflows.
- Categories are Company-wide. Inactive items are available in history views but cannot be selected in new operations. An authenticated Shop session can reactivate an item; existing drafts may display a previously selected inactive item but cannot add another line/reference to it.

### Fault Catalogue

- Create/edit uses description as the primary field. Generated code is displayed read-only (example `FLT-000001`) and is not the primary search/selection value.
- Fault search uses description; inactive faults cannot be added to new ServiceOrders.

Every screen includes loading, empty, validation, API error and unauthorized states. User-facing text is English. Customer and catalogue writes do not open the employee credential challenge; the API still requires a valid Shop session.

## 10. Authorization

- Authenticated Shop session supplies the Shop and Company context; it does not identify an employee.
- Customer, Product, Service, category, price and Fault mutations require an active Shop session and Company scope; they do not require an employee challenge.
- These mutations are audited against the authenticated ShopSession. All reads and writes enforce Company scope.
- Employee challenge rules apply only to actions whose feature SPEC requires them. Refunds remain Manager-only and employee-protected.

## 11. Acceptance criteria

Acceptance criteria:

- No cross-Company customer/catalogue/fault reads or mutations.
- Customer phone defaults to Ireland; international checkbox accepts international input; both normalize to E.164.
- A matching E.164 customer phone in the Company, including an inactive record, returns a hard conflict and creates no duplicate.
- Repeated customer email is accepted.
- Catalogue items can be saved without an SKU. A supplied SKU matches `SKU-[A-Z0-9]{8}`, is normalized uppercase, and cannot be duplicated within the Company even if the previous item is inactive.
- New Faults receive immutable, unique `FLT-` sequence codes; description remains primary in search/selection.
- `OTHER` is neither offered nor imported into the target catalogue.
- Low-stock comparison uses the active Shop's `ShopInventory.minimum_quantity`.
- Inactive customer/catalogue records cannot be used in new operations but remain readable from existing history.
- An authenticated Shop session can reactivate an inactive record; an existing draft can display its linked inactive item but cannot add another reference to it.
- Customer and catalogue CRUD works without an employee challenge but is restricted to the authenticated Shop's Company and is audited against the ShopSession; refunds remain Manager-only and employee-protected.
- A Shop cannot assign inventory to a Service or to a Product belonging to another Company.
- Money uses decimal/Numeric, never binary floating point.
- Inactive records do not disappear from historical committed records.
- Mutations are audited with the ShopSession actor; no employee identity or one-use employee authorization is required for CAT-01 writes.
- User-facing screens and validation messages are in English.

## 12. Test scenarios

Expected coverage includes Company-scope isolation; Irish/international phone parsing and duplicate blocking; repeated email acceptance; saving catalogue items without SKU; supplied-SKU validation/case normalization/uniqueness including inactive rows; concurrent Fault code generation; Product-vs-Service constraints; Shop-specific stock minimum; inactive-item handling in new vs existing records; ShopSession-only write authorization; and ShopSession audit attribution. Add tests after this SPEC is approved.

## 13. Approval

**Approved 2026-11-13; Shop-session-only authorization for Customer/Catalogue writes confirmed 2026-11-14.** The exact customer retention/purge schedule remains separately deferred. Related documents: [`customer-catalog-feature-audit.md`](../01-discovery/customer-catalog-feature-audit.md), `specs/02-architecture/product-owner-decisions.md`, `domain-model.md`, `database-model.md`, and `employee-access-and-roles.md`.
