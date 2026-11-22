# CAT-01 — Customers and Shared Catalogues

**Status:** APPROVED — product-owner approval received 2026-11-13. Implementation authorized.
**Scope:** Company-shared customer records, Product/Service catalogue, product categories, and Fault catalogue. Employee administration is delivered separately as EMP-01. Sales, service-order lifecycle, inventory movements and reports are consuming features, not implemented by this SPEC.
**Language:** All user-facing text is English.

## 1. Goal

Define the approved customer and shared catalogue foundation used by every Shop in one Company, with Company scoping enforced on reads and writes. Preserve historical references when customer or catalogue records are deactivated. CAT-01 is approved; implementation status is tracked separately from this specification.

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
- Customer profile fields: required first name, last name and phone; optional email. Catalogue Product and Service sale prices are required, must be greater than or equal to zero, and use C# `decimal` with PostgreSQL `NUMERIC(19,2)`; negative prices are invalid and zero-price items are allowed. No floating-point money.
- Customer email may repeat; do not enforce email uniqueness.
- Catalogue model separates `CatalogItem` kind `PRODUCT`/`SERVICE`; `OTHER` is removed. No SKU field or SKU workflow exists in the target catalogue. Product and Service sale prices are required and non-negative; zero is allowed. Only Products have stock. Product categories are configurable Company data, shared by every Shop, not a hard-coded enum. Every Product must belong to exactly one category; Services do not use product categories.
- A Customer with the same Company phone in E.164 form cannot be created or updated to duplicate another Customer's phone; a match blocks the operation, including when the other Customer is soft-deleted. When editing, the current Customer record is excluded from the duplicate check so its phone can remain unchanged. Phone input defaults to Ireland; an “International number” checkbox switches the phone text field to international input. No automatic customer merge is performed.
- Fault description remains the primary display/search/selection value. Fault code is generated automatically as `FLT-` plus a Company sequence padded to at least six digits (for example `FLT-000001`); it is immutable, unique within the Company and never reused.
- Shop inventory is separate, per Shop, and quantity tracking uses whole units.
- Shop stock records contain only the physical on-hand quantity (`quantity_on_hand`) and active reservation quantity (`quantity_reserved`). There is no minimum-stock (`min_qty` / `minimum_quantity`) field, threshold or low-stock alert/report in the approved model.
- Customer directory access is governed by the authenticated Shop session, not by an employee role. Customer/catalogue records marked inactive remain visible in existing records and history but cannot be selected for new operations. Legacy `OTHER` entries are excluded and not migrated.
- Any authenticated active Shop session may create/edit/deactivate/reactivate customer records, Products, Services, product categories, sale prices and Fault catalogue entries without an employee challenge; these CAT-01 catalogue permissions are not restricted by employee role. Product categories are Company-wide and therefore shared by every Shop. Every Product must have exactly one category, while Services must not have a product category. Customer reactivation restores the same Customer record and preserves its ID, contact details, history and existing business references; it does not create a replacement record. Reactivation is blocked if it would violate a Company-scoped uniqueness invariant. Payment refunds and deposit refunds are permitted to `MANAGER` and `ADMIN`; deposit forfeiture remains `MANAGER`-only. All require the fresh direct employee credential validation defined by FIN-01.
- V1 uses soft deletion. Customer deactivation is allowed even when the Customer has existing Sales, ServiceOrders or other historical references; those relationships do not block deactivation. A deactivation reason/observation is optional; when supplied, store it as an audit detail associated with the deactivation. Deactivation only makes the Customer unavailable for new operations and must not cancel, alter or detach existing records. Historical sale/service-order records must retain the customer identity reference and an immutable snapshot of the customer details used when the transaction/order was committed, including the customer's name and contact details available at that time. Later Customer edits must not rewrite these historical snapshots. Historical sale/service-order lines must retain stable snapshots/references as defined in their feature specifications.
- All queries and mutation targets must be scoped to the authenticated Company. Shop-specific inventory is scoped to the active Shop.

## 5. Approved target design

- Customer directory with Company-wide search, details, create/edit and deactivate actions.
- Product and Service catalogue screens share search and lifecycle controls while showing type-specific fields. Product forms manage category and stock-tracking configuration; physical quantities and reservations are managed per Shop in inventory workflows. No minimum-stock setting is provided.
- Fault catalogue is a Company-shared list for service-order selection. Service-order technical findings remain a separate operational record referencing a catalogue fault when applicable.
- Normalize phone input for matching while preserving a formatted display value. Do not merge customer identities automatically.
- Store immutable customer-detail snapshots (name and contact details) on committed Sale/ServiceOrder records, alongside the stable Customer reference, so later Customer edits do not rewrite historical records. Store immutable descriptive and price snapshots on committed Sale/ServiceOrder lines so later catalogue edits, including sale-price changes, do not rewrite history.

Inactive records remain visible on existing records and history, but cannot be selected in new operations. Committed order-line snapshots and references remain unchanged. Product categories may be deactivated even when Products reference them; deactivation preserves the category record and all existing Product references. A category referenced by any Product must never be permanently deleted. Category lifecycle is soft deactivation/reactivation only.

## 6. Business rules and invariants

### Confirmed

- Company owns shared customers and catalogue entries; a Shop is not the owner of a customer/catalogue row.
- Product stock is per Shop and not stored as a shared catalogue quantity.
- Customer and catalogue management is available to an authenticated active Shop session without an individual employee credential challenge; there is no role-based employee distinction within CAT-01.
- Soft deletion is the v1 baseline. Product categories may be deactivated even when Products reference them; this preserves the category and all existing Product references. A category referenced by one or more Products must never be permanently deleted; only soft deactivation/reactivation is supported.

### Resolved business rules

1. Customer phone defaults to Irish input. The “International number” checkbox switches the field to international input. Store and compare as E.164. A matching Company phone blocks both Customer creation and phone changes during updates, including when the other matching record is inactive. During an update, exclude the Customer being edited from the duplicate check so an unchanged phone is accepted. No automatic merge.
2. An authenticated active Shop session can read the Company customer directory and create/edit customer records; no individual employee identity or role is required for CAT-01.
3. Customer email is optional and may repeat; no email unique constraint.
4. Remove `OTHER` from the target and do not migrate legacy `OTHER` entries.
6. No minimum-stock field or low-stock threshold exists. `quantity_on_hand` represents physical stock; `quantity_reserved` represents active reservations; available quantity is `quantity_on_hand - quantity_reserved`.
7. Fault descriptions are the primary display/search/selection values. Generate immutable Company-unique `FLT-` sequence codes, padded to at least six digits, with no reuse.
8. Any authenticated active Shop session may manage Products, Services, product categories and prices and create/edit Fault catalogue entries without an employee challenge; no Manager role is required. Product categories are shared across all Shops in the Company. Each Product has exactly one required category; Services have no product category. Categories may be deactivated even when Products reference them, preserving all existing references. A referenced category must never be permanently deleted; only soft deactivation/reactivation is supported. A Product may be reassigned to another active category in the same Company; reassignment changes only the current catalogue association and does not alter committed Sale or ServiceOrder history, including historical line snapshots and references. Payment refunds and deposit refunds are authorized to `MANAGER` and `ADMIN` under FIN-01, with fresh direct employee credential validation; deposit forfeiture remains `MANAGER`-only.
9. Inactive records remain visible on existing records/history and cannot be selected for new operations. Existing drafts may display already-linked inactive records but cannot add another reference to them. An authenticated active Shop session may reactivate records without an employee challenge. Customer reactivation restores the same Customer record, preserving its ID, phone, email, history and existing references; it never creates a new Customer. Block reactivation if a Company-scoped uniqueness invariant would be violated.
10. Editing a Customer updates only the current Company customer-directory record. It must not rewrite customer-detail snapshots or other historical data already committed to Sales or ServiceOrders. New committed operations capture the latest Customer details in their own immutable snapshot while retaining the stable Customer reference.
11. Customer deactivation must not be blocked by existing Sales, ServiceOrders or historical references. It only prevents the Customer from being selected for new operations; existing records, their Customer references and immutable snapshots remain intact and readable. Deactivation does not cancel, modify or detach any existing business record. A deactivation reason/observation is optional and, when provided, is retained as an audit detail.

### Deferred details

- Phone output formatting and exact English invalid-number/duplicate messages are UI details; canonical storage and matching use E.164.
- Exact customer retention/purge schedule remains deferred in `product-owner-decisions.md`; v1 uses soft deletion without automated purge.

## 7. Data model and relationships

The current logical draft in `specs/02-architecture/database-model.md` contains:

- `customer(company_id, person_id, phone_e164, notes, active)` linked to Company and Person. `phone_e164` is required and unique per Company, including soft-deleted records. Optional email may repeat.
- `catalog_item(company_id, kind, name, list_price, active)`; `kind` is PRODUCT or SERVICE. No SKU column or `OTHER` kind is defined.
- `product(catalog_item_id, product_category_id, inventory_tracking_mode, ...)` for Product-only fields. `product_category_id` is required (NOT NULL), so every Product belongs to exactly one Company-scoped category. Services have no category relationship.
- `service(catalog_item_id, description)` for Service-only fields.
- `product_category(company_id, name, normalized_name, code, description, is_active)`. Categories are flat: there is no parent category, child category or hierarchy. Category names are unique per Company using a normalized comparison that trims leading/trailing whitespace and ignores letter case; the uniqueness rule also applies to inactive categories.
- `shop_inventory(shop_id, product_catalog_item_id, quantity_on_hand, quantity_reserved)` for Shop stock. `quantity_on_hand` is the physical quantity and may be negative; `quantity_reserved` is non-negative and represents active reservations. No minimum-stock field is defined.
- `fault(company_id, code, description, active)`; code is automatically generated as `FLT-` plus a Company sequence padded to at least six digits, immutable and never reused. Description is the primary display/search/selection value.

This is a logical draft. Enforce Company and customer-phone uniqueness, Company-scoped uniqueness for normalized product-category names (including inactive categories), and a transactional Company-scoped Fault code sequence. No SKU uniqueness constraint or index is required. Exact physical indexes, audit fields and foreign-key delete constraints are implementation details to review with the migration.

## 8. API contracts

All endpoints are under `/api/v1`, require the authenticated and active Shop session, derive `companyId` and `shopId` from that session, and use DTOs rather than EF entities. Search/list reads and all Customer/Product/Service/category/Fault writes require no individual employee credential challenge. Mutations remain Company-scoped and audit the ShopSession that performed them.

| Resource | Contract | Authorization / behavior |
|---|---|---|
| Customers | `POST /customers/queries` with `{ search?, includeInactive?, page, pageSize }`; `POST /customers`; `GET /customers/{id}`; `PUT /customers/{id}`; `POST /customers/{id}/deactivate`; `POST /customers/{id}/reactivate` | Active Shop session only; no employee challenge. Reactivates the same Customer row/identity and preserves its existing relationships and history; returns a conflict if a Company-scoped uniqueness invariant prevents reactivation. Input: firstName, lastName, phone, isInternational, email?. API canonicalizes phone to E.164 and returns 409 `duplicate_customer_phone` on collision for both create and update. On update, the record being edited is excluded from the uniqueness check; any other Company Customer with that phone, active or inactive, causes the conflict. DTO exposes `phoneE164`; no cross-Company data is returned. |
| Catalogue items | `POST /catalog-items/queries`; `POST /catalog-items`; `GET /catalog-items/{id}`; `PUT /catalog-items/{id}`; `POST /catalog-items/{id}/deactivate`; `POST /catalog-items/{id}/reactivate` | Active Shop session only; no employee challenge. DTO has kind PRODUCT/SERVICE, name, decimal listPrice, isActive and subtype fields. |
| Product categories | `POST /product-categories/queries`; `POST /product-categories`; `PUT /product-categories/{id}`; `POST /product-categories/{id}/deactivate`; `POST /product-categories/{id}/reactivate` | Any authenticated active Shop session; no Manager role or employee challenge. Categories are Company-wide. DTO contains name, code, description and isActive; categories have no parent/child relationship or hierarchy. API trims leading/trailing whitespace and rejects a duplicate name within the Company, ignoring letter case, including collisions with inactive categories; returns 409 `duplicate_product_category_name`. |
| Faults | `POST /faults/queries`; `POST /faults`; `GET /faults/{id}`; `PUT /faults/{id}`; `POST /faults/{id}/deactivate`; `POST /faults/{id}/reactivate` | Active Shop session only; no employee challenge. Create/update accepts description only; server generates and returns immutable code. Description is the primary search/selection field. |

Query DTOs return paged results and include inactive records only when `includeInactive=true`; inactive items remain available to history readers but are omitted from new-operation selectors. Return 404 for IDs outside the active Company. Validation errors use the API's Problem Details format. Per-Shop physical quantity and reservations belong to ShopInventory/inventory APIs, not the shared catalogue-item endpoint; there is no minimum-stock field or low-stock threshold.

## 9. UI flows and states

### Customer Directory

- Search Company-wide by name, entered/canonical phone and email. Results show name, phone and email; inactive records are hidden by default and available through an inactive filter.
- Create/edit requires first name, last name and phone; email is optional and may repeat. These operations use the authenticated Shop session and do not request employee credentials.
- Phone input defaults to Ireland. “International number” checkbox switches the phone field to international input. Validate and canonicalize to E.164 on create and edit; duplicate phone blocks save with an English error, including collisions with inactive customers. Editing a customer's other fields or retaining its unchanged phone must not trigger a false duplicate conflict.
- Create, edit and deactivate require the active Shop session and do not invoke an employee credential challenge. Deactivation remains available when the Customer has existing Sales or ServiceOrders; it does not modify or cancel them, and the Customer remains visible through those historical/existing records. The deactivation form may include an optional free-text reason/observation; leaving it blank does not block the operation.
- Reactivation uses the same Customer record and preserves its ID, phone, email, history and existing sale/service-order references. It makes the customer available for new operations again. If a Company-scoped uniqueness invariant would be violated, block reactivation and return an actionable English conflict; do not create a replacement customer.
- Editing a Customer changes only the current directory record. Committed Sales and ServiceOrders retain the customer name and contact details captured at their own commit time, as well as the stable Customer reference; later edits never rewrite those historical snapshots. New committed operations capture the latest customer details.

### Product/Service Catalogue and Categories

- One catalogue supports Product and Service types only. `OTHER` is not offered.
- Product fields include name, sale price and one required category. Category selection is mandatory when creating or editing a Product; the selected category must belong to the authenticated Company and be active for new assignments. An existing Product may be reassigned to another active category in the same Company. Reassignment changes only the current catalogue association and must not rewrite or otherwise alter committed Sale or ServiceOrder history, including existing line snapshots and references. Service fields include name, sale price and description; Services do not display, require or store a product category. Sale price is required, accepts zero, rejects negative values, and uses decimal/`NUMERIC(19,2)`. Product physical stock and reservations belong to Shop inventory workflows; no minimum-stock setting is available. Any authenticated active Shop session can create, edit, deactivate and reactivate Company-wide categories without a Manager role or employee challenge.
- Categories are a flat Company-wide list with no subcategories or hierarchy. Category names are unique within the Company after trimming leading/trailing whitespace and comparing without case sensitivity; the same normalized name is not allowed even when the existing category is inactive. Shops share the same category namespace. Inactive items are available in history views but cannot be selected in new operations. An authenticated Shop session can reactivate an item; existing drafts may display a previously selected inactive item but cannot add another line/reference to it. A Product may be reassigned to another active category in the same Company; this updates only the current catalogue association and never rewrites committed Sale or ServiceOrder history.

### Fault Catalogue

- Create/edit uses description as the primary field. Generated code is displayed read-only (example `FLT-000001`) and is not the primary search/selection value.
- Fault search uses description; inactive faults cannot be added to new ServiceOrders.

Every screen includes loading, empty, validation, API error and unauthorized states. User-facing text is English. Customer and catalogue writes do not open the employee credential challenge; the API still requires a valid Shop session.

## 10. Authorization

- Authenticated Shop session supplies the Shop and Company context; it does not identify an employee.
- Customer, Product, Service, product-category, price and Fault mutations require an active Shop session and Company scope; they do not require a Manager role or employee challenge. Any authenticated active Shop session may manage Company-wide product categories.
- These mutations are audited against the authenticated ShopSession. All reads and writes enforce Company scope.
- Employee challenge rules apply only to actions whose feature SPEC requires them. Payment refunds and deposit refunds are permitted to `MANAGER` and `ADMIN`; deposit forfeiture remains `MANAGER`-only. All require the fresh direct employee credential validation defined by FIN-01.

## 11. Acceptance criteria

Acceptance criteria:

- No cross-Company customer/catalogue/fault reads or mutations.
- Customer phone defaults to Ireland; international checkbox accepts international input; both normalize to E.164.
- A matching E.164 customer phone in the Company, including an inactive record, returns a hard conflict on create or phone update and creates no duplicate. Updating a customer while retaining its own existing phone succeeds; the edited record is excluded from its duplicate check.
- Repeated customer email is accepted.
- Product category creation/update rejects a name that duplicates another category in the same Company after trimming leading/trailing whitespace and ignoring letter case; inactive categories are included in this check. The same category name may not be duplicated by another Shop because the catalogue is Company-wide.
- Product and Service sale prices are required, non-negative and stored as `NUMERIC(19,2)`; zero is valid and negative values are rejected. Every Product requires exactly one category belonging to the Company; Services have no product category. Product category names are unique per Company after trimming leading/trailing whitespace and ignoring letter case, including inactive categories. Any authenticated active Shop session can manage categories without a Manager role or employee challenge. Categories can be deactivated even when referenced by Products, preserving all references; referenced categories cannot be permanently deleted. Products can be reassigned to another active Company category without modifying committed Sale or ServiceOrder history or line snapshots. The target catalogue has no SKU field.
- New Faults receive immutable, unique `FLT-` sequence codes; description remains primary in search/selection.
- `OTHER` is neither offered nor imported into the target catalogue.
- `ShopInventory` stores physical on-hand quantity and reserved quantity only; no minimum-stock threshold or low-stock comparison is implemented.
- Inactive customer/catalogue records cannot be used in new operations but remain readable from existing history. Customer deactivation succeeds even when Sales or ServiceOrders reference the Customer; those existing records remain unchanged and readable. A deactivation reason/observation is optional; deactivation succeeds when it is omitted.
- Editing a Customer does not change customer-detail snapshots on previously committed Sales or ServiceOrders; new committed operations capture the latest customer details and retain the stable Customer reference.
- An authenticated active Shop session can reactivate an inactive record without an employee challenge. Customer reactivation restores the same record and preserves its ID, contact details, history and existing references; it does not create a replacement. Block reactivation when a Company-scoped uniqueness invariant would be violated. An existing draft can display its linked inactive item but cannot add another reference to it.
- Customer and catalogue CRUD works without an employee challenge but is restricted to the authenticated Shop's Company and is audited against the ShopSession; payment refunds and deposit refunds require `MANAGER` or `ADMIN` credentials under FIN-01; deposit forfeiture remains `MANAGER`-only.
- A Shop cannot assign inventory to a Service or to a Product belonging to another Company.
- Deactivating a product category does not delete it or alter existing Product references. Permanent deletion of a category referenced by one or more Products is prohibited; category lifecycle uses soft deactivation/reactivation.
- Product categories have no parent/child relationship or hierarchy; they are a single flat list. Product category names must be unique within a Company, regardless of active status. Compare names after trimming leading/trailing whitespace and ignoring letter case. Categories with the same normalized name cannot coexist, even if they belong to different Shops, because categories are Company-wide.
- A Product can be reassigned to another active category in the same Company. Reassignment updates only the current Product-to-category association; committed Sales and ServiceOrders, including their historical line snapshots and references, remain unchanged.
- Money uses C# `decimal` and PostgreSQL `NUMERIC(19,2)`, never binary floating point. Product and Service sale prices are required and cannot be negative; zero is allowed.
- Inactive records do not disappear from historical committed records.
- Mutations are audited with the ShopSession actor; no employee identity or one-use employee authorization is required for CAT-01 writes.
- User-facing screens and validation messages are in English.

## 12. Test scenarios

Expected coverage includes Company-scope isolation; Irish/international phone parsing and duplicate blocking on create and update, including unchanged-phone edits and collisions with inactive records; repeated email acceptance; flat product-category structure with no parent/child relationships; product-category name uniqueness after trimming and case-insensitive comparison, including collisions with inactive categories and concurrent create/update attempts; concurrent Fault code generation; Product-vs-Service constraints; per-Shop physical stock and reservation invariants with no minimum-stock field; inactive-item handling in new vs existing records, including Customer deactivation despite existing Sales/ServiceOrders, preservation of existing references, same-record Customer reactivation and uniqueness-conflict blocking; immutable customer-detail snapshots on committed Sales/ServiceOrders; ShopSession-only write authorization; and ShopSession audit attribution. Add tests after this SPEC is approved.

## 13. Approval

**Approved 2026-11-13; Shop-session-only authorization for Customer/Catalogue writes confirmed 2026-11-14; Product category cardinality and category-management permissions confirmed 2026-11-22.** The exact customer retention/purge schedule remains separately deferred. Related documents: [`customer-catalog-feature-audit.md`](../01-discovery/customer-catalog-feature-audit.md), `specs/02-architecture/product-owner-decisions.md`, `domain-model.md`, `database-model.md`, and `employee-access-and-roles.md`.
