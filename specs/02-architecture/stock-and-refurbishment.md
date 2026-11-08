# Stock, Reservations, Transfers and Refurbished Equipment

**Status:** DRAFT — owner decisions recorded 2026-11-05, reconfirmed in v12, and physical return disposition approved 2026-11-08. See ADR-003, ADR-006 through ADR-008 and ADR-018.

## 1. Quantity policy

- Product stock is measured in indivisible whole units. Fractional product stock is invalid.
- Inventory balances, reservation lines, transfer lines, return lines and inventory movement deltas are integer values.
- A product line in a Sale or ServiceOrder must have a whole-number quantity. Service catalogue lines do not affect inventory; any future fractional service billing must use a separate service quantity/measurement rule.
- No stock records may be created for `CatalogItem.kind = SERVICE`.

## 2. Availability model

For quantity-tracked products:

`available_quantity = quantity_on_hand - quantity_reserved` (may be negative)

- `quantity_on_hand`: signed integer balance recorded at the Shop. It may be negative by explicit owner decision.
- `quantity_reserved`: non-negative whole units committed to active Sale/ServiceOrder reservations.
- `available_quantity`: derived as on-hand minus reserved. It may be negative and represents an inventory deficit/backorder, not sellable stock.
- A reservation command can reserve only currently positive available units. Reservation does not create physical stock. A later payment posting from another transaction can drive the balance/availability negative while existing reservations remain visible; the application must surface the deficit and must not silently invalidate or release those reservations.
- Standard quantity-tracked Product stock may be negative. Serialized refurbished units are physical assets and cannot be negative or fabricated.

For serialized refurbished products, inventory is the set of physical `RefurbishedUnit` rows. Available stock is the number of units at the Shop with status `AVAILABLE`; reserved units are status `RESERVED`. No aggregate ShopInventory balance is manually maintained for serialized units.

## 3. Reservation lifecycle

| Action | State effect | Stock effect |
|---|---|---|
| Reserve | Create `InventoryReservation` and line(s) | Increase reserved counter or set exact unit to RESERVED; on-hand unchanged |
| Cancel/release | Reservation/line becomes RELEASED | Decrease reserved counter or return unit to AVAILABLE; on-hand unchanged |
| Consume | Reservation/line becomes CONSUMED | Decrease reserved and on-hand, or set serialized unit to SOLD |

Reservations are explicit and optional. Creating a Sale or ServiceOrder does not silently reserve every product line. The UI should expose Reserve and Release Reservation actions with the employee challenge required by the session/security model.

A reservation is tied to exactly one Sale or ServiceOrder and to the specific source line. A new reservation after release creates a new reservation record so the history of each hold/release is retained. Expiration is not automatic until an expiration duration is approved.

## 4. Sale stock timing

1. The Sale may have product lines reserved before payment.
2. Payment transactions may be partial. Partial payment alone does not post stock.
3. When the Sale balance reaches zero through settled payment transactions, stock for all product lines is consumed in one atomic operation. Active reservations are consumed first. This is the defined meaning of payment completion for stock posting.
4. Negative stock is permitted for quantity-tracked products. If a product line exceeds the current available balance, settlement still completes and the movement may take `quantity_on_hand` below zero. Record the exact signed deficit and surface it in inventory reports; do not truncate to zero. Serialized refurbished units remain non-negative and must identify the exact physical unit.
5. Repeated/retried settlement commands must be idempotent and must not create duplicate stock movements.

**Owner decision recorded:** deduction occurs only when the Sale is fully settled (balance due reaches zero), not on the first partial payment. For LAYAWAY refurbished plans, the serialized unit remains reserved until full settlement/handover; a serialized unit can never become a negative quantity.

## 5. Service-order stock timing

1. When a `ServicePartRequirement` is created, the Shop reserves currently available whole units immediately, when reservation is requested. If only part is available, reserve that portion and procure the remaining shortage; a blocking shortage moves the ServiceOrder to `AWAITING_PARTS`.
2. A service deposit received at booking (for example, to secure a battery/keyboard order or as a guarantee) is recorded as a financial deposit. Receiving it does not itself consume stock.
3. The ServiceOrder cannot transition to `FIXED` while any blocking part requirement remains short. Once all blocking requirements are reserved, an employee-authorized command resumes the order to `IN_PROGRESS`; after repair, transition to `FIXED` keeps the reservations while the repaired equipment awaits payment/pickup.
4. When settled payments and explicitly applied deposits reduce the ServiceOrder balance to zero, the service payment command consumes reserved product parts atomically. This is the defined meaning of payment completion for stock posting; partial settlement does not deduct stock. Deduction is not delayed until the later physical `PICKED`/collection event.
5. Non-blocking product entries follow the same full-settlement deduction rule. If a quantity-tracked balance becomes negative because of the payment posting, the signed deficit is retained and reported.
6. If the order is cancelled before consumption, active reservations are released; no return movement is posted.

## 5A. Service-part procurement and receipt

The detailed workflow is specified in [Service Parts Procurement Workflow](service-parts-procurement.md). The inventory rules are:

1. Every part needed for a service job must exist as a product `ServiceOrderItem` and a `ServicePartRequirement`. A requirement has a positive integer quantity and may be marked `blocks_work` (default true).
2. When a requirement is created, the Shop may reserve currently available units. If a blocking requirement is not fully reserved, the ServiceOrder moves to `AWAITING_PARTS`. A partially available requirement may be fulfilled by existing stock plus one or more supplier purchases.
3. A Shop can create a `SupplierPurchase` with a supplier/source, order reference, purchase link, product listing link, order date, expected delivery date, tracking link/reference and notes. One purchase can contain several purchase lines and allocations to procurement request lines from multiple service orders in the same Shop; the request lines link back to durable service-part requirements.
4. When goods arrive, an employee enters the actual accepted quantity, including partial deliveries. This posts `PURCHASE_RECEIPT_IN` only for the actual quantity received. Allocated quantities are reserved against the linked service requirements in the same transaction; unallocated over-order quantity stays available as normal Shop stock.
5. Every order-status, ETA, tracking and receipt change is recorded in an append-only procurement event timeline. An overdue indicator is derived when the ETA passes while the purchase remains open.
6. When every blocking requirement is fully reserved, an employee-authorized command resumes the ServiceOrder to `IN_PROGRESS`. Receipt is not an automatic repair-start event.
7. Supplier purchasing is operational traceability plus inventory receipt. Supplier accounts payable, settlement, supplier returns and marketplace API integrations are out of the initial scope.

## 6. Refund/cancellation and selected stock return

- A financial refund and a physical stock return are separate actions and separate records.
- When the Sale/ServiceOrder status is `REFUNDED` or `CANCELLED`, the UI may offer an explicit selection of consumed product lines and quantities to return to stock.
- The selection creates a `StockReturn` with one or more `StockReturnLine` rows. Only selected quantities are credited to inventory.
- A quantity return cannot exceed `consumed_quantity - already_returned_quantity` for the source line.
- An unpaid cancellation only releases reservations; it must not create a positive `RETURN_IN` movement because no stock was previously deducted.
- For serialized refurbished units, the exact original unit must be selected; a different device with similar model/specification is not interchangeable in the stock ledger.

### Approved return inspection and disposition

- Physical receipt creates a selected `StockReturnLine` in `PENDING_INSPECTION`; this records custody but does not increase sellable stock.
- An authorized inspection assigns one disposition: `RESTOCK`, `QUARANTINE` or `DISPOSE`. Only `RESTOCK` creates a positive `RETURN_IN` movement and increases `ShopInventory`.
- A serialized refurbished return must be the exact unit from the original sale. On receipt it becomes `QUARANTINED`; it can return to `AVAILABLE` only after a passing inspection and explicit `RESTOCK` disposition.
- For quantity-tracked products, split a return line when only part of its quantity can be restocked. Quantities pending inspection or quarantined are excluded from sellable on-hand/available balances; keep the return record as their custody/audit record.
- Refund settlement and `REFUNDED`/`CANCELLED` status never trigger physical receipt, inspection or restocking. Unpaid cancellation still releases reservations only.

This decision determines stock availability and audit mechanics only. It does not decide refund entitlement, consumer-law remedies, employee role permissions or any customer-facing assessment standard.

## 7. Inter-shop transfer

A transfer has source Shop, destination Shop, one or more product lines and a lifecycle:

`DRAFT -> IN_TRANSIT -> PARTIALLY_RECEIVED -> RECEIVED`

- `DRAFT`: no stock effect. It can be cancelled.
- `IN_TRANSIT`: source has dispatched stock; source on-hand decreases; destination has not yet received it. In-transit stock is not available.
- `PARTIALLY_RECEIVED`: some dispatched units were received; destination on-hand increases only by actual received quantity; outstanding quantity remains in transit.
- `RECEIVED`: all dispatched quantity is accounted for at destination.
- `CANCELLED`: allowed only before dispatch. A dispatched transfer requires a reverse/corrective transfer rather than status-only cancellation.

Dispatch is an employee-authorized action in the source Shop session. Receipt is a separate employee-authorized action in the destination Shop session. Both Shops must belong to the same Company and must be different. At dispatch, only unreserved available quantities may leave the source. At receipt, record actual count, not merely requested count.

Serialized refurbished devices transfer by unit ID with quantity exactly one. A serialized unit in transit is unavailable in both Shops.

The initial design has one stock pool per Shop. Multiple storerooms, shelves or warehouses inside one Shop are intentionally not modelled yet.

## 8. Refurbished categories, technical specifications, condition grades, complaints, images and commercial guarantee

### Product classification

- `ProductCategory` is configurable and hierarchical; examples: PC, Notebook, Console, TV, Monitor.
- A Product can use `QUANTITY` tracking (standard electronic parts/accessories) or `SERIALIZED_UNIT` tracking (individual refurbished equipment).
- A refurbished product model/SKU references a category. Each physical refurbished device has exactly one `RefurbishedUnit` record.

See [Refurbished Condition Grades and Customer Complaint Workflow](refurbished-condition-and-complaints.md) for grade meanings, inspection rules and the complaint/claim lifecycle.

### Unit identity and lifecycle

Recommended unit fields:

- Internal `asset_code` (required and unique), QR/barcode value if used.
- Manufacturer `serial_number` (capture whenever the manufacturer provides one; optional only when no manufacturer serial exists).
- `brand`, `model`, condition grade, free-text description/notes. `asset_code` is mandatory and unique within the Company; the normalized manufacturer + serial pair is also unique within the Company when populated.
- Acquisition date/cost and asking price if the purchase/resale process needs them.
- `current_shop_id` and status (`AVAILABLE`, `RESERVED`, `IN_TRANSIT`, `SOLD`, `QUARANTINED`, `DISPOSED`).

A serialized unit has only one active state at a time. Reservation, dispatch, sale and return commands update unit status and inventory movement atomically.

### Flexible technical specifications

`ProductAttributeDefinition` defines fields by category. Example definitions:

| Category | Possible technical attributes |
|---|---|
| PC / Notebook | Processor, RAM, GPU, SSD/HDD capacity, screen size, operating system |
| Console | Generation/model, storage capacity, included controller count, included accessories |
| TV / Monitor | Screen size, resolution, panel technology, refresh rate, smart platform |

`RefurbishedUnitAttributeValue` stores one typed value per definition. New category attributes can be added through configuration/data, without creating a new SQL column or product-specific table. The value type must match the definition (`TEXT`, `NUMBER`, `BOOLEAN`, `DATE` or `OPTION`). Numeric attributes may carry a unit such as GB, GHz, inches or Hz.

### Refurbished-unit commercial guarantee

Every refurbished unit sold by the Company carries a 12-month commercial guarantee beginning on the actual customer handover/collection date recorded by the system. The guarantee record is linked to the exact physical `RefurbishedUnit` and its `SaleItem`; it stores the start/end dates and a snapshot of the terms shown to the customer. The commercial guarantee is additional to Irish statutory consumer rights and must never be described as limiting or replacing them. Irish consumer guidance states that statutory rights apply regardless of a guarantee/warranty and apply to second-hand goods as well. The 12-month term is only the additional commercial guarantee promised by this product owner; it is not a cap on statutory remedies. See the [CCPC guidance on guarantees and warranties](https://www.ccpc.ie/consumer-advice/consumer-rights/buying-goods/guarantees-and-warranties) and [rights when buying goods](https://www.ccpc.ie/consumer-advice/consumer-rights/buying-goods/your-rights-when-buying-goods/).  A proposed standard coverage/exclusion baseline is included in [Refurbished Condition Grades and Customer Complaint Workflow](refurbished-condition-and-complaints.md); final customer-facing terms still require owner/legal review. Before launch, verify the Irish/EU consumer-information display requirements for in-store and online refurbished sales; CCPC guidance reports a standard consumer-rights notice requirement effective 27 September 2026. See [CCPC guarantees and warranties](https://www.ccpc.ie/consumer-advice/consumer-rights/buying-goods/guarantees-and-warranties), [CCPC rights when buying goods](https://www.ccpc.ie/consumer-advice/consumer-rights/buying-goods/your-rights-when-buying-goods/) and [CCPC business guidance for sellers](https://www.ccpc.ie/information-for-businesses/selling-goods-and-services/selling-products).

## 9. Required validation and idempotency

- Lock the relevant ShopInventory row while reserving, releasing, consuming, transferring or returning quantity-tracked stock, or use an optimistic concurrency token with retry-safe commands.
- Verify availability after acquiring the lock; pre-read UI values are not authoritative.
- Use a unique command/idempotency key for payment settlement, transfer dispatch/receipt and stock returns.
- Every movement records Shop, Product, employee, action authorization, time, reason and source document/line.
- Never mutate `quantity_on_hand` without inserting the matching InventoryMovement in the same database transaction.
- Reconcile the materialized reserved counter against active reservation lines periodically and provide an administrative repair/audit path.
- For serialized refurbished units, enforce transitions with concurrency checks and prevent the same unit from being reserved/sold/transferred twice.

## 10. Remaining stock/refurbishment decisions

1. Serial policy is specified: internal asset code is mandatory/Company-unique; manufacturer serial is captured when present and is Company-unique when populated. Exact acquisition/source fields can be refined during DDL review.
2. The 12-month commercial guarantee duration is confirmed. Proposed coverage/exclusions and remedy terms are in [Refurbished Condition Grades and Customer Complaint Workflow](refurbished-condition-and-complaints.md), but must be reviewed before customer-facing publication.
3. Initial stock design is one stock pool per Shop. Reservation-expiry automation and multiple internal stock locations are intentionally out of scope. Reservations remain active until explicitly released or consumed. Partial reservation/sourcing is supported for service-part requirements.
4. Returned goods enter pending inspection outside sellable stock; only an explicit RESTOCK disposition credits inventory. Serialized units remain quarantined until inspection passes and restock is approved.
5. `CREDIT_SALE` handover-before-full-payment remains disabled pending Irish legal/regulatory review; the initial enabled mode is `LAYAWAY`.
