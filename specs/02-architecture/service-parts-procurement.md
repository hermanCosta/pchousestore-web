# Service Parts Procurement Workflow

**Status:** DRAFT — owner requirement recorded 2026-11-05. See ADR-013 and ADR-014.  
**Scope:** Identify, queue, source, order, track, receive and reserve electronic parts required to complete a service order. This is an operational procurement workflow, not a supplier accounts-payable module.

## 1. Business objective

A device may be booked and diagnosed before the required replacement part is available. The Shop needs a controlled way to record every part that must be purchased, attach photos to identify the exact replacement, and manage those needs in a dedicated purchasing screen. A single ServiceOrder may reveal different parts at different moments, so it may have multiple independent procurement requests over time.

The workflow also records the external supplier/order reference, purchase link, expected delivery date and tracking link, supports partial deliveries, and resumes repair only when blocking requirements are reserved.

Examples include a replacement laptop battery, keyboard, screen, console component or TV power board.

## 2. Core concepts

| Concept | Purpose |
|---|---|
| `ServicePartRequirement` | Durable total demand for a product and whole-unit quantity on a specific ServiceOrder line; indicates whether the missing part blocks repair work. |
| `ServiceProcurementRequest` | A Shop queue ticket for one ServiceOrder and one sourcing round. A ServiceOrder may have several requests created at different moments. |
| `ServiceProcurementRequestLine` | The quantity of a requirement that must be sourced in this round, with part-number and compatibility notes. |
| `ServiceProcurementRequestImage` | Private reference photos attached to an individual request line to help the buyer identify the correct part. |
| `ServiceProcurementRequestEvent` | Append-only history of request creation, edits, assignments, priority changes, image actions and sourcing-round cancellation. |
| `Supplier` | Reusable company-level list of retailers, wholesalers and online marketplaces. |
| `SupplierPurchase` | A Shop-scoped external purchase/order, with order reference, source, purchase link, ETA and shipment tracking. |
| `SupplierPurchaseLine` | Product and quantity ordered/received under an external purchase. |
| `ServicePartProcurementAllocation` | Allocates a purchase-line quantity to one or more service procurement request lines. |
| `SupplierPurchaseEvent` | Append-only timeline of order, ETA, tracking, status and receipt updates. |

`ServicePartRequirement` is the durable technical instruction to source and reserve a physical part. It is not itself a purchase attempt. `ServiceProcurementRequest` represents one operational round of sourcing. A request line references the requirement and states how much is being sourced in that round. A product `ServiceOrderItem` may have one requirement; service catalogue lines may not.

## 3. Service order state

Add the target status `AWAITING_PARTS` to the ServiceOrder state machine.

| Current status | Event/action | Next status | Preconditions |
|---|---|---|---|
| `CREATED` or `IN_PROGRESS` | Booking/diagnosis identifies a blocking part that is not fully reserved | `AWAITING_PARTS` | At least one active `ServicePartRequirement` has a reserved quantity below its required quantity. This may happen before repair work starts. |
| `AWAITING_PARTS` | All blocking requirements are fully reserved; employee resumes repair | `IN_PROGRESS` | Backend rechecks reservation quantities and consumes a fresh employee authorization. |
| `IN_PROGRESS` | Repair completed | `FIXED` | Existing transition; parts remain reserved. |
| `FIXED` | Full customer settlement | Remains `FIXED` until collected | All amounts due are settled and reserved parts are consumed atomically; physical handover is recorded separately as `PICKED`. |
| `AWAITING_PARTS` | Service is cancelled | `CANCELLED` | Active reservations are released. Received purchased goods remain in Shop stock unless supplier return is separately implemented. |

`AWAITING_PARTS` is not a synonym for a procurement-request or supplier-purchase status. A purchase can be `ORDERED` while the ServiceOrder is `AWAITING_PARTS`; a ServiceOrder can have several request rounds, multiple purchases may support one order, and one purchase can supply several service orders.

The `blocks_work` flag supports cases where a part is required but work can continue on other tasks. Only unresolved blocking requirements force `AWAITING_PARTS`. Non-blocking requirements remain visible in the queue without stopping repair; however, all product parts required to complete the repair must be reserved before the order can transition to `FIXED`.

## 4. End-to-end workflow

### 4.1 Identify the required part

1. At booking or diagnosis, an employee identifies the missing part and adds it as a `PRODUCT` ServiceOrderItem. The requirement can be recorded while the order is still `CREATED`, before repair work starts, or later while it is `IN_PROGRESS`.
2. Create a `ServicePartRequirement` with the positive integer quantity, `blocks_work` (default `true`) and optional needed-by date.
3. Check `ShopInventory.available_quantity` in the active Shop and reserve any available quantity.
4. If a shortage remains, create a procurement request for the quantity still to be sourced. If the requirement blocks work and is not fully reserved, set the ServiceOrder to `AWAITING_PARTS` from `CREATED` or `IN_PROGRESS`; do not require repair to have started first.
5. If the part is already represented by an existing open requirement, update that requirement through an audited command rather than creating a duplicate requirement. A new purchasing round is still allowed when additional sourcing work is genuinely required.

### 4.2 Create a procurement request and attach images

1. Create a `ServiceProcurementRequest` for the ServiceOrder. The request is one sourcing round and may include several part lines.
2. Add a `ServiceProcurementRequestLine` for each shortage. It references the durable `ServicePartRequirement` and records the whole-unit quantity to source in this round.
3. Add the supplier/manufacturer part number, compatibility notes, model information and any details useful to distinguish visually similar replacements.
4. Capture or upload one or more reference images for each request line. Useful examples are the old/removed part, printed model or serial label, connector, dimensions, mounting points or visible damage.
5. Requests can be raised at any point in the service lifecycle. If another component is discovered later, create a new request number for the same ServiceOrder; do not overwrite the earlier request or image set.
6. Request numbers are sequential within the ServiceOrder (for example, `SO-1042 / PR-02`).

### 4.3 Manage the Shop-wide Service Parts Purchase Queue

1. Buyers open the dedicated **Service Parts Purchase Queue** to review outstanding sourcing requests across service orders in the active Shop.
2. The queue supports filters for request status, ServiceOrder ID, customer/device, part/product, priority, requested date, needed-by date and assigned buyer. Supplier and supplier-purchase status filters are available after ordering.
3. Each row shows request number, ServiceOrder ID, customer/device summary, part thumbnail, description/SKU, requested/reserved/ordered/received/outstanding quantities, priority, needed-by date, request status and assignee.
4. Opening a request shows the linked ServiceOrder, all request lines, image gallery, part number and compatibility notes, stock/reservation information, linked SupplierPurchases and activity history.
5. A request can be created from the ServiceOrder screen or from the queue by searching for and selecting a ServiceOrder ID. The backend derives Shop from ShopSession and validates that the ServiceOrder belongs to that Shop.
6. The buyer may assign the request, change priority, add notes, attach further images or link it to a new/existing supplier purchase. Each mutation requires employee authorization and appends a `ServiceProcurementRequestEvent`.
7. The request header status is derived from its lines and allocations. It is not manually set to `FULFILLED` while any active line is still outstanding.

See [Service Parts Purchase Queue — Screen Specification](service-procurement-queue.md) for the detailed screen contract, filters, columns, actions, image UX, permissions and acceptance criteria.

### 4.4 Create or reuse a supplier purchase

1. Select an existing `Supplier` or enter a one-off source name. Supplier is optional to support a retailer or marketplace that has not yet been added to the directory.
2. Create a `SupplierPurchase` for the active Shop. Record the external order reference, purchase/order URL, ordered date, expected delivery date, tracking reference/URL and notes when available.
3. Add one or more `SupplierPurchaseLine` rows for product and whole-unit quantity ordered. Store the supplier product-page URL and unit-cost snapshot where available.
4. Allocate ordered quantities to one or more `ServiceProcurementRequestLine` records through `ServicePartProcurementAllocation`. A purchase can be consolidated across service jobs; a request line can be split across several purchases.
5. The request line links to its durable `ServicePartRequirement`, so the allocation still contributes to the required quantity for the correct service job.
6. Any quantity ordered but not allocated to a service request is a general Shop replenishment quantity and will remain available after receipt.

The order URL and the product-page URL have different purposes: the first opens the retailer's purchase/order details; the second identifies the item listing. Shipment tracking has its own tracking reference and URL.

### 4.5 Track order and delivery

The ServiceOrder screen and the purchase queue show:

- required part, SKU/description and required/reserved quantity;
- reference photos and compatibility notes;
- supplier/source and external order reference;
- product page and purchase/order links;
- order date and current expected delivery date;
- tracking reference and tracking link;
- purchase status and latest timeline events;
- quantity ordered, accepted/received, allocated and still outstanding.

An employee may update expected delivery date, tracking reference/URL, purchase status or notes. Each change creates a `SupplierPurchaseEvent` with employee and action authorization. Previous ETA and tracking values remain in history. If the ETA passes and the purchase is not fully received or cancelled, the UI displays an `OVERDUE` indicator derived from the current date; it does not automatically cancel the purchase or alter the ServiceOrder.

The initial release uses manual entry and links out to the supplier's website. It does not scrape supplier websites, authenticate to vendor accounts or call marketplace APIs.

### 4.6 Receive all or part of the delivery

1. The employee opens the purchase and records the actual accepted quantity for each delivered line. Partial receipt is supported.
2. The backend validates that cumulative accepted quantity does not exceed the ordered quantity. Damaged or rejected items are not included in accepted/stocked quantity; the reason is captured in the receipt event.
3. For every accepted unit, create a `PURCHASE_RECEIPT_IN` InventoryMovement and increase the destination ShopInventory balance.
4. For accepted units allocated to active request lines, automatically create/extend InventoryReservationLine rows for the corresponding ServiceOrderItem through the linked ServicePartRequirement. The receipt movement and reservation are committed atomically.
5. Accepted units that are unallocated, or whose requirement was cancelled before receipt, remain available Shop stock.
6. A partial delivery sets the purchase to `PARTIALLY_RECEIVED`; only when all ordered quantities have been accounted for does the purchase become `RECEIVED`. Short deliveries remain outstanding and can be followed up with the supplier. The queue recalculates request-line/header status from accepted allocations.

The receipt command is idempotent. Retrying the same receipt request must not double the ShopInventory balance or reservation counter.

### 4.7 Resume repair

When all active `blocks_work = true` requirements are fully reserved, the ServiceOrder becomes eligible to resume. The employee explicitly selects **Resume repair** and authenticates. The server revalidates all requirements and reservations, records a `ServiceOrderEvent` from `AWAITING_PARTS` to `IN_PROGRESS`, and returns the order to the work queue.

If the part is later found defective or an additional part is required, create/update the corresponding requirement and, when sourcing is needed, create another request round. Return the ServiceOrder to `AWAITING_PARTS` where a missing requirement blocks work.

After repair, `FIXED` retains reservations. When settled payments and explicitly applied deposits reduce the service balance to zero, the system consumes the reserved parts atomically. The later `PICKED` event records physical handover and does not trigger a second stock deduction. A deposit for the purchase of a part is a separate `ServiceDeposit` with purpose `PARTS_PREORDER`; it is not itself a supplier purchase or a stock movement.

## 5. Data rules and invariants

1. Only `PRODUCT` catalogue items can be service-part requirements or supplier purchase lines. `SERVICE` catalogue items cannot be received into stock.
2. Product quantities are positive whole units. Fractional quantities are invalid.
3. A ServicePartRequirement belongs to one ServiceOrder and one product ServiceOrderItem in the same Shop. A product line cannot have duplicate active requirements.
4. A ServiceOrder can have multiple ServiceProcurementRequests over time. Each request belongs to exactly one ServiceOrder and Shop, and contains one or more request lines. Its request number is unique within the ServiceOrder.
5. Each request line references a requirement in the same ServiceOrder/Shop. Its quantity is a positive whole number and represents the quantity to source in that procurement round, not the total required quantity.
6. Multiple request rounds may be created as additional parts are identified. Active request quantities and outstanding purchase allocations must be reconciled against the requirement's remaining shortage so the same missing unit is not requested twice.
7. Request-line images are private, Shop-scoped files. MySQL stores metadata and a private storage key only. Request changes and image operations append a `ServiceProcurementRequestEvent` in the same transaction. Validate file content/MIME, size and hash; do not store permanent public URLs, raw filesystem paths or secrets.
8. A SupplierPurchase belongs to one Shop. Its lines and all allocations must remain in that Shop. Supplier master data is shared at Company level.
9. `quantity_received` is the cumulative accepted quantity, not an ETA estimate or a supplier's shipment claim. It cannot exceed `quantity_ordered`.
10. Total allocation on a SupplierPurchaseLine cannot exceed the ordered quantity. Total allocation to a request line cannot exceed its requested quantity.
11. The sum of received allocation quantities cannot exceed the purchase-line accepted/received quantity. Only accepted physical receipts generate inventory movements.
12. Received units allocated to a request line must be reserved for the same ServiceOrder in the same database transaction as the receipt movement. If the request/requirement has been cancelled, received stock remains unallocated and available.
13. A ServiceOrder cannot resume from `AWAITING_PARTS` while any active blocking requirement remains short. The server, not the browser, evaluates this invariant. A ServiceOrder cannot transition to `FIXED` while any required-to-complete part requirement remains short.
14. Changes to request details, images, assignment and priority append a `ServiceProcurementRequestEvent`; ETA, links, supplier order status and receipt quantities append a `SupplierPurchaseEvent`. All require employee authorization. The persistent ShopSession still contains no employee identity.
15. Supplier purchase receipt, inventory movement, reservation allocation and event insertion are atomic and idempotent.
16. Cancelling a request line releases its unconsumed allocation from the request; it does not cancel the underlying ServicePartRequirement unless the requirement or ServiceOrder itself is cancelled. SupplierPurchase and order history are never deleted. Unreceived quantities may be reallocated/cancelled through an audited operation.
17. A received purchase item is not automatically returned to the supplier when a ServiceOrder is cancelled. Supplier returns are a separate future workflow.
18. SupplierPurchase captures operational sourcing and inventory receipt. It does not create a supplier payable or represent payment to the retailer.

## 6. Status vocabularies

### Service part requirement

- `OPEN`: not all required quantity is reserved.
- `PARTIALLY_RESERVED`: some, but not all, required quantity is reserved.
- `RESERVED`: full required quantity is reserved.
- `CANCELLED`: no longer required; active reservation is released.

Requirement status is maintained transactionally from active reservation quantities; it is not inferred from procurement request or SupplierPurchase status.

### Service procurement request

- `DRAFT`: request is being prepared and is not yet on the shared purchase queue.
- `OPEN`: request is available for buyer processing; at least one active line has sourcing quantity not yet linked to a supplier order.
- `IN_PROGRESS`: a buyer is actively reviewing images, compatibility, quotes or suppliers.
- `PARTIALLY_ORDERED`: some but not all requested quantities have been linked to supplier purchase lines.
- `ORDERED`: all active requested quantities have been linked to supplier purchase lines; no accepted quantity has yet been received.
- `PARTIALLY_RECEIVED`: at least one accepted unit is received, while requested quantities remain outstanding.
- `FULFILLED`: all active request-line quantities have been received and allocated/reserved for their service requirements.
- `CANCELLED`: the request is cancelled; underlying service requirements remain active unless separately cancelled.

The request header status is an aggregate of request-line sourcing/receipt states and explicit draft/assignment/cancellation actions. It is not the status of the ServiceOrder or SupplierPurchase. Exact derivation rules are part of the command/domain implementation and must be tested for partial orders and cancellations.

Each `ServiceProcurementRequestLine` has a domain-maintained status:

- `OPEN`: no supplier purchase quantity has been allocated yet.
- `PARTIALLY_ORDERED`: some, but not all, requested quantity has been allocated to supplier purchase lines.
- `ORDERED`: all requested quantity is allocated to supplier purchase lines and no accepted receipt is recorded.
- `PARTIALLY_RECEIVED`: at least one allocated unit has been accepted, while requested quantity remains outstanding.
- `FULFILLED`: all requested quantity has been accepted and reserved for the linked service requirement.
- `CANCELLED`: this request line was cancelled; the underlying requirement remains unless separately cancelled.

Line status is updated only by domain commands for allocation, receipt and cancellation; the browser cannot arbitrarily set it.

### Supplier purchase

- `DRAFT`: being prepared; no order has been placed.
- `ORDERED`: external order placed; no accepted quantity received yet.
- `PARTIALLY_RECEIVED`: one or more lines have accepted receipts, but some ordered quantity remains outstanding.
- `RECEIVED`: all ordered quantities have been accepted into stock.
- `CLOSED_SHORT`: received quantities are retained and the unreceived balance is explicitly closed by an authorized action.
- `CANCELLED`: supplier purchase cancelled before any accepted receipt. After partial receipt, the purchase must use `CLOSED_SHORT` for the remaining balance; historical receipts remain.

`OVERDUE` is a derived indicator, not a persisted status.

## 7. Out of scope for the first release

- Supplier accounts payable, supplier invoice approval and settlement.
- Supplier-return/RMA workflow and supplier credit notes.
- Automated price or stock scraping from retailer URLs, marketplace APIs and email integration.
- Multiple warehouses/shelves inside a Shop.
- Automatic scheduling/notification of ETA delays. The data and UI timeline support a later notification feature.

## 8. Acceptance criteria

1. Create/book a service job, identify a required battery before repair starts, and find no available stock: the order enters `AWAITING_PARTS` from `CREATED`, and the parts panel shows the requirement.
2. Create a procurement request for that ServiceOrder, attach multiple reference images and publish it to the Shop purchase queue.
3. Create a second request for the same ServiceOrder later (for example, after discovering a keyboard issue); both requests remain separately searchable by ServiceOrder ID with independent images and history.
4. Create a request from the ServiceOrder screen and from the queue by ServiceOrder ID; both paths enforce Shop scope.
5. A buyer searches the queue by ServiceOrder ID, filters by priority/status/needed-by date and opens the linked ServiceOrder.
6. Record an online supplier and the purchase link, order reference, order date, expected delivery date and tracking link.
7. Update ETA twice; the timeline shows both dates and the employee responsible.
8. Receive only one of two ordered batteries: exactly one unit is stocked and reserved for the service order; the order remains `AWAITING_PARTS`.
9. Receive the second battery: inventory and reservations update once; the order becomes eligible to resume.
10. One purchase covers parts for two ServiceOrders; received allocations are reserved for the correct orders, with extra stock remaining available.
11. An accepted receipt quantity greater than the ordered quantity, allocation greater than request-line quantity, or duplicate allocation of the same requirement shortage is rejected.
12. A user from another Shop cannot read the request, images or purchase history by guessing an ID/storage key.
13. Duplicate receipt command is idempotent and creates no duplicate stock movement.
14. Cancelling a procurement request closes only that sourcing round; it does not cancel the requirement or other requests. A later receipt for a cancelled allocation stays in general Shop stock unless explicitly reallocated.
