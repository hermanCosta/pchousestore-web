# Proposed Relational Database Model

**Status:** DRAFT / logical baseline — not an approved production DDL. PostgreSQL was selected by the product owner on 2026-11-11 and supersedes the earlier MySQL target; provider-specific physical review remains part of DATA-05. DATA-02, DATA-03, DATA-04 and DATA-06 logical decisions are approved. Remaining business/regulatory reviews are still pending.
**Decisions incorporated:** one Company with multiple Shops; Shop session; company-wide employee password credentials and product-owner-approved MANAGER / TECHNICIAN role matrix (employees may hold both; effective permissions are the union and MANAGER grants full access); per-operation employee authorization; product-only integer stock with negative balances permitted; reservations, selected returns and inter-shop transfers; serialized refurbished units with typed specifications, A/B/C condition grades, inspection records, 12-month commercial guarantee, complaint cases and image uploads; payment transaction + tender allocations; service deposits; weekly refurbished-sale instalment schedules; partial/total refunds; Ireland EUR/cash-rounding baseline with a company-level VAT feature flag defaulting OFF (the application adds no VAT amount while off; legal status remains to be reviewed); one OPEN/CLOSED DailyClosing per Shop/business date with opening float, expected/count cash, denomination count and immutable correction records; supplier procurement with a dedicated service-parts request queue, reference images, ETA tracking, partial receipts and no purchase-approval workflow. DRAFT pending remaining business and regulatory review.

### DATA-02 approved logical decisions

- Company owns Shops; `shop.code` is the stable sign-in identifier and is unique within its Company. `shop_access_credential` has one row per Shop, keyed by `shop_id`, and stores only the password hash and credential lifecycle metadata. Do not duplicate the Shop code in the credential row. The stable Shop identity (`shop.id`) is the OIDC subject; credential rotation does not change it.
- A Shop has zero or more historical `shop_session` rows. `shop_session.id` is the session identifier (`shop_session_id` / `sid`); persist no raw or hashed bearer/session token. A session has no employee identity. Idle/absolute expiry values and concurrent-device policy remain configurable/deferred; do not enforce a single active session per Shop.
- `employee_action_authorization` is scoped to exactly one ShopSession, Shop, Company employee, action and short validity window. Its optional operation-context fingerprint is stored only where a feature defines a canonical representation. The authorization is consumed with a conditional atomic update and the protected business write in one transaction; keep consumed records for audit.
- Enforce tenant boundaries with composite candidate keys and FKs: a ShopSession must belong to the authorization's Shop; authorization Shop and Employee must belong to the same Company; Shop-scoped child references must point to a parent in the same Shop. Where practical, carry the scope key in the FK instead of relying on application-only checks.
- Employee role assignments are Company-wide. The Product Owner approved multiple role assignments and union semantics; `MANAGER` grants the full capability set. Fine-grained implementation follows `employee-access-and-roles.md`.
- OpenIddict owns its persistence schema and migrations. Application tables may retain provider authorization identifiers for correlation/revocation, but do not depend on undocumented provider table structure.

## 1. Physical design standards

- DBMS: PostgreSQL 16 or later, using UTF-8 encoding.
- Primary keys: signed `BIGINT` paired with C# `long`; every FK uses the exact same SQL type as its referenced PK.
- Monetary values: C# `decimal` and PostgreSQL `NUMERIC(19,2)` for EUR transaction amounts. Rates/percentages use a separate higher-precision numeric type (for example `NUMERIC(9,6)`). The transaction currency is stored as ISO 4217 `CHAR(3)`; initial default is `EUR`.
- **Stock quantities:** no fractional product stock. `quantity_on_hand` and inventory movement deltas use signed integer types (`INT`/`BIGINT`) because negative quantity-tracked balances are allowed. Reservation, requested, ordered, received and returned quantities use non-negative integer types (`INT with a non-negative CHECK constraint`/`BIGINT with a non-negative CHECK constraint`) with positive-value and reconciliation constraints.
- Product quantities in SaleItem/ServiceOrderItem must be whole numbers. Service quantities may be treated separately if a future service-pricing requirement needs fractional units; service quantities never affect stock.
- Timestamps: PostgreSQL `TIMESTAMPTZ` stored in UTC; business-date calculations use Shop time zone. Initial Irish default: `Europe/Dublin`.
- Avoid `DOUBLE` for money.
- One Company is provisioned for the current deployment. Shared master data is company-level; operational records carry `shop_id`.
- Prefer `RESTRICT` for deleting financial/history records; use soft deactivation for master data referenced by history.
- No polymorphic source references for core transaction ownership. Use explicit nullable FKs where the source type is known.

## 2. Proposed tables

Field names are canonical English snake_case. The final DDL must make FK types, uniqueness and Shop consistency constraints explicit. Operational tables derive `shop_id` from the validated ShopSession, not from an independently trusted browser value.

### Company, Shops and authentication context

| Table | Key columns / purpose | Important constraints |
|---|---|---|
| `company` | `id`, `name`, `address`, `contact_one`, optional `contact_two`, optional `email`, `country_code` (`IE`), `default_currency_code` (`EUR`), `time_zone` (`Europe/Dublin`), `is_active` | Legacy Company requires name/address/contact_one; email is optional. Do not enforce email uniqueness until resolved because the legacy JPA annotation and inspected DDL disagree. Company legal registration fields remain OPEN; country/currency/time-zone are configuration, not calculation constants. |
| `shop` | `id`, `company_id`, `code`, `name`, `address` (optional, max 500 chars), `phone` (optional, max 40 chars), `time_zone`, `is_active` | FK to Company; unique `(company_id,code)`. Address/phone are branch-specific display values for print output; snapshot when a document is issued. Populate only from owner-confirmed details. |
| `shop_access_credential` | `shop_id` (PK/FK), `password_hash`, `is_active`, rotation metadata | Exactly one credential record per Shop; login identifier is `shop.code`, not duplicated here; secure password hash only |
| `shop_session` | `id` (`shop_session_id` / `sid`), `shop_id`, `oidc_authorization_id?`, `issued_at`, `last_seen_at?`, `idle_expires_at?`, `absolute_expires_at?`, `revoked_at?`, optional workstation metadata | Shop context only; no `employee_id` column or employee identity claim. Persist no raw or hashed bearer/session token. Session values and concurrent-device policy remain configurable/deferred; no one-active-session constraint |
| `shop_financial_policy` | `id`, `shop_id`, `currency_code` (`EUR`), `cash_rounding_enabled`, `cash_rounding_increment` (`0.05`), `deposit_review_after_days` (90), other policy flags | One row per Shop; cash rounding optional and only applied to eligible cash bill totals. Refunds and deposit forfeitures are Manager-only, independent of amount; no second Manager approval. Procurement purchase approval is not used. |
| `company_tax_configuration` | `id`, `company_id` (unique), `vat_enabled` (boolean, default false), `setup_status` (`NOT_REVIEWED`/`READY`), `vat_registration_number?`, `prices_include_vat?`, `approved_at?`, `approved_by_employee_id?`, `notes` | Company-wide VAT feature toggle; initial state is `vat_enabled=false`, `setup_status=NOT_REVIEWED`, so the application adds no VAT amount for now. This is a software instruction, not a legal determination that VAT is not due. Production tax-document finalization remains gated on an Irish tax review. |
| `company_tax_code` | `id`, `company_id`, `code`, `description`, `treatment` (`STANDARD`/`REDUCED`/`ZERO`/`EXEMPT`/`OUT_OF_SCOPE`/`REVERSE_CHARGE`), `is_active` | Configurable company tax classification; no tax code or rate is preselected as a legal conclusion. |
| `company_tax_rate` | `id`, `company_tax_code_id`, `rate DECIMAL(9,6)`, `effective_from`, `effective_to?` | Effective-dated rate history; overlap for the same code is rejected; no rate is hard-coded in application code. |
| `payment_method` | `id`, `code`, `display_name`, `method_family`, `requires_reference`, `affects_cash_drawer`, `is_refundable`, `is_active` | Seed only `CASH` and `CARD` as active in v1. Other tender methods are out of initial scope. |
| `shop_payment_method` | `id`, `shop_id`, `payment_method_id`, optional `external_terminal_label`, `is_enabled`, display_order | Unique `(shop_id,payment_method_id)`. No processor/API integration or secrets are stored in v1; card payments are confirmed on an external terminal and recorded manually. |

### OAuth 2.0 / OpenID Connect persistence

| Persistence | Purpose | Important constraints |
|---|---|---|
| OpenIddict EF Core tables (`OpenIddictApplications`, `OpenIddictAuthorizations`, `OpenIddictScopes`, `OpenIddictTokens`, names configurable) | Registered React client, `shop.api` scope/resource, authorization codes, Shop authorization grants and revocation metadata | Managed by the OpenIddict store/migrations; refresh tokens are disabled per ADR-034; never store raw secrets outside provider-protected token persistence; keep token records tied to Shop authorization/ShopSession. |
| `shop_access_credential` | Interactive Shop login credential and password hash | Used at the OIDC authorization endpoint; not used as a Resource Owner Password Credentials grant. |

Physical definitions for `shop_access_credential`, `shop_session`, `employee_action_authorization` and the requested `system_services` signing-key registry are in [Authentication and Signing-Key Persistence](authentication-persistence.md), with a non-executable design script at [`authentication-foundation.draft.sql`](../../infrastructure/database/authentication-foundation.draft.sql). They are a DATA-05 draft and must be reviewed before migration generation.

The Shop access JWT carries `sub`, `client_id`, `iss`, `aud`, `iat`, `nbf`, `exp`, `jti`, `scope`, `company_id`, `shop_id` and `shop_session_id`. It carries no `employee_id`, employee code or employee role. The API must validate the active ShopSession referenced by the token.

### Employees and transaction-level authorization

| Table | Key columns / purpose | Important constraints |
|---|---|---|
| `person` | `id`, `company_id`, `first_name`, `last_name`, `contact_no`, optional `email` | Shared company-level person record. Email uniqueness is OPEN and is not enforced. |
| `employee` | `id`, `company_id`, optional `person_id`, `employee_code`, `password_hash`, `is_active` | One Company-wide identity and password per employee; unique employee code within Company; same credentials work in every Shop belonging to the Company; password hash only. Optional Person link follows the legacy mapping (`@ManyToOne` without `optional=false`); status vocabulary beyond active/inactive remains OPEN. Per-operation challenge resolves employee by identifier plus password, not password-only lookup. |
| `employee_role_assignment` | `id`, `employee_id`, `role_code` (`MANAGER`/`TECHNICIAN`), `is_active`, assigned_at, assigned_by_employee_id? | Company-wide role assignment, not Shop-specific; unique `(employee_id,role_code)`. Multiple roles are allowed; effective permissions are the union and MANAGER grants full access. |
| `employee_action_authorization` | `id`, `shop_session_id`, `shop_id`, `company_id`, `employee_id`, `action_code`, `operation_context_hash?`, `authenticated_at`, `expires_at`, `consumed_at` | Composite FKs enforce same ShopSession/Shop and same Company for Shop and Employee. Short-lived, action-scoped, single-use; optional canonical operation fingerprint; consumed atomically with the protected write. No ShopEmployee membership is required for cross-Shop access. |

### PostgreSQL image binary storage

| Table | Key columns / purpose | Important constraints |
|---|---|---|
| `stored_image` | `id`, `company_id`, `shop_id`, `content BYTEA`, `thumbnail_content BYTEA?`, `content_type`, `size_bytes`, `width_px`, `height_px`, `sha256`, `uploaded_by_employee_id`, `authorization_id`, `uploaded_at`, `deleted_at?`, `deleted_by_employee_id?`, `delete_authorization_id?` | Binary bytes are stored in PostgreSQL only. Full normalized image <=10 MiB; thumbnail <=60 KiB. Company/Shop must match the attachment owner. No object-storage key or filesystem path. |

The entity-specific image relations below store the parent FK and `stored_image_id` FK; `stored_image` contains the bytes and common technical/audit metadata. Use no polymorphic owner id. See [Image Upload and PostgreSQL Storage Policy](image-upload-policy.md).

### Shared people, catalogue and category-specific specifications

| Table | Key columns / purpose | Important constraints |
|---|---|---|
| `customer` | `id`, `company_id`, `person_id`, notes, active | Company-level customer shared across Shops by default |
| `catalog_item` | `id`, `company_id`, `kind` (`PRODUCT`/`SERVICE`), name, SKU, list_price, active | Shared Company catalogue; unique `(company_id,sku)` when SKU present |
| `product` | `catalog_item_id` PK/FK, `product_category_id`, `inventory_tracking_mode` (`QUANTITY`/`SERIALIZED_UNIT`), other product-level attributes | Subtype only for kind PRODUCT; refurbished serialized products use SERIALIZED_UNIT |
| `service` | `catalog_item_id` PK/FK, description | Subtype only for kind SERVICE; no stock fields |
| `product_category` | `id`, `company_id`, nullable `parent_category_id`, `name`, `code`, `description`, `is_active` | Flexible hierarchy; unique `(company_id,code)`; examples PC, Notebook, Console, TV; not an enum |
| `product_attribute_definition` | `id`, `product_category_id`, `name`, `code`, `data_type` (`TEXT`/`NUMBER`/`BOOLEAN`/`DATE`/`OPTION`), `unit`, `is_required`, `allowed_values`, validation metadata, display_order | Unique `(product_category_id,code)`; defines allowed technical fields for a category |
| `refurbished_unit` | `id`, `company_id`, `product_catalog_item_id`, `current_shop_id` nullable, `asset_code`, nullable `manufacturer_name_normalized`, nullable `serial_number_normalized`, brand/model override, `condition_grade_id`, `functional_test_status`, `status`, description/known_cosmetic_defects, acquisition_date/cost, asking_price | One row per physical unit; product must use SERIALIZED_UNIT; Company-unique required `asset_code`; capture manufacturer serial when present; unique `(company_id,manufacturer_name_normalized,serial_number_normalized)` when both serial components are present; only inspection PASSED can become AVAILABLE; `current_shop_id` null when sold/in transit |
| `refurbished_unit_image` | `id`, `refurbished_unit_id`, `stored_image_id`, `image_role`, caption, sort_order | Multiple PostgreSQL-backed images per physical unit; `stored_image_id` FK, unique within this attachment table; access and soft-delete are authorized/audited. |
| `stored_image` | Shared table described above | Full binary content and optional <=60 KiB thumbnail use `BYTEA`. |

| `refurbished_unit_warranty` | `id`, `refurbished_unit_id`, `sale_item_id` (unique), `shop_id`, `warranty_type` (`COMMERCIAL`), `duration_months` (12), `starts_at`, `expires_at`, `terms_snapshot`, `status` | One commercial guarantee record per sold refurbished unit; starts at documented customer handover, not at reservation or deposit; snapshot is immutable. Does not replace Irish statutory consumer rights. |
| `refurbished_condition_grade` | `id`, `company_id`, `code` (`A`/`B`/`C`), `display_name`, description, display_order, `is_active` | Company-configurable commercial cosmetic grade catalogue; initial seed A=Excellent, B=Very Good, C=Good; grade does not imply functional test result |
| `refurbished_unit_inspection` | `id`, `refurbished_unit_id`, `shop_id`, `condition_grade_id`, `functional_test_status` (`NOT_TESTED`/`IN_PROGRESS`/`PASSED`/`FAILED`), `checklist_json`, known_defects, notes, `inspected_by_employee_id`, authorization_id, inspected_at | Append-only inspection snapshot; only PASSED plus A/B/C may transition unit to AVAILABLE; FAILED remains QUARANTINED/repair |
| `customer_complaint_case` | `id`, `shop_id`, `customer_id`, nullable `sale_item_id`, nullable `service_order_id`, `claim_basis`, `reported_at`, `issue_first_seen_at?`, issue_summary, requested_remedy, status, assigned_employee_id?, opened_by_employee_id, authorization_id | Exactly one of SaleItem or ServiceOrder is the source; SaleItem can identify the RefurbishedUnit; claim basis distinguishes statutory rights, commercial guarantee, service workmanship, goodwill and paid repair; commercial warranty expiry does not auto-reject statutory claim |
| `customer_complaint_case_event` | `id`, `complaint_case_id`, event_type, from_status?, to_status?, note, customer_visible, employee_id, authorization_id, occurred_at | Append-only case status, diagnosis, communication, escalation and decision history |
| `customer_complaint_case_attachment` | `id`, `complaint_case_id`, `stored_image_id`, caption, image_role, sort_order | PostgreSQL-backed image evidence; direct FK to `stored_image`; soft-delete and audit; no external storage key. |
| `customer_complaint_resolution` | `id`, `complaint_case_id` (unique), resolution_type, reason, nullable `refund_payment_transaction_id`, nullable `replacement_refurbished_unit_id`, nullable `service_order_id`, resolved_by_employee_id, authorization_id, resolved_at | Manager-approved final remedy; possible repair/replacement/full refund/partial refund or price reduction/no-fault finding/customer withdrawal; financial refund and physical stock return remain separate |
| `refurbished_unit_attribute_value` | `id`, `refurbished_unit_id`, `product_attribute_definition_id`, typed value columns (`text_value`, `number_value`, `boolean_value`, `date_value`) | Unique `(refurbished_unit_id,product_attribute_definition_id)`; exactly one value column consistent with definition data_type |
| `device` | `id`, `company_id`, `customer_id`, brand, model, serial_number | Customer equipment received for service; separate from refurbished stock units |
| `fault` | `id`, `company_id`, code, description, active | Shared Company fault catalogue |

A `ProductCategory` defines the schema for technical details. For example, a PC category may define CPU, RAM, GPU and SSD fields; a console category may define generation, storage and included accessories; a TV category may define screen size, resolution and panel technology. The values for each refurbished physical device are stored in `refurbished_unit_attribute_value`, not as columns added for every new product type.

### Shop inventory, reservations and movements

| Table | Key columns / purpose | Important constraints |
|---|---|---|
| `shop_inventory` | `id`, `shop_id`, `product_catalog_item_id`, `quantity_on_hand INT` (signed), `quantity_reserved INT with a non-negative CHECK constraint`, `minimum_quantity INT with a non-negative CHECK constraint` | Unique `(shop_id,product_catalog_item_id)`; only for QUANTITY-tracked Product; negative on-hand is allowed; reservations stay non-negative; `available_quantity = quantity_on_hand - quantity_reserved` may be negative and is surfaced as a stock deficit |
| `inventory_reservation` | `id`, `shop_id`, nullable `sale_id`, nullable `service_order_id`, status (`ACTIVE`/`PARTIALLY_CONSUMED`/`CONSUMED`/`RELEASED`), created_by_employee_id, authorization_id, created_at, released_at, consumed_at | Exactly one source Sale/ServiceOrder; source and Shop must match; no automatic expiry until policy is approved |
| `inventory_reservation_line` | `id`, `reservation_id`, nullable `sale_item_id`, nullable `service_order_item_id`, nullable `shop_inventory_id`, nullable `refurbished_unit_id`, `quantity_reserved`, `quantity_consumed`, `quantity_released` | Exactly one source line and exactly one stock target; positive integer quantity; serialized unit line has quantity one; consumed + released <= reserved |
| `inventory_movement` | `id`, `shop_id`, `product_catalog_item_id`, nullable `shop_inventory_id`, nullable `refurbished_unit_id`, signed integer `quantity_delta`, movement_kind, employee_id, authorization_id, occurred_at, reason; optional source FKs to sale_item/service_order_item/stock_transfer_line/stock_return_line/supplier_purchase_line | Exactly one stock target; target belongs to Shop and Product; serialized-unit delta is ±1; movement and balance/unit-state change are atomic |

Reservation rules:

- `quantity_reserved` is a materialized counter maintained in the same transaction as reservation lines. It must equal the sum of active/unconsumed reservation quantities for that ShopInventory record.
- Reservation reduces availability, not on-hand. It creates no outbound `InventoryMovement`.
- Release decreases the reserved counter and records the reservation state/event; on-hand remains unchanged.
- Consume decreases both on-hand and reserved; an unreserved payment deduction decreases on-hand only after checking available quantity.
- A released reservation does not guarantee stock later. Payment rechecks live availability and may fail if another operation consumed the stock.
- `refurbished_unit` is reserved by its own status and is never represented as fractional or aggregate quantity.

### Inter-shop transfers

| Table | Key columns / purpose | Important constraints |
|---|---|---|
| `stock_transfer` | `id`, `source_shop_id`, `destination_shop_id`, status (`DRAFT`/`IN_TRANSIT`/`PARTIALLY_RECEIVED`/`RECEIVED`/`CANCELLED`), created_by_employee_id/auth, dispatched_by_employee_id/auth, received_by_employee_id/auth, created_at, dispatched_at, received_at, notes | Source != destination; both Shops belong to same Company; cancel only before dispatch |
| `stock_transfer_line` | `id`, `stock_transfer_id`, `product_catalog_item_id`, nullable `refurbished_unit_id`, `quantity_requested`, `quantity_dispatched`, `quantity_received` | Positive integer quantities; received <= dispatched <= requested; serialized unit requires qty 1 and exact unit id; one physical unit cannot be in two active transfers |
| `stock_transfer_discrepancy` | `id`, `stock_transfer_id`, nullable `stock_transfer_line_id`, discrepancy_type (`SHORT`/`DAMAGED`/`OVERAGE`/`OTHER`), expected_quantity, observed_quantity, reason, notes/evidence, employee_id, authorization_id, reported_at, status (`OPEN`/`RESOLVED_BY_RECEIPT`/`RESOLVED_BY_CORRECTIVE_TRANSFER`), resolved_at? | Append-only audit of count/condition mismatch; discrepancy quantities do not create destination inventory; excess units require a separate transfer/document; short/damaged dispatched units remain outstanding until a later accepted receipt or corrective transfer; no automatic loss write-off |

Transfer ledger behavior:

- Dispatch validates source available stock, creates outbound InventoryMovement rows and sets status IN_TRANSIT in one transaction.
- Receipt records actual quantities, creates inbound movements at destination and updates status to PARTIALLY_RECEIVED or RECEIVED.
- Dispatched but not yet received quantity is in transit and is not counted in either Shop's available/on-hand stock.
- Accept only the counted, acceptable quantity, never more than dispatched. Short/damaged quantities stay outstanding and keep the transfer PARTIALLY_RECEIVED; overage is recorded but never added to stock under that transfer. An unresolvable loss stays open; no automatic write-off or transfer closure.
- Cancellation before dispatch has no stock effect. After dispatch use a reverse/corrective transfer, not a status-only cancellation.
- Source dispatch authorization must belong to source Shop; receiving authorization must belong to destination Shop.

### Returns from cancelled/refunded sales and service orders

| Table | Key columns / purpose | Important constraints |
|---|---|---|
| `stock_return` | `id`, `shop_id`, nullable `sale_id`, nullable `service_order_id`, optional `refund_payment_transaction_id`, `employee_id`, `employee_action_authorization_id`, status (`RECEIVED`/`PARTIALLY_DISPOSED`/`DISPOSED`), returned_at, notes | Exactly one Sale/ServiceOrder target; target status must be REFUNDED or CANCELLED; same Shop; separate from monetary refund |
| `stock_return_line` | `id`, `stock_return_id`, nullable `sale_item_id`, nullable `service_order_item_id`, nullable `refurbished_unit_id`, `quantity_returned`, `disposition` (`PENDING_INSPECTION`/`RESTOCK`/`QUARANTINE`/`DISPOSE`), inspection/condition metadata, disposition_employee_id?, disposition_authorization_id?, disposed_at? | Exactly one source line; positive integer; cannot exceed consumed quantity minus prior returns; serialized item must match original unit; initially pending inspection; only RESTOCK creates an inbound movement and increases sellable stock; a quantity line may be split to record partial dispositions |

Only explicitly selected `StockReturnLine` quantities are physically received. Approved disposition workflow: receive returned goods as `PENDING_INSPECTION`, excluded from sellable `ShopInventory`; an authorized inspection records `RESTOCK`, `QUARANTINE` or `DISPOSE`; only `RESTOCK` creates an inbound movement. A serialized returned unit starts in `QUARANTINED` and becomes `AVAILABLE` only after a passing inspection. Split quantity lines when a return has mixed dispositions. A refund or order status change never restocks automatically. For a cancellation before payment/consumption, release active reservations instead; do not create a stock return.

### Sales and service orders

| Table | Key columns / purpose | Important constraints |
|---|---|---|
| `sale` | `id`, `shop_id`, nullable `customer_id`, `seller_employee_id`, `employee_action_authorization_id`, sale type/status, subtotal, discount, total, `balance_due`, created_at, settled_at | Exactly one Shop; actor/auth match; non-negative totals |
| `sale_item` | `id`, `sale_id`, `catalog_item_id`, nullable `refurbished_unit_id`, description snapshot, quantity, unit price, discount, taxable_amount, tax_code/rate snapshot, tax_amount, line total | Product quantity must be a whole number; serialized refurbished line quantity is one and references exact unit; immutable price/description snapshot |
| `service_order` | `id`, immutable `origin_shop_id`, current `shop_id`, `customer_id`, nullable `device_id`, `responsible_employee_id`, `created_by_employee_id`, `employee_action_authorization_id`, nullable `refurbished_unit_warranty_id`, status (includes `AWAITING_PARTS`), currency_code, subtotal, discount, tax_total, total, balance_due, lifecycle timestamps, bad_sector | Initial/current Shop ownership is explicit; on approved transfer, current `shop_id` changes to destination and origin is preserved; state transitions are recorded in ServiceOrderEvent; actor must belong to the Company owning the current Shop |
| `service_order_item` | `id`, `service_order_id`, `catalog_item_id`, description snapshot, quantity, unit price, discount, taxable_amount, tax_code/rate snapshot, tax_amount, line total | Product quantity whole-number; Service entries never create reservation/movement; immutable price and tax snapshots; VAT behavior follows CompanyTaxConfiguration |
| `service_order_fault` | `id`, `service_order_id`, `fault_id` | Unique `(service_order_id,fault_id)` |
| `service_order_event` | `id`, `service_order_id`, `shop_id`, `employee_id`, authorization_id, event type, from/to status, description, occurred_at | Records each committed transition/action |
| `service_order_transfer` | `id`, `service_order_id`, `source_shop_id`, `destination_shop_id`, `open_balance_transferred`, `held_deposit_liability_transferred`, `transferred_by_employee_id`, `employee_action_authorization_id`, `transferred_at`, reason/notes | Append-only; distinct Shops in same Company; atomically reassigns current Shop and transfers outstanding balance/deposit liability; no cash receipt; preserves already-settled PaymentTransactions at their original Shop |
| `business_note` | `id`, nullable `sale_id`, nullable `service_order_id`, `employee_id`, authorization_id, note, created_at | Exactly one Sale/ServiceOrder target |

**Service-order transfer requirement (owner-confirmed; separate SPEC pending):** a service order/work item may be sent to another Shop. The destination Shop assumes operational and financial responsibility, and all transactions from transfer onward run through that Shop. Previously settled transactions remain attributed to the source Shop; outstanding balance and held/unapplied deposit liability move to the destination through an auditable internal event without creating a cash receipt. Device custody, service-part inventory, transfer authorization and exact audit fields remain open. Do not implement transfers until these details are modeled and approved.

### Suppliers and service-part procurement

| Table | Key columns / purpose | Important constraints |
|---|---|---|
| `supplier` | `id`, `company_id`, name, website, contact_name, email, phone, notes, is_active | Company-level supplier/source directory; may represent a retailer or online marketplace; deactivation preserves purchase history |
| `service_part_requirement` | `id`, `shop_id`, `service_order_id`, `service_order_item_id`, `required_quantity INT with a non-negative CHECK constraint`, `blocks_work`, status (`OPEN`/`PARTIALLY_RESERVED`/`RESERVED`/`CANCELLED`), needed_by, created_by_employee_id, employee_action_authorization_id, created_at, cancelled_at | Exactly one requirement per product ServiceOrderItem; item must be a `PRODUCT`; positive integer quantity; order and Shop must match; `RESERVED` means the required quantity is fully reserved; reserved quantity is derived from active reservation lines |
| `service_procurement_request` | `id`, `shop_id`, `service_order_id`, `request_number`, status (`DRAFT`/`OPEN`/`IN_PROGRESS`/`PARTIALLY_ORDERED`/`ORDERED`/`PARTIALLY_RECEIVED`/`FULFILLED`/`CANCELLED`), priority, requested_at, needed_by, notes, assigned_employee_id?, created_by_employee_id, employee_action_authorization_id, completed_at, cancelled_at | One procurement work item for one ServiceOrder. A ServiceOrder can have multiple requests created at different times; unique `(service_order_id,request_number)`. All requests and lines must remain in the same Shop as the ServiceOrder. Header status is derived/transitioned from its lines and allocations |
| `service_procurement_request_line` | `id`, `service_procurement_request_id`, `service_part_requirement_id`, `quantity_requested INT with a non-negative CHECK constraint`, status (`OPEN`/`PARTIALLY_ORDERED`/`ORDERED`/`PARTIALLY_RECEIVED`/`FULFILLED`/`CANCELLED`), part_number_snapshot, compatibility_notes, notes, cancelled_at? | One requested sourcing quantity for a durable part requirement. Positive whole units. Requirement must belong to the same ServiceOrder and Shop as the request. A requirement may have multiple request lines across separate procurement rounds; active requested/ordered quantities must not double-count the same outstanding shortage. Line status is maintained by domain commands from allocations/receipts/cancellation |
| `service_procurement_request_image` | `id`, `service_procurement_request_line_id`, `stored_image_id`, image_role, caption, sort_order | One or more reference images per request line. `stored_image_id` references the PostgreSQL `stored_image` BYTEA record. Deletion is soft/audited; upload/delete requires employee authorization and access is Shop-scoped. |
| `service_procurement_request_event` | `id`, `service_procurement_request_id`, nullable `service_procurement_request_line_id`, event_type, status_snapshot, priority_snapshot, assigned_employee_id_snapshot?, payload_json, employee_id, employee_action_authorization_id, occurred_at | Append-only request history for creation, edits, line changes, priority/assignment, image upload/deletion, supplier-order linkage and cancellation; actor authorization must match the request Shop |
| `supplier_purchase` | `id`, `shop_id`, nullable `supplier_id`, source_name_snapshot, external_order_reference, purchase_url, tracking_reference, tracking_url, ordered_at, expected_delivery_at, completed_at, status (`DRAFT`/`ORDERED`/`PARTIALLY_RECEIVED`/`RECEIVED`/`CLOSED_SHORT`/`CANCELLED`), currency_code, notes, ordered_by_employee_id, employee_action_authorization_id | Shop-scoped external purchase record; optional Supplier permits one-off sources; purchase URL and tracking URL are external links only; status changes are audited; `completed_at` is set for full receipt or short-close; cancel before receipt; after partial receipt remaining quantities may be closed short without reversing accepted stock |
| `supplier_purchase_line` | `id`, `supplier_purchase_id`, `product_catalog_item_id`, quantity_ordered INT with a non-negative CHECK constraint, quantity_received INT with a non-negative CHECK constraint, unit_cost DECIMAL(19,2), product_url, notes | Product only; ordered quantity positive; `quantity_received` is cumulative accepted quantity and must be between zero and ordered; unit cost and currency are snapshots; each accepted receipt must create matching stock movement |
| `service_part_procurement_allocation` | `id`, `supplier_purchase_line_id`, `service_procurement_request_line_id`, quantity_allocated INT with a non-negative CHECK constraint, quantity_received INT with a non-negative CHECK constraint | Many-to-many bridge between supplier purchase lines and queue request lines. One supplier purchase can supply requests for multiple service orders; one request line may be split across purchases. Received allocation <= allocated; total allocations <= purchase-line ordered quantity and request-line requested quantity |
| `supplier_purchase_event` | `id`, `supplier_purchase_id`, event_type, status_snapshot, expected_delivery_at_snapshot, tracking_reference_snapshot, tracking_url_snapshot, notes, received_quantity_delta?, employee_id, employee_action_authorization_id, occurred_at | Append-only timeline; each ETA/status/tracking/receipt update requires employee authorization from the Purchase Shop |

Procurement behavior:

- `ServicePartRequirement` is the durable total demand against a product `ServiceOrderItem`. The backend checks current available stock and reserves any available quantity; only the remaining shortage is put into procurement.
- `ServiceProcurementRequest` is a separate work item in the Shop-wide **Service Parts Purchase Queue**. Each request belongs to exactly one ServiceOrder and may contain multiple request lines. A ServiceOrder may have multiple requests over its lifetime, so additional parts discovered during later diagnosis/repair can be registered without overwriting an earlier request or purchase history.
- A `ServiceProcurementRequestLine` references the durable requirement and records the quantity to source in that procurement round, plus part-number/compatibility notes. A request can be created from the ServiceOrder screen or from the queue by entering/selecting the ServiceOrder ID.
- `ServiceProcurementRequestImage` supports multiple reference photos per requested part (for example, the removed part, model/serial label, connector, dimensions or visible damage). Each relation references `stored_image`, where the normalized image bytes and optional compact thumbnail use PostgreSQL `BYTEA`. No object storage, external file service, storage key or filesystem path is used. Read through an authorized ASP.NET Core endpoint; validate content and file signatures.
- The queue aggregates requests across the active Shop and supports filtering by request status, ServiceOrder ID, customer/device, part/product, priority, created date, needed-by date and assigned buyer. The request detail opens the linked ServiceOrder and shows its photos, quantity, sourcing and supplier-purchase history.
- A `SupplierPurchase` may include stock for several request lines across multiple ServiceOrders or general Shop stock. `ServicePartProcurementAllocation` identifies the amount earmarked for each request line; the requirement is reached through that line.
- A purchase link and a tracking link are distinct: `purchase_url` opens the retailer's order/details page; `product_url` identifies the item listing; `tracking_url` opens shipment tracking. External links are not scraped or treated as integration credentials.
- Receipt is recorded by actual accepted quantity, not simply by changing status to RECEIVED. A partial receipt creates only the matching positive `PURCHASE_RECEIPT_IN` movements.
- For received quantities allocated to request lines, the same database transaction creates/extends inventory reservations for the corresponding ServiceOrder. Excess/unallocated received units stay available in the Shop.
- ETA or tracking changes create a `SupplierPurchaseEvent`. `OVERDUE` is derived when `expected_delivery_at` is in the past and the purchase is not fully received or cancelled.
- `AWAITING_PARTS` remains while any blocking requirement is not fully reserved. Once all blocking requirements are reserved, a separate employee-authorized resume command returns the order to `IN_PROGRESS`. The order status itself is not automatically derived from request or purchase status.
- Supplier bills, AP balances, payment to suppliers, procurement tax accounting, supplier returns and third-party marketplace APIs are outside the initial scope.

### Finance: transactions, service deposits, instalments, refunds and cash management

| Table | Key columns / purpose | Important constraints |
|---|---|---|
| `payment_transaction` | `id`, `shop_id`, `employee_id`, `authorization_id`, nullable `sale_id`, nullable `service_order_id`, nullable `service_deposit_id`, `kind` (`PAYMENT`/`DEPOSIT_RECEIPT`/`REFUND`/`DEPOSIT_REFUND`), positive `amount_applied`, `currency_code`, `cash_rounding_adjustment`, `original_transaction_id`, status (`PENDING`/`SETTLED`/`FAILED`/`VOIDED`/`REVERSED`), provider/reference and idempotency metadata | Exactly one of Sale, ServiceOrder or ServiceDeposit targets; kind/target combination must be valid; only SETTLED transactions affect balance/drawer; refunds point to original settled transaction and have cumulative cap |
| `payment_allocation` | `id`, `payment_transaction_id`, `payment_method_id`, `amount_applied`, `amount_tendered`, `change_amount`, provider_reference, metadata | One or more allocations; sum `amount_applied` = transaction amount; no card data such as PAN/CVV; cash drawer amount is derived from cash tender minus change and the separate rounding adjustment |
| `service_deposit` | `id`, immutable `origin_shop_id`, current `shop_id`, `service_order_id`, purpose (`PARTS_PREORDER`/`BOOKING_ADVANCE`/`SECURITY`/`OTHER`), currency_code, target_amount, status, terms_snapshot, notes, created_by_employee_id/auth | Deposit belongs to one ServiceOrder; origin is retained for receipt history; held/unapplied liability follows the current ServiceOrder Shop after transfer; it is not automatically revenue or invoice settlement |
| `deposit_application` | `id`, `service_deposit_id`, `service_order_id`, optional `service_order_item_id`, amount_applied, employee_id, authorization_id, applied_at | Non-cash credit only; target must be same ServiceOrder/Shop; cumulative application cannot exceed received less prior application/refund/forfeiture |
| `service_deposit_event` | `id`, `service_deposit_id`, event_kind (`RECEIVED`/`APPLIED`/`REFUNDED`/`FORFEITED`/`RELEASED`), payment_transaction_id?, deposit_application_id?, amount, employee_id, authorization_id, occurred_at, reason | Immutable audit of each deposit lifecycle change; event sums reconcile to received/apply/refund/forfeiture balances |
| `installment_plan` | `id`, `shop_id`, `sale_id`, plan_type (`LAYAWAY`/`CREDIT_SALE`), frequency (`WEEKLY` initially), currency_code, cash_price, amount_financed, disclosed_charges, total_payable, down_payment, first_due_date, installment_count, handover_policy, status, terms_snapshot, compliance_approval_reference | Only for Sale(s) containing serialized refurbished equipment; sum of down payment plus schedule = total payable; CREDIT_SALE disabled until Irish regulatory approval; LAYAWAY holds unit until full settlement |
| `installment_schedule` | `id`, `installment_plan_id`, sequence_number, due_date, scheduled_amount, status (`SCHEDULED`/`PARTIALLY_PAID`/`PAID`/`OVERDUE`/`CANCELLED`) | Unique `(installment_plan_id,sequence_number)`; schedule sum reconciles to plan total less down payment |
| `installment_settlement` | `id`, `installment_schedule_id`, `payment_transaction_id`, amount_applied, settled_at | Many-to-many bridge: one receipt can settle several instalments and one instalment can be paid by multiple receipts; referenced transaction must be SETTLED and belong to the plan's Sale |
| `payment_refund_line` | `id`, `refund_payment_transaction_id`, nullable `sale_item_id`, nullable `service_order_item_id`, quantity_refunded?, amount_refunded, reason | Optional line attribution; exactly one source line if provided; line amounts reconcile to refund amount when line detail is supplied; does not create stock return |
| `business_action_approval` | `id`, `shop_id`, action_code, requested_by_employee_id, approver_employee_id, approver_authorization_id, requested_amount?, status, reason, requested_at, approved_at | Optional separate approver identity/authorization for future explicitly approved dual-control actions; not used for standard refunds or deposit forfeitures, which require one Manager authorization |
| `cash_movement` | `id`, `shop_id`, `daily_closing_id`, movement_type (`CASH_IN`/`CASH_OUT`/`SAFE_DROP`), direction (`IN`/`OUT`), positive amount, reason, employee_id, authorization_id, occurred_at | Must link to the Shop's OPEN DailyClosing for the business date; cash refunds are PaymentTransactions and must not be duplicated as CashMovement |
| `daily_closing` | `id`, `shop_id`, `business_date`, status (`OPEN`/`CLOSED`), `opening_float`, opened_by_employee_id, open_authorization_id, opened_at, expected_cash, counted_cash, `cash_variance`, closing_employee_id?, close_authorization_id?, electronic_tender_snapshot_json, transaction_count, notes, closed_at? | Unique `(shop_id,business_date)`; created at opening; one physical till per Shop/day; expected cash = opening float + settled cash receipts net of change + cash-in − cash-out − settled cash refunds − settled cash deposit refunds − safe drops; non-zero variance requires reason; immutable after CLOSED |
| `daily_closing_cash_count` | `id`, `daily_closing_id`, denomination DECIMAL(6,2), quantity, subtotal | Denomination count used to calculate counted_cash; unique `(daily_closing_id,denomination)` |
| `daily_closing_correction` | `id`, `daily_closing_id`, signed_amount, reason, employee_id, authorization_id, created_at | Append-only Manager-only correction referencing the immutable close; never rewrites original expected/count/variance |

**Payment/target constraints:** `PAYMENT` and `REFUND` target exactly one Sale or ServiceOrder; `DEPOSIT_RECEIPT` and `DEPOSIT_REFUND` target exactly one ServiceDeposit. A `REFUND` references the original settled `PAYMENT`; a `DEPOSIT_REFUND` references the original settled `DEPOSIT_RECEIPT`. All records inherit the target Shop. Store amounts as positive magnitudes; sign is derived from transaction kind in reporting.

**Deposit balance:** `available_deposit = settled_deposit_receipts - deposit_applications - settled_deposit_refunds - approved_forfeitures`. A held/security deposit does not reduce the ServiceOrder balance until a distinct `DepositApplication` is recorded.

**Instalment balance:** `installment_outstanding = scheduled_amount - sum(settled InstallmentSettlement.amount_applied)`. No amount can be settled twice. The plan, schedule and receipts remain immutable/auditable; corrections are reversal/adjustment events, not edits to historical receipts.

## 3. Relationship cardinalities

- `Company 1:N Shop`; current deployment provisions one Company.
- `Shop 1:1 ShopAccessCredential`; `Shop 1:N ShopSession`.
- `Employee 1:N EmployeeRoleAssignment`; employee credentials and role assignments are Company-wide and can authorize actions in any Shop owned by that Company.
- `Company 1:N Person`, Customer, Employee, CatalogItem, ProductCategory, Device and Fault.
- `CatalogItem 1:0..1 Product` and `CatalogItem 1:0..1 Service`, exactly one subtype according to kind.
- `ProductCategory 1:N ProductAttributeDefinition`; category definitions are reused by many Products/RefurbishedUnits.
- `Product 1:N RefurbishedUnit` when inventory tracking mode is SERIALIZED_UNIT.
- `RefurbishedUnit 1:N RefurbishedUnitAttributeValue` and `RefurbishedUnit 1:N RefurbishedUnitImage`; one physical unit has one value per applicable attribute definition and may have multiple private photos.
- `RefurbishedUnit 1:0..1 RefurbishedUnitWarranty`; a sold unit gets a 12-month commercial guarantee starting at customer handover.
- `Shop 1:N ShopInventory`; `Product 1:N ShopInventory` across Shops; unique `(shop_id,product_catalog_item_id)` for quantity-tracked Products.
- `ShopInventory 1:N InventoryMovement` and `ShopInventory 1:N InventoryReservationLine`.
- `Sale` and `ServiceOrder` each have zero or more InventoryReservations; each reservation has one or more lines.
- `StockTransfer 1:N StockTransferLine`; each transfer has one source and one destination Shop.
- `StockReturn 1:N StockReturnLine`; each return belongs to one Sale or ServiceOrder.
- `Sale 1:N SaleItem`; `ServiceOrder 1:N ServiceOrderItem`, ServiceOrderEvent, ServicePartRequirement and ServiceProcurementRequest. A product ServiceOrderItem has zero or one durable requirement.
- `ServiceProcurementRequest 1:N ServiceProcurementRequestLine`; each request has one ServiceOrder, and each line references a requirement belonging to that same order. A requirement can appear in multiple procurement rounds over time.
- `ServiceProcurementRequestLine 1:N ServiceProcurementRequestImage`; a request line can have multiple private reference images.
- `ServiceProcurementRequest 1:N ServiceProcurementRequestEvent`; line-specific events may reference a request line.
- `Supplier 1:N SupplierPurchase` (supplier nullable for one-off sources); `Shop 1:N SupplierPurchase`; `SupplierPurchase 1:N SupplierPurchaseLine` and SupplierPurchaseEvent.
- `SupplierPurchaseLine N:M ServiceProcurementRequestLine` through ServicePartProcurementAllocation; received allocated quantities create reservations against the underlying requirement and purchase-receipt inventory movements. `SupplierPurchaseLine 1:N InventoryMovement` across partial receipts.
- `PaymentTransaction 1:N PaymentAllocation`; refund transactions reference an original settled payment.
- `ServiceOrder 1:N ServiceDeposit`; each deposit has receipt/refund transactions, deposit events and zero or more DepositApplications.
- `Sale 1:0..1 InstallmentPlan` for qualifying refurbished sales; each plan has many scheduled instalments; InstallmentSettlement joins schedule entries to payment transactions.
- `PaymentMethod 1:N PaymentAllocation`; ShopPaymentMethod controls per-Shop availability.
- `PaymentTransaction 1:N PaymentRefundLine` optionally attributes a partial/total refund to source lines.
- `Shop 1:N DailyClosing` with at most one close per `(ShopId,BusinessDate)`.

## 4. Critical database and application constraints

1. Provision one Company for the current deployment. All Shop records reference it; `UNIQUE(company_id,code)` is the stable Shop login identifier.
2. Every operational operation derives Shop from the validated OAuth access token and active ShopSession. Never trust a client-supplied ShopId as authorization context.
3. Use composite FKs for scope integrity: `shop(id,company_id)`, `employee(id,company_id)`, and `shop_session(id,shop_id)` candidate keys support composite authorization references; Shop-scoped children reference `(parent_id,shop_id)` where applicable.
4. Consume `EmployeeActionAuthorization` with a concurrency-safe compare-and-set in the same database transaction as the business command. Bind action and any defined canonical operation-context fingerprint.
5. Employee must be active, belong to the Company that owns the active Shop and satisfy the approved action policy. Role combinations use union semantics; MANAGER grants full access. The same employee password works across all Shops.
6. Product-only: `SERVICE` cannot have ShopInventory, reservation, transfer, return or InventoryMovement.
7. Integer inventory: quantities are whole, positive for reservation/transfer/return lines; movement deltas are signed whole integers. Product sale/order line quantities must be whole.
8. Quantity inventory: `quantity_on_hand` is a signed integer and may be negative; `quantity_reserved` remains non-negative; `available_quantity = quantity_on_hand - quantity_reserved` may be negative; enforce unique `(shop_id,product_catalog_item_id)`. Reservation commands may reserve only currently positive available units; payment settlement is allowed to post the full sale/service deduction even when it drives on-hand or available below zero. Negative balances must be visible as stock deficits and must never be converted to zero or silently suppressed.
9. Reservation source must be exactly one Sale or ServiceOrder; reservation line source must be exactly one corresponding SaleItem or ServiceOrderItem; stock target must be exactly one ShopInventory or RefurbishedUnit.
10. For each ShopInventory, `quantity_reserved` must equal active unconsumed reservation-line totals. Update reservation lines and counters under row-level locking or optimistic concurrency.
11. Reserve/release/consume operations are idempotent. A payment/stock operation may not consume the same reservation twice.
12. Payment completion for stock posting means the Sale/ServiceOrder `balance_due` reaches exactly zero through settled payments (plus explicitly applied ServiceOrder deposits). Partial payment does not consume stock. A ServiceOrder must be `FIXED` before its reserved parts are consumed. The posting occurs in the same transaction that settles the balance and is independent of the later physical `PICKED`/collection event. Quantity-tracked stock may become negative at settlement.
13. On cancellation before consumption release reservations without an inbound movement. On REFUNDED/CANCELLED after consumption, stock is returned only through explicitly selected StockReturnLines.
14. StockReturn source status must be REFUNDED/CANCELLED; returned quantity cannot exceed consumed net-of-previous-returns; serialized return must identify exact unit.
15. Transfer source/destination differ and belong to same Company. Dispatch and receipt are separate authorized transactions; dispatched quantities must be <= requested; received <= dispatched. No cancellation after dispatch without reverse transfer.
16. For serialized inventory, `RefurbishedUnit` is exactly one physical unit, has `inventory_tracking_mode=SERIALIZED_UNIT`, and a unit cannot be simultaneously RESERVED, IN_TRANSIT or SOLD in conflicting states. Serialized balances cannot be negative: a physical unit must exist. A serialized SaleItem/transfer/return has quantity exactly one.
17. ProductCategory is configurable. Attribute definitions are category-scoped; attribute value type must match definition; each unit has at most one value per definition. Each refurbished unit has a Company-unique internal asset code; capture a manufacturer serial when it exists and enforce Company-scoped uniqueness on normalized populated serial numbers.
18. `PaymentTransaction` target XOR: exactly one of Sale/ServiceOrder/ServiceDeposit; enforce kind-to-target combinations. Refund references original settled transaction and cannot exceed its remaining refundable amount under concurrency.
19. For a ServiceDeposit, settled receipts = applications + refunds + forfeitures + remaining held amount. DepositApplication is not a cash receipt and requires its own employee authorization.
20. InstallmentPlan is restricted to serialized refurbished sales; schedule totals reconcile to the agreed total, and settlements cannot exceed individual or total outstanding balances. LAYAWAY handover occurs after full settlement; CREDIT_SALE handover requires explicit compliance gate. Every refurbished unit sale gets a 12-calendar-month commercial guarantee beginning at documented customer handover; statutory Irish consumer rights are additional and are not limited by this guarantee.
21. PaymentAllocation methods are data-configured per Shop. Only SETTLED transactions affect balance and close. Provider settlement fees/payouts are reconciled separately from customer tender.
22. Partial/total refunds are positive-magnitude transactions tied to original settled payment; cumulative refunds never exceed refundable balance. Physical stock return remains a separate selected StockReturn workflow.
23. Irish cash rounding is optional per Shop, only for an eligible cash-paid final bill total, to nearest €0.05. Record signed adjustment separately; do not round line prices, tax base or card/electronic payments. VAT calculation is controlled by CompanyTaxConfiguration.vat_enabled and effective-dated company tax codes/rates; tax setup must be reviewed before production tax-document finalization.
24. `DailyClosing` uniqueness remains `UNIQUE(shop_id,business_date)`; the approved operational formula is specified in `cash-closing-policy.md`. Accounting/tax mapping and processor settlement reconciliation remain subject to finance review.
25. `ServicePartRequirement` can reference only a product line belonging to the same ServiceOrder and Shop; requirement quantity is a positive whole number. Supplier purchases do not require a second approval step; the employee who creates/records the purchase is attributed and audited.
26. A ServiceOrder may have zero or more `ServiceProcurementRequest` records, each linked to exactly one ServiceOrder and Shop. Request numbers are unique within a ServiceOrder. A later-discovered part creates a new request/round; it must not overwrite prior request or purchase history.
27. Every `ServiceProcurementRequestLine` references a requirement belonging to the same ServiceOrder/Shop as its request. Its quantity is a positive whole number and cannot exceed the currently unreserved/uncovered shortage after accounting for other active procurement requests and allocations.
28. `StoredImage` binary content and optional thumbnail use PostgreSQL `BYTEA`; application limits remain 10 MiB and 60 KiB respectively. Each owner-specific attachment table references `stored_image` using a foreign key. Image access is Shop-scoped and authorized. Validate file size, MIME by content, signature, dimensions and SHA-256. Do not use public URLs, object storage or filesystem paths. Deletion is soft/audited.
29. Request mutations and image actions append a `ServiceProcurementRequestEvent` in the same transaction as the operation; event records identify the employee and single-use authorization from the active Shop.
30. Each SupplierPurchase and its lines are Shop-scoped. A SupplierPurchaseLine can reference only a PRODUCT catalogue entry.
31. `quantity_received` on a SupplierPurchaseLine cannot exceed `quantity_ordered`; each physical receipt posts inventory movement exactly once using an idempotency key.
32. Sum of `ServicePartProcurementAllocation.quantity_allocated` for a purchase line cannot exceed its ordered quantity; sum allocated to a request line cannot exceed its requested quantity.
33. Sum of received allocation quantities cannot exceed actual purchase-line received quantity. Only the received and accepted quantity creates `PURCHASE_RECEIPT_IN`.
34. Allocated receipt quantities reserved for service orders must create InventoryReservationLine records for the same Shop and ServiceOrder in the same transaction as the receipt movement. Unallocated receipt quantities remain available.
35. Request and request-line aggregate statuses are derived from sourcing allocations, receipts and cancellations; do not allow a header to be marked `FULFILLED` while any active line remains short.
36. ETA, tracking, status and receipt history is append-only in `SupplierPurchaseEvent`; purchase URLs are data, never trusted instructions or credentials.
37. Totals and line prices are recomputed/validated server-side; never trust browser-submitted totals.

## 5. Stock lifecycle rules

| Scenario | Reservation | On-hand | Movement |
|---|---|---|---|
| Reserve quantity-based product | Reserved quantity increases | No change | No movement |
| Release/cancel reservation | Reserved quantity decreases | No change | No movement |
| Sale balance reaches zero | Active reserve consumed; remaining quantity deducted as needed | Decreases by sold product qty | `SALE_OUT` |
| ServiceOrder marked FIXED | Remains active | No change | No movement |
| Supplier purchase partially/fully received | Allocated qty reserved for service requirement; excess qty free | Increases by actual accepted qty | `PURCHASE_RECEIPT_IN` |
| ServiceOrder marked AWAITING_PARTS | Existing available portions remain reserved; no deduction | No change | No movement |
| ServiceOrder balance reaches zero | Active reserve consumed; remaining unreserved qty is deducted as required | Decreases by used product qty | `SERVICE_ORDER_OUT` (pickup is a separate later event) |
| Unpaid sale/order cancelled | Reservation released | No change | No movement |
| Selected line physically returned (proposed) | No active reservation | No change while pending inspection | No movement |
| Returned line inspected and disposition is RESTOCK (proposed) | No active reservation | Increases by restocked qty | `RETURN_IN` |
| Transfer dispatched | Reserved qty cannot be dispatched | Source decreases | `TRANSFER_OUT` |
| Transfer received | Not applicable | Destination increases by actual received qty | `TRANSFER_IN` |

For serialized refurbished units, the equivalent operation is a status transition and a `-1` / `+1` movement tied to the exact unit, rather than changing a generic aggregate quantity.

## 6. Reporting and reconciliation semantics

- Gross sale/order value comes from Sale/ServiceOrder, not from summing tender allocations.
- Cash/card totals come from PaymentAllocation grouped by method, transaction kind and Shop.
- Operational reports default to current Shop context; Company-level reports may aggregate all Shops with explicit permission.
- Product stock report distinguishes on-hand, reserved, available and in-transit quantities.
- Transfer in-transit quantity is not included in either source or destination available stock.
- Serialized refurbished stock reports count `RefurbishedUnit` records by Shop and status; never add a second aggregate quantity that can drift.
- Return reports distinguish monetary refunds from physically returned quantities.
- Each Shop's expected drawer uses its own cash allocations and CashMovements only.

## 7. Explicitly unresolved schema choices

- Reservation-expiry automation is out of initial scope; reservations remain until explicitly consumed or released. Partial sourcing/reservation is supported for service-part requirements.
- Multiple warehouses/shelves/stock locations inside a Shop are out of initial scope; the initial model uses one stock pool per Shop.
- An explicit loss-adjustment/write-off process for unrecoverable transfer discrepancies remains out of scope; such transfers stay open until separately resolved.
- Refurbished unit identity is decided: company-unique internal asset code is required; manufacturer serial is captured when present and is Company-unique when populated. Acquisition/source details and commercial-guarantee coverage/exclusions are documented as proposed v1 policy in refurbished-condition-and-complaints.md, pending customer-facing legal review.
- MANAGER full access and the approved TECHNICIAN restricted service/technical permissions are defined in employee-access-and-roles.md. Customer/device visibility remains service-context only for TECHNICIAN in v1.
- Initial tender methods are CASH/CARD only. External card terminal/provider integration is deferred; VAT legal status/rates and tax-inclusive/exclusive display remain subject to later accountant review. Accounting/bookkeeper reconciliation mapping is deferred by owner instruction.
- One Shop/day open-close formula and opening-float workflow are specified in cash-closing-policy.md; future multiple-till support remains out of scope.
- Whether supplier returns, supplier invoice/AP settlement, procurement VAT accounting or automated retailer/marketplace integration should be added after the operational procurement tracker. Supplier purchase approval is explicitly not required.
- Complete Sale/ServiceOrder status transition matrix. Exact PostgreSQL image-retention period/GDPR purge schedule and final customer-facing deposit/refurbished guarantee terms remain to be approved.

## 8. Implementation warning

This document is a logical proposal, not an executable migration. Do not create production migrations or tables until the open constraints above are reviewed. The legacy `schema.sql` is a historical structure dump with `DROP TABLE IF EXISTS`; it must not be executed against an existing database.


See the [consolidated product-owner decision register](product-owner-decisions.md) for confirmed choices and remaining questions.

## Document history and reprint audit (REP-02)

The document module uses versioned application templates and immutable render snapshots. It does not store arbitrary templates in the database.

### `GeneratedDocument`

- `GeneratedDocumentId` PK (UUID in the initial proposal).
- `CompanyId`, `ShopId` — tenant/branch boundary; `ShopId` is mandatory.
- `DocumentType`, optional `DocumentNumber`.
- `SourceType`, `SourceId`, `SourceVersion` — identifies the originating business record; application validation must confirm Company/Shop ownership.
- `TemplateKey`, `TemplateVersion`, `LayoutCode` (`A4` or `THERMAL_80MM`).
- `IssuedAtUtc`, `IssuedByEmployeeId`.
- `SnapshotJson` — immutable customer-facing/operational render data.
- `ContentHash`, `Status` (`ISSUED`, `VOIDED`).

### `DocumentReprintEvent`

- `DocumentReprintEventId` PK.
- `GeneratedDocumentId` FK, `ShopId` FK, `PrintedAtUtc`, `PrintedByEmployeeId` FK.
- `LayoutCode`, `CopyLabel`, optional `Reason`.

### Constraints

- A document and every reprint are scoped to the same Company/Shop as the source record.
- Issued snapshots are immutable. Void/correction creates a new document or correction record; it does not edit the issued snapshot.
- Reprint events are append-only and never create a new payment, refund, sale, stock movement or cash movement.
- PDF binaries are not stored in PostgreSQL in v1; the immutable JSON snapshot and retained template version are used to regenerate the PDF. A future PDF binary policy requires a separate approval and capacity/backup review.
- The source reference is polymorphic (`SourceType` + `SourceId`); enforce it in the application/service layer and with domain-specific validation. Do not trust client-provided Shop IDs.

See [Reports, Receipts and Document Printing](reports-and-document-printing.md) and [focused document ERD](er-diagram-documents.svg).

