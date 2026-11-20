# Legacy Reconciliation — Service Orders and Stock

**Review date:** 2026-11-20  
**Legacy repository:** [hermanCosta/pchousestoremvn](https://github.com/hermanCosta/pchousestoremvn)  
**Target repository/branch:** [hermanCosta/pchousestore-web — phase2/technical-foundation](https://github.com/hermanCosta/pchousestore-web/tree/phase2/technical-foundation)  
**Status:** Evidence review recorded. This document does not authorize implementation or change approved product decisions.

## 1. Review method and scope

The review inspected the legacy Java model, enum, DAO, controller and Swing view files related to ServiceOrders, product/service catalogue, payments, refunds and Notes. Findings below describe behavior visible in those source files. A missing feature means it was not found in the inspected repository tree and code paths; it is not a claim about undocumented manual business practices.

Key source files:
- [OrderStatus.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/enums/OrderStatus.java)
- [ServiceOrder.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/models/ServiceOrder.java)
- [ServiceOrderProdServ.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/models/ServiceOrderProdServ.java)
- [ServiceOrderPayment.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/models/ServiceOrderPayment.java)
- [OrderNote.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/models/OrderNote.java)
- [ProductService.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/models/ProductService.java)
- [ServiceOrderDAO.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/dao/ServiceOrderDAO.java)
- [OrderProdServDAO.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/dao/OrderProdServDAO.java)
- [ServiceOrderPaymentDAO.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/dao/ServiceOrderPaymentDAO.java)
- [RefundDAO.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/dao/RefundDAO.java)
- [OrderNoteDAO.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/dao/OrderNoteDAO.java)
- [ProductServiceDAO.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/dao/ProductServiceDAO.java)
- [CreatedServiceOrderView.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/views/CreatedServiceOrderView.java)
- [FixedServiceOrderView.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/views/FixedServiceOrderView.java)
- [NotFixedOrderView.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/views/NotFixedOrderView.java)
- [PickedOrderView.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/views/PickedOrderView.java)

## 2. Confirmed legacy behavior

### 2.1 ServiceOrder creation and lifecycle

- The legacy enum contains `CREATED`, `IN_PROGRESS`, `FIXED`, `NOT_FIXED`, `FINISHED`, `PICKED`, `REFUNDED` and `CANCELLED`.
- The creation form constructs a new ServiceOrder with status `IN_PROGRESS`; it does not create it in `CREATED`. The initial order creation also requires a valid employee password.
- The legacy ServiceOrder stores one Customer, one Device, one Employee and one Company reference, along with total, due, created/finished/picked timestamps, integer `badSector` and a free-text note.
- The legacy UI allows transitions from work to `FIXED` or `NOT_FIXED), and allows an undo action from those views back to `IN_PROGRESS`. These actions request an employee password.
- The `NOT_FIXED` view exposes pickup separately. Its pickup method sets employee and picked timestamp and persists the ServiceOrder, but does not explicitly assign `PICKED` in that method. By contrast, the fixed-order payment DAO explicitly changes the order status to `PICKED` when it persists a payment. This is an inconsistency in the legacy implementation, not a target requirement.
- `CANCELLED` and `FINISHED` exist in the enum, but no corresponding ServiceOrder cancellation/finish workflow was identified in the reviewed controller/view paths. Their presence in the enum alone does not establish operational rules.

### 2.2 ServiceOrder lines and edits

- A ServiceOrder line stores a ProductService reference, integer quantity and line total.
- The legacy DAO supports deleting a persisted ServiceOrder product/service line. The general ServiceOrder update path deletes all existing fault and product/service lines and inserts the submitted lists again.
- No lifecycle-based lock preventing line edits after payment, reservation or work start was found in these DAO paths.
- The legacy ServiceOrder model has both a free-text note field and a separate ServiceOrderNote table.

### 2.3 Payments, pickup and refunds

- ServiceOrder payment rows store employee, order, payment type, payment method, amount due/paid, card/cash amounts, change and transaction timestamp.
- The payment DAO persists payment rows and, after persisting the last payment in the submitted batch, sets the ServiceOrder status to `PICKED` and the picked timestamp. This couples payment posting and pickup in the legacy implementation.
- The `PickedOrderView` exposes a refund action. It asks for an employee password, creates a Refund record for the ServiceOrder total, adds a Note and creates refund payment rows based on the original payments. The Refund DAO sets the ServiceOrder status to `REFUNDED`.
- The reviewed refund path does not enforce a Manager/Admin role; it validates an employee by password. The target authorization policy is intentionally stricter.
- The legacy refund path is a full-order refund flow in the inspected view; it does not demonstrate the target's explicit refundable-balance limits, original-tender limits or independent refund event model.

### 2.4 Notes and employee attribution

- `OrderNote` can be linked to either a ServiceOrder or a Sale and stores an Employee reference, note text and timestamp.
- ServiceOrder notes are queried in ascending creation order.
- Several status and payment paths create a Note with the employee and timestamp. In the inspected status/pickup flows, the order update and Note insertion are often separate DAO transactions; therefore the legacy code does not consistently guarantee atomicity between the business action and its Note.
- The ServiceOrder itself also retains a mutable free-text note field.

### 2.5 Product catalogue and stock evidence

- `ProductService` contains `qty`, `minQty`, category, price and company. In the legacy model, `qty` is the manually maintained product quantity; the new target model uses physical on-hand quantity only and does not carry over `minQty`.
- The catalogue screen permits operators to enter/update `qty` and legacy `minQty` directly.
- The DAO's legacy low-stock query filters products where category is `PRODUCT` and `qty <= 2`; it does not use the stored `minQty` field. This fixed-threshold behavior is not carried into the target.
- The reviewed ServiceOrder and Sale line DAOs persist document lines but do not adjust ProductService quantity in those persistence methods.
- No dedicated Stock/Inventory/InventoryMovement/Reservation/StockReceipt class or DAO was found in the repository tree inspected for this review. No explicit stock reservation, release, consumption, receipt, transfer or physical-return workflow was identified in the reviewed code.
- Consequently, the legacy source supports a manually maintained catalogue quantity and a low-stock listing, but does not establish a reliable stock ledger or automatic ServiceOrder part allocation.
- No supplier procurement or supplier tracking workflow was identified in the inspected source paths.

## 3. Reconciliation with approved target behavior

| Area | Confirmed legacy behavior | Approved target behavior |
|---|---|---|
| Initial status | New order starts `IN_PROGRESS` | Booking starts `CREATED`; explicit Start Work moves to `IN_PROGRESS` |
| Parts shortage | No dedicated reservation/shortage lifecycle found | `AWAITING_PARTS`, explicit partial reservation and explicit resume |
| Stock quantity | Product quantity manually maintained in catalogue | Shop-scoped stock ledger, movements and derived availability |
| Reservation | Not found in reviewed legacy code | Explicit employee-authorized reserve/release; receipt never auto-reserves |
| Stock deduction | No automatic deduction found in reviewed ServiceOrder/Sale line persistence | Consume reserved parts atomically when ServiceOrder is fully settled |
| Product/service lines | DAO supports deletion/replacement without a lifecycle lock | Editing locked after first reservation or first payment |
| Fixed order payment | Payment DAO sets status `PICKED` | Financial settlement and physical handover are separate |
| Not-fixed pickup | Pickup timestamp persisted; method does not explicitly set `PICKED` | Explicit `NOT_FIXED -> PICKED`; outstanding balance may remain |
| Refund | Full-order refund action visible from Picked view; sets `REFUNDED` | Refund is a financial event, not a lifecycle status; Manager/Admin only |
| Cancellation | Enum value exists; no operational cancellation path identified | Explicit eligible cancellation with atomic reservation release; no automatic refund |
| Notes | Separate notes table, often written in a separate transaction | Append-only Notes/event traceability committed atomically with successful action |

## 4. Decisions and implementation implications

1. Preserve the target decisions already approved. Legacy behavior is evidence, not an instruction to reproduce defects or coupling.
2. Do not implement automatic stock deductions or reservations based on the legacy catalogue quantity. The reviewed code does not provide a dependable stock ledger to migrate.
3. The target has no `min_qty`, minimum-stock threshold or reorder threshold. `qty` maps only to physical on-hand quantity (`quantity_on_hand`) and may be imported only after physical stock reconciliation and a separately approved migration policy.
4. Do not migrate legacy `minQty` or its hard-coded `qty <= 2` low-stock query into the target.
5. Retain the target distinction between payment settlement, pickup, refund and cancellation. The legacy coupling is explicitly superseded.
6. Keep Parts to Buy as the approved visibility list, not a supplier/procurement module.
7. The target stock ledger, reservation lifecycle and ServiceOrder state machine are redesign capabilities; they are not confirmed legacy features.

## 5. Review limitations and next validation

- This review inspected the current `main` branch source tree and the files listed above. It did not inspect a production database, historical backups, operator procedures or external supplier records.
- Before data migration, inspect a representative legacy database to establish the meaning and quality of existing `PROD_SERV.QTY`, ServiceOrder statuses, payment rows, deposits and notes.
- Before implementation approval, reconcile the remaining shared database model and feature specifications against this evidence. No physical DDL, stock migration strategy or implementation is approved by this report.
