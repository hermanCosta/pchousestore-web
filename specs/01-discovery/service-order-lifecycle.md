# Service Order Lifecycle — Discovery

**Status:** DISCOVERY — full state-transition matrix pending.

## Current legacy enum

Source: `enums/OrderStatus.java`.

- CREATED
- IN_PROGRESS
- FIXED
- NOT_FIXED
- FINISHED
- PICKED
- REFUNDED
- CANCELLED

## Evidence from inspected views and controllers

- `NewServiceOrderView` collects customer, device, fault, product/service, note and bad-sector data.
- `ServiceOrderController.addServiceOrder` accepts the order and its related faults, line items, payment rows, deposit and note.
- `CreatedServiceOrderView` has an `updateOrderStatus` flow that confirms the action, re-identifies an employee, updates the order, creates an `OrderNote` and opens a view associated with the new status.
- `FixedServiceOrderView.payServiceOrder` requests operator authorization and payment for the amount due.
- `ServiceOrderPaymentDAO.addOrderPaymentDAO` persists payment rows, changes status to `PICKED`, sets the picked timestamp and persists a note in the same transaction.
- `PickedOrderView` supports refunding the service order.
- `ServiceOrderController` exposes transfer functionality.

## State transitions to document

`AWAITING_PARTS` is a target redesign state introduced by ADR-013; it is not present in the legacy `OrderStatus` enum.

| Current state | Action | Resulting state | Evidence/status |
|---|---|---|---|
| New order | Create service order | IN_PROGRESS (observed in a construction path) | CONFIRMED, validate all creation paths |
| Created / In progress | Booking or diagnosis identifies a blocking part that is unavailable or not fully reserved | AWAITING_PARTS | TARGET REDESIGN — ADR-013; may occur before repair starts |
| Awaiting parts | All blocking part requirements fully reserved; resume repair | IN_PROGRESS | TARGET REDESIGN — employee-authorized command |
| In progress | Mark fixed | FIXED | View transition method exists; confirm action/role |
| In progress | Mark not fixed | NOT_FIXED | View transition method exists; confirm action/role |
| Fixed | Receive payment | PICKED | CONFIRMED in payment DAO |
| Picked | Refund | REFUNDED | CONFIRMED in `PickedOrderView` and `RefundDAO` |
| Other | Transfer | Unknown | OPEN: destination and status semantics |
| Any eligible state | Cancel | CANCELLED | OPEN: source action and permissions |
| Fixed | Finished | FINISHED | OPEN: meaning and actor |

Do not implement this table as a final state machine until the remaining screens and all button handlers have been examined.

## Supplier parts procurement workflow (target redesign — ADR-013 / ADR-014)

- A product part required for repair is represented by a `ServiceOrderItem` plus `ServicePartRequirement`. Only product catalogue entries can be requested as stock parts; quantities are whole units.
- Reserve any quantity already available in the active Shop. If a blocking requirement remains short, move the ServiceOrder to `AWAITING_PARTS` from `CREATED` (before repair starts) or `IN_PROGRESS`.
- Create one or more `ServiceProcurementRequest` rounds for the ServiceOrder and manage them in the Shop-wide purchase queue. Attach reference images to each request line. Track external purchases with supplier/source, purchase URL/reference, product URL, ordered date, ETA, tracking reference/URL, status and timeline events. A purchase may cover requests from several orders and one request line may be split across multiple purchases.
- Record partial or full receipt as actual accepted quantity. Add only received stock and reserve any quantity allocated to a service requirement; excess is general Shop stock.
- When all blocking requirements are fully reserved, an employee explicitly resumes the order to `IN_PROGRESS`. Do not auto-resume merely because a parcel was marked delivered.
- Once repair is completed, `FIXED` retains the reservations. Full settlement consumes the reserved stock while the order remains `FIXED`; a later `PICKED` event records collection only, per ADR-006.

See [Service Parts Procurement Workflow](../02-architecture/service-parts-procurement.md), [Service Parts Purchase Queue](../02-architecture/service-procurement-queue.md) and [lifecycle diagram](../02-architecture/service-parts-procurement-lifecycle.mmd).

## Target stock rules recorded by product owner (redesign)

- Product parts may be explicitly reserved against the service order.
- Transition to `FIXED` does not deduct stock; active reservations remain held while the device awaits payment/pickup.
- When settled payments and explicitly applied deposits reduce the order balance to zero, product quantities and reservations are consumed in one atomic transaction. `PICKED` records physical collection separately; it does not trigger stock deduction. Partial payments and unapplied deposits do not consume stock.
- Cancellation before consumption releases reservations without an inbound stock movement.
- If the order later becomes `REFUNDED` or `CANCELLED` after stock was consumed, the operator may select exact product lines/quantities physically returned. Only those selected lines are restocked.
- Service catalogue lines never reserve or move stock.

The full stock lifecycle and remaining assumptions are documented in [Stock, Reservations, Transfers and Refurbished Equipment](../02-architecture/stock-and-refurbishment.md). Supplier ordering, ETA tracking and receipt are documented in [Service Parts Procurement Workflow](../02-architecture/service-parts-procurement.md).

## Target command requirements

- Validate transition against current persisted state, not stale UI state.
- Perform transition, required notes and audit events in one transaction.
- Record actor and timestamp.
- Enforce authorization per transition.
- Make payment/refund effects idempotent or protected against duplicate submission.
- Prevent invalid repeated transitions under concurrent requests.
- Keep status history, not just the latest status, if audit needs are confirmed.
