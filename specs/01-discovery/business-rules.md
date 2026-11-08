# Business Rules — Initial Evidence

**Status:** DISCOVERY  
**Policy:** This document records only rules evidenced in source files inspected so far. It is not yet a complete or approved business specification.

## Sales

Source: `views/NewSaleView.java`

- **CONFIRMED:** First name, last name, contact number and at least one product/service row are required by `validateRequiredFields`.
- **CONFIRMED:** The flow requests an employee password and resolves an employee before constructing a sale.
- **CONFIRMED:** Product/service rows contribute to a displayed total.
- **CONFIRMED:** Customer lookup/matching is performed when an existing customer is selected.
- **OPEN:** Whether an email is mandatory.
- **OPEN:** Which payment methods and partial-payment rules are supported.
- **OPEN:** Whether discounts, price overrides or refunds are allowed in this workflow.

Source: `controllers/SaleController.java`

- **CONFIRMED:** Sale creation can include a sale, sale items, payments, deposit and note in a single use-case call.
- **CONFIRMED:** Refurbished sales have a separate creation flow.

## Service orders

Source: `views/NewServiceOrderView.java`

- **CONFIRMED:** First name, last name, contact, device brand, device model, serial number, at least one product/service and at least one fault are required by the inspected save validation.
- **CONFIRMED:** The operator is resolved using a password before the order is created.
- **CONFIRMED:** The workflow supports searching and adding faults and products/services.
- **OPEN:** Exact semantics of `badSector`.
- **OPEN:** Which status transitions are permitted and by whom.
- **OPEN:** Deposit and remaining-balance rules.
- **OPEN:** Whether serial number uniqueness is required.

Source: `controllers/ServiceOrderController.java`

- **CONFIRMED:** Service orders can be created, updated and transferred.
- **CONFIRMED:** A creation call can include faults, product/services, payments, deposit and note.

## Sale model

Source: `models/Sale.java`

- **CONFIRMED:** Setter rejects a negative total.
- **CONFIRMED:** Setter rejects a negative remaining balance.
- **OPEN:** Whether zero totals are valid in the domain.
- **REDESIGN:** Use decimal monetary types.

## Status enumeration

Source: `enums/OrderStatus.java`

Stored labels are IN_PROGRESS, FIXED, NOT_FIXED, FINISHED, PICKED, REFUNDED, CANCELLED and CREATED.

**OPEN:** Map valid transitions and identify which views/actions cause each transition before implementing a state machine.

## Important implementation note

Do not translate UI validation directly into API validation without evaluating the domain. API validation must be authoritative so that the rules apply regardless of client.

## Target-domain decisions recorded (2026-11-04; authentication clarification 2026-11-05)

The following are product-owner decisions for the new system, not claims about legacy behavior:

- **DECIDED — One Company, multiple Shops:** one legal/business company has multiple store/branch records. Company-level master data is shared; sales, service orders, inventory, payments and cash movements are scoped to the active Shop.
- **DECIDED — Shop session:** after initial Shop authentication, the persistent application session represents the Shop, not an employee. Any employee may use the active Shop session.
- **DECIDED — Employee attribution per protected action:** the employee performing each committed business operation must authenticate with their own credential. A short-lived, single-use authorization is consumed for that operation; the employee identity is not retained as the persistent session identity.
- **DECIDED — Product-only stock:** only catalogue entries of kind `PRODUCT` have stock balances/movements; balances are separate per Shop. `SERVICE` entries are non-stock.
- **DECIDED — Transaction payments:** each payment/deposit/refund event is one `PaymentTransaction`, which can have multiple `PaymentAllocation` rows (e.g. cash plus card). The gross amount is stored once.
- **DECIDED — Shop/date closing:** each Shop has at most one `DailyClosing` per business date, unique on `(ShopId, BusinessDate)`. Each close includes only that Shop’s operations; other Shops may close the same date independently.

- **DECIDED — Whole product quantities:** product stock, reservations, transfers, returns and product lines use whole units; fractional product stock is invalid.
- **DECIDED — Optional reservation:** product stock may be explicitly reserved and the reservation may be released/cancelled without changing on-hand stock. A later payment rechecks availability before deduction.
- **DECIDED — Sale stock posting:** product stock is deducted only when settled payments reduce the sale balance to zero. Partial payments do not consume stock.
- **DECIDED — Service stock posting:** an order moving to `FIXED` keeps its product reservations. Product stock is deducted when settled payments and explicitly applied deposits reduce the service balance to zero (the defined payment-completion trigger); the later `PICKED` event records handover and does not trigger stock deduction. Quantity-tracked stock may become negative. Deposits not explicitly applied and partial payments do not consume stock.
- **DECIDED — Selected return lines:** when a Sale or ServiceOrder is `REFUNDED` or `CANCELLED`, the operator may select product lines/quantities physically returned to stock. Unselected items are not restocked automatically. Financial refund is separate from physical stock return.
- **DECIDED — Inter-shop transfer:** use transfer document with source dispatch, in-transit status and destination receipt; only receipt adds quantity to destination stock.
- **CONFIRMED — Service-order transfer:** an existing service order/work may be sent to another Shop, separate from physical Product stock transfer. The destination Shop assumes operational and financial responsibility; all transactions from transfer onward run through it. Settled transactions remain with the source Shop; open balance and held/unapplied deposit liability move to the destination through an audited internal event without a cash receipt. Device custody and product-part inventory attribution remain OPEN.
- **DECIDED — Refurbished equipment:** support configurable categories such as PCs, consoles and TVs, category-defined technical fields and one serialized record per physical unit.

The precise requirements and remaining decisions are documented in [Architecture Decision Records](../02-architecture/architecture-decisions.md), [Target Domain Model](../02-architecture/domain-model.md) and [Relational Database Model](../02-architecture/database-model.md).


- **DECIDED — Service parts procurement:** when a required product part is unavailable or only partly available, the order can enter `AWAITING_PARTS`. Available quantities are reserved; shortages are tracked in a Shop-wide Service Parts Purchase Queue through multiple `ServiceProcurementRequest` rounds per ServiceOrder, with line-level reference images, source/order links, expected delivery dates and tracking links. Partial receipts post only actual received stock and reserve allocated quantities. When all blocking requirements are reserved, an employee explicitly resumes repair to `IN_PROGRESS`. Supplier payment/AP and supplier-return handling remain outside initial scope.

See [Service Parts Procurement Workflow](../02-architecture/service-parts-procurement.md), [Service Parts Purchase Queue](../02-architecture/service-procurement-queue.md), and ADR-013/ADR-014 for the target design.
