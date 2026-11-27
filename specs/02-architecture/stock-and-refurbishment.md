# Stock, Reservations, Transfers and Refurbished Equipment

**Status:** DRAFT — owner decisions recorded 2026-11-05, reconfirmed in v12, physical return disposition and product-transfer discrepancy handling approved 2026-11-08, and discrepancy permissions approved 2026-11-21. Cross-Shop service-order transfer is a separate requirement with a separate SPEC pending. See ADR-003, ADR-006 through ADR-008 and ADR-018.

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

1. Creating a `ServicePartRequirement` does not reserve stock. Stock replenishment increases on-hand and available quantity only. An employee must explicitly select the ServiceOrder requirement and requested quantity in a separate reservation action. The backend reserves no more than the currently available quantity; if less than requested is available, it creates a partial reservation for the available amount and reports the actual reserved and remaining quantities. A blocking requirement that remains short keeps the ServiceOrder in `AWAITING_PARTS`.
2. A service deposit received at booking (for example, to secure a battery/keyboard order or as a guarantee) is recorded as a financial deposit. Receiving it does not itself consume stock.
3. The ServiceOrder cannot transition to `FIXED` while any blocking part requirement remains short. Once all blocking requirements are reserved, an employee-authorized command resumes the order to `IN_PROGRESS`; after repair, transition to `FIXED` keeps the reservations while the repaired equipment awaits payment/pickup.
4. For a ServiceOrder whose technical outcome is `FIXED`, settled payments and explicitly applied deposits reducing the balance to zero consume reserved Product parts atomically. Partial settlement does not deduct stock. The consumption is not delayed until physical `PICKED`/collection.
5. For a ServiceOrder whose technical outcome is `NOT_FIXED`, reaching a zero balance does not consume or release stock. Reservations remain active until the customer handover action. At handover, the employee must explicitly record the quantity actually used from each reserved Product requirement. The command consumes only those recorded used quantities and releases every unused reserved quantity, atomically with the handover. If no part was used, all reservations are released. This applies whether the order is fully paid or still has an outstanding balance; handover itself does not settle the financial balance. Only quantities covered by active reservations can be recorded as used through this action.
6. The `NOT_FIXED` handover usage record is a separate stock/ServiceOrder event, attributed to the employee and committed with the inventory movements and ServiceOrder `Notes`. A failed or rolled-back handover creates no usage, movement, release or Note.
7. Non-blocking Product entries follow the same consumption timing as the ServiceOrder's technical outcome: full settlement for `FIXED`, explicit actual-usage recording at handover for `NOT_FIXED`. If a quantity-tracked balance becomes negative because of a permitted settlement posting, the signed deficit is retained and reported.
8. If the order is cancelled before stock consumption, all active ServiceOrder reservations are released atomically with cancellation; reserved and available counters are updated, on-hand is unchanged, and no inbound/return movement is posted. If any parts were already consumed, cancellation does not reverse that consumption or automatically restock the parts. Any physically returned parts must use the separate selected StockReturn and inspection/disposition workflow.

## 5A. Service-part replenishment and reservation

The former supplier-procurement workflow is retired. Parts to Buy is only a visibility list; purchases happen outside the application. Inventory replenishment is recorded through the normal Stock module and is not linked to an external purchase or a ServiceOrder allocation.

1. Every part needed for a service job must exist as a Product `ServiceOrderItem` and a `ServicePartRequirement`. A requirement has a positive integer quantity and may be marked `blocks_work` (default true).
2. Creating a requirement does not reserve stock. A stock receipt increases on-hand and available quantity only; it never automatically reserves or allocates received units to an order.
3. `EMPLOYEE`, `MANAGER` or `ADMIN` may explicitly reserve available stock for a ServiceOrder requirement. Each reservation action requires a fresh employee code/password challenge; the backend resolves the employee, validates active status, Company membership and permission, and derives the Shop from ShopSession.
4. The operator selects the target ServiceOrder requirement and requested whole-unit quantity. The backend checks current availability and creates the reservation atomically. It must never reserve more than the currently available quantity. If availability is lower than requested, reserve the available quantity as a partial reservation and return the actual reserved and remaining quantities to the UI. If no quantity is available, create no reservation and report the shortage.
5. Each successful reservation/release records the server-resolved employee and timestamp in the relevant immutable inventory/ServiceOrder event and ServiceOrder `Notes`, atomically with the stock update. A failed or rolled-back action creates no event or Note.
6. A blocking requirement that remains short keeps the ServiceOrder in `AWAITING_PARTS`. When every blocking requirement is fully reserved, an explicit employee-authorized resume command is still required to return the order to `IN_PROGRESS`; reservation itself never resumes work.
7. Releasing a reservation is a separate explicit, employee-authorized action, except when a defined lifecycle operation (such as eligible cancellation) releases reservations atomically as part of that operation. Release restores availability only and does not change on-hand stock.
8. No supplier, purchase-order, purchase allocation, ETA, tracking, supplier receipt or procurement event is recorded in this workflow.

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

### Approved transfer discrepancy handling

- Post inbound inventory only for quantities physically counted and accepted by the destination Shop; accepted quantity cannot exceed dispatched quantity.
- A short or damaged quantity is not accepted into destination sellable stock. Keep it outstanding on the transfer and record the discrepancy, reason, observed quantity and employee authorization. The transfer remains `PARTIALLY_RECEIVED` until every dispatched quantity is received or corrected by a separately documented transfer.
- Do not automatically write off lost or damaged in-transit units. If the goods cannot be received or corrected, the transfer remains open; any future loss-adjustment workflow requires a separate decision and authorization.
- If more units arrive than were dispatched, do not post the excess to inventory or alter the dispatch record. Record the overage and handle the extra units through a separately authorized transfer/document.
- For serialized units, reconcile the exact dispatched unit IDs. A damaged unit stays unavailable until the receiving Shop records its disposition; an unaccounted unit keeps the transfer open.

Every discrepancy action requires fresh direct employee credential validation, including active status and Company membership. The backend resolves the acting employee and enforces the following approved permissions:

| Discrepancy action | EMPLOYEE | MANAGER | ADMIN |
|---|---:|---:|---:|
| Register a discrepancy | Yes | Yes | Yes |
| Add reason, notes and evidence | Yes | Yes | Yes |
| Record a later physical receipt against the outstanding quantity | Yes | Yes | Yes |
| Authorize an administrative resolution | No | Yes | Yes |
| Authorize an exceptional administrative closure | No | Yes | Yes |

Registration records the observed facts; it does not authorize a write-off, alter dispatched quantities or close the transfer. A later physical receipt is processed through the normal receipt command and can accept only verified, acceptable quantities within the dispatched outstanding quantity. A corrective transfer remains a separately documented transfer and follows its own dispatch/receipt authorization.

Only MANAGER or ADMIN may authorize an administrative resolution or exceptional closure. This permission does not create an automatic loss write-off or permit changing the original dispatch/receipt evidence. The resolution must retain its reason, employee attribution and link to the discrepancy; any stock adjustment requires a separately approved stock-adjustment workflow. Every protected action requires its own fresh employee code/password challenge.

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

- For stock commands linked to a ServiceOrder, first lock the parent `service_order` row (`SELECT ... FOR UPDATE`) before locking reservation rows and then `shop_inventory` rows. This shared parent lock serializes reservation changes with payment settlement, deposit application, Pickup and cross-Shop ServiceOrder transfer. Lock multiple reservation and ShopInventory rows in ascending ID order. For standalone stock commands without a ServiceOrder, lock the relevant ShopInventory row while reserving, releasing, consuming, transferring or returning quantity-tracked stock, or use an optimistic concurrency token with retry-safe commands.
- Verify availability after acquiring the lock; pre-read UI values are not authoritative.
- ServiceOrder-linked stock commands use PostgreSQL `READ COMMITTED` with the canonical lock order documented in [Relational Database Model](database-model.md), section 4: ServiceOrder first, then transfer/deposit/payment rows when relevant, reservation rows, and ShopInventory. Re-read reservation and availability state after acquiring locks; do not rely on pre-lock UI values. A command must participate in the caller's transaction when invoked by payment settlement, Pickup or transfer acceptance.
- Use a unique command/idempotency key for payment settlement, transfer dispatch/receipt and stock returns.
- Every movement records Shop, Product, the server-resolved employee actor, time, reason and source document/line. Protected stock commands validate the employee's fresh code/password directly; do not persist or reference an EmployeeActionAuthorization or authorization ID.
- Never mutate `quantity_on_hand` without inserting the matching InventoryMovement in the same database transaction.
- Reconcile the materialized reserved counter against active reservation lines periodically and provide an administrative repair/audit path.
- For serialized refurbished units, enforce transitions with concurrency checks and prevent the same unit from being reserved/sold/transferred twice.

## 10. Remaining stock/refurbishment decisions

1. Serial policy is specified: internal asset code is mandatory/Company-unique; manufacturer serial is captured when present and is Company-unique when populated. Exact acquisition/source fields can be refined during DDL review.
2. The 12-month commercial guarantee duration is confirmed. Proposed coverage/exclusions and remedy terms are in [Refurbished Condition Grades and Customer Complaint Workflow](refurbished-condition-and-complaints.md), but must be reviewed before customer-facing publication.
3. Initial stock design is one stock pool per Shop. Reservation-expiry automation and multiple internal stock locations are intentionally out of scope. Reservations remain active until explicitly released or consumed. Partial reservations are supported for service-part requirements; purchases and sourcing happen outside the application.
4. Returned goods enter pending inspection outside sellable stock; only an explicit RESTOCK disposition credits inventory. Serialized units remain quarantined until inspection passes and restock is approved.
5. `CREDIT_SALE` handover-before-full-payment remains disabled pending Irish legal/regulatory review; the initial enabled mode is `LAYAWAY`.


## 11. Legacy evidence reconciliation

See [Legacy Stock and ServiceOrder Reconciliation](legacy-stock-service-order-reconciliation.md) for the source-level comparison with `hermanCosta/pchousestoremvn`. The legacy application stores a manually editable product quantity and provides a low-stock listing, but the reviewed source does not establish a stock ledger, reservation lifecycle or automatic ServiceOrder/Sale stock deduction. The target has no minimum-stock field or reorder threshold: `qty` represents physical on-hand stock (`quantity_on_hand`), while reservations are tracked separately. The target stock ledger, explicit reservations, settlement consumption and independent pickup/refund/cancellation flows are approved redesign behavior, not legacy parity. Import legacy physical quantities only after a separate data-validation decision.


## ServiceOrder Pickup stock rules (2026-11-20)

- Pickup is allowed from both `FIXED` and `NOT_FIXED`; lifecycle becomes `PICKED` while technical outcome remains independently stored.
- `FIXED`: Pickup performs no inventory movement and does not consume, release or mutate reservations. Reserved parts are consumed only by the existing full-financial-settlement rule.
- `NOT_FIXED`: present all active reservations with reserved, used and calculated released quantities. Used quantity is an integer from zero through the active reserved quantity. Consume only used reserved quantity and release the unused remainder atomically with Pickup. Zero use releases all active reservations. If no active reservations exist, skip stock disposition. Unreserved products/quantities cannot be added to this command.
- Financial balance is irrelevant to Pickup eligibility and stock disposition. Pickup does not settle, waive or refund any amount.
- The stock disposition, movements, reservation updates, ServiceOrder state, immutable event and Note share one transaction. A rollback leaves no partial effects.
- Require fresh employee code/password authorization, explicit confirmation, an idempotency key and concurrency protection. A repeated committed key returns its original result; a concurrent distinct command loses with a conflict. No direct undo/cancel is offered; corrections require a separate auditable stock operation.
