# SO-01 — Service Orders

**Status:** PRODUCT DECISIONS COMPLETE — TECHNICAL RECONCILIATION IN PROGRESS (2026-11-21). No implementation is authorized. Pickup HTTP behavior, idempotency/concurrency persistence and regression coverage are specified. Current domain/database models reflect the `EMPLOYEE` / `MANAGER` / `ADMIN` profiles and direct per-operation employee credential validation; older ADRs and authentication-persistence material are explicitly marked as historical or superseded. Remaining implementation and database-migration work is not authorized by this SPEC status.  
**Type:** Shop-scoped operational workflow.  
**Depends on:** AUTH-01/AUTH-02, CAT-01, DATA-02, [Employee Management](employee-management.md), stock and financial policies.  
**Related work:** Stock and Refurbishment (inventory and explicit reservations), FIN-01 (payments/deposits/refunds), SO-XFER-SPEC (cross-Shop transfer), REP-01 (documents and receipts).

## 1. Objective and scope

Define the end-to-end Service Order workflow used to receive a customer's device, record the reported faults, diagnose and perform work, identify required Product parts, track technical progress, record service notes, and hand the device back to the customer.

A ServiceOrder is an operational record owned by one Shop. Customers and catalogue definitions remain Company-shared master data. The active Shop is always derived from the authenticated ShopSession.

This SPEC is limited to the Service Order workflow and its integration points. It does not redefine the financial ledger, stock ledger, cross-Shop transfer or document-generation modules. The former supplier-procurement queue is superseded; Parts to Buy is a visibility list only.

## 2. Legacy references

| Type | Source path | Class / method | Evidence |
|---|---|---|---|
| Enum | src/main/java/com/pchouse/pchousestoremvn/enums/OrderStatus.java | OrderStatus | Legacy values: CREATED, IN_PROGRESS, FIXED, NOT_FIXED, FINISHED, PICKED, REFUNDED, CANCELLED. |
| Swing view | .../views/NewServiceOrderView.java | getOrderFields, getOrderFault, getOrderProdServ, btn_save_orderActionPerformed | Booking form, required-field checks, customer/device selection, faults, Product/Service lines, optional deposit and creation note. |
| Swing view | .../views/CreatedServiceOrderView.java | updateOrderStatus, btn_save_orderActionPerformed, btn_fixActionPerformed, btn_not_fixActionPerformed, btn_depositActionPerformed, btn_transfer_orderActionPerformed | Edit existing order, add notes/deposits, mark Fixed/Not Fixed, open transfer workflow. |
| Swing view | .../views/FixedServiceOrderView.java | updateOrderStatus, payServiceOrder, btn_undo_fixed1ActionPerformed | Revert to In Progress, accept payment and open receipt flow. |
| Swing view | .../views/NotFixedOrderView.java | updateOrderStatus, pickServiceOrder, btn_pick_upActionPerformed, btn_undo_fixedActionPerformed | Revert to In Progress or record physical pickup. |
| Swing view | .../views/PickedOrderView.java | refundServiceOrder | Legacy full-refund entry point. |
| Controller | .../controllers/ServiceOrderController.java | getServiceOrderById, getAllOrders, searchOrder, addServiceOrder, updateServiceOrder, updateServiceOrderStatus, transferServiceOrderController | Read/search/create/update/status/transfer operations. |
| DAO | .../dao/ServiceOrderDAO.java | getAllOrderDAO, searchOrderDAO, addServiceOrderDAO, updateServiceOrderDAO, updateServiceOrderStatusDAO, transferServiceOrderDAO | Company-scoped legacy reads and transaction boundaries. |
| DAO | .../dao/ServiceOrderPaymentDAO.java | addOrderPaymentDAO | Payment persistence and legacy status side effect. |
| Models | .../models/ServiceOrder.java, Device.java, ServiceOrderFault.java, ServiceOrderProdServ.java, OrderNote.java, Deposit.java | Entity fields and relationships | Legacy order, device, fault, line, note and deposit structures. |

The legacy repository is https://github.com/hermanCosta/pchousestoremvn, branch main. The full package prefix for the abbreviated paths above is src/main/java/com/pchouse/pchousestoremvn/.

## 3. Existing behavior (CONFIRMED)

### 3.1 Booking and editing

- The inspected booking form requires first name, last name, contact number, device brand, device model, serial number, at least one Product/Service line and at least one Fault.
- The form can populate an existing customer or construct a new Person/Customer record.
- The form requests an employee password before creating the order. This is legacy behavior only. In the target, every protected command requires a fresh employee code + password challenge; the backend resolves and validates the employee directly. No EmployeeActionAuthorization token, entity or endpoint is used.
- The legacy order is initially created with status IN_PROGRESS, a creation timestamp, total, due amount, bad-sector integer and free-text note. The product owner has selected CREATED as the initial status for the target system.
- Device records contain brand, model and serial number. The legacy booking UI marks the brand, model and serial-number fields as required; it searches for an existing Device by serial number and reuses it when found. No serial-number uniqueness constraint was confirmed by this source inspection.
- The order can contain multiple Fault references and multiple Product/Service lines. Legacy line quantity is an integer and the persisted line stores a total amount.
- An optional initial deposit opens a payment dialog and creates a Deposit record alongside the order. The order, faults, Product/Service lines, deposit, payment rows and creation note are persisted through one DAO transaction.
- The existing-order screen permits edits to customer/device details, faults and Product/Service lines, plus an optional deposit and an update note.
- The DAO's edit implementation deletes and recreates the existing fault and Product/Service child rows. This is not an acceptable target history strategy; the web implementation must follow the target model's audit and immutable-history requirements.

### 3.2 Search and service actions

- Legacy order search matches customer first/last name, contact number, email, device brand/model, serial number and ServiceOrder ID.
- The Created/In Progress view exposes Fixed, Not Fixed, Notes, Deposit, Print and Transfer actions.
- The Fixed view exposes payment, deposit, notes and an action to return the order to In Progress.
- The Not Fixed view exposes notes, deposit, pickup and an action to return the order to In Progress.
- The Picked view exposes payment history, notes, deposits and refund.
- Legacy transfer changes the Company association through transferServiceOrderDAO; the target cross-Shop transfer is handled by the separate SO-XFER-SPEC and must not copy this implementation.

### 3.3 Legacy inconsistencies that must not be copied

- ServiceOrderPaymentDAO.addOrderPaymentDAO sets the order status to PICKED and sets the picked timestamp when a payment is persisted. Payment completion and physical customer handover are different business events in the approved target model.
- NotFixedOrderView.pickServiceOrder sets the picked timestamp without changing the status. The legacy representation of pickup is therefore inconsistent across screens.
- Some legacy status-update paths set the finished timestamp while moving between work statuses. The target must use explicit event timestamps and must not treat every status change as completion.
- The legacy status and note writes are not consistently committed in one transaction. The target must persist the state change, event and required note atomically.
- CREATED, FINISHED and CANCELLED are present in the enum, but their complete active UI transitions were not established in the inspected screens.

### 3.4 Bad-sector field — legacy evidence and owner clarification

- The legacy UI labels the field `Bad Sectors`, uses an integer spinner and shows a required-field marker.
- The product owner clarified that this field represents the count of defective/bad sectors on the device HDD. It is relevant only to certain services, with operating-system installation given as an example; it must be required when one of those services is selected, not treated as universally mandatory.
- The target requirement is conditional. The exact catalogue services that trigger the requirement, how that requirement is configured, and the accepted numeric range remain open decisions.

## 4. Approved target business rules

| ID | Rule | Source | Status |
|---|---|---|---|
| BR-01 | Each ServiceOrder belongs to exactly one Shop; derive ShopId from the active ShopSession, never from browser-supplied ownership. | domain-model.md; database-model.md | DECIDED |
| BR-02 | The customer and catalogue definitions are Company-shared; all ServiceOrder writes must verify that the referenced customer/catalogue records belong to the Company that owns the active Shop. | product-owner-decisions.md; CAT-01 | DECIDED |
| BR-03 | Each employee-protected command requires the acting employee's own fresh employee-code/password challenge. The backend validates active status, Company membership and action permission at the start of command processing. Do not cache/reuse employee authentication or issue/consume an EmployeeActionAuthorization token, entity or endpoint. Once authorized, an in-flight operation may complete if the employee is subsequently deactivated. | employee-management.md | DECIDED |
| BR-04 | `EMPLOYEE`, `MANAGER` and `ADMIN` may perform ServiceOrder Pickup from either `FIXED` or `NOT_FIXED`. `EMPLOYEE` may perform the operational ServiceOrder actions defined here; `MANAGER` has the full ServiceOrder access, including cancellation and financial actions assigned to managers by FIN-01; `ADMIN` inherits the full `MANAGER` permission set. The retired `TECHNICIAN` profile is not used. | employee-management.md; Product-owner decision 2026-11-20 | DECIDED |
| BR-05 | A part requirement is linked to a Product ServiceOrderItem and uses a positive whole-unit quantity. SERVICE catalogue lines never create stock requirements. | stock-and-refurbishment.md; ADR-013 | DECIDED |
| BR-06 | If any blocking part requirement is not fully reserved, the ServiceOrder enters AWAITING_PARTS. It can originate during booking/diagnosis or later work. | ADR-013; domain-model.md | DECIDED |
| BR-07 | An order in `AWAITING_PARTS` may return to `IN_PROGRESS` only when every blocking part requirement is fully reserved. The transition requires an explicit resume command with a fresh employee code/password challenge; receipt or reservation alone never restarts repair. Record the server-resolved employee in the immutable event and `Notes`. | Product-owner decision 2026-11-20; ADR-013 | DECIDED |
| BR-08 | FIXED requires all blocking requirements to be fully reserved and retains active reservations while the repaired device awaits payment/collection. | ADR-006; stock-and-refurbishment.md | DECIDED |
| BR-09 | For a `FIXED` ServiceOrder, settled payments and explicitly applied deposits reducing the balance to zero consume reserved Product parts atomically; partial payments and unapplied deposits do not. For a `NOT_FIXED` ServiceOrder, full financial settlement does not consume or release reservations. At physical handover, the employee explicitly records actual used quantities from active reserved requirements; the system consumes those quantities and releases all unused reserved quantities atomically with handover, whether or not a balance remains due. If no parts were used, all active reservations are released. Only quantities covered by active reservations may be recorded as used through this action. | Product-owner decision 2026-11-20; stock-and-refurbishment.md | DECIDED |
| BR-10 | Physical pickup is a separate event from payment completion and stock consumption. PICKED must not be set merely because a payment was recorded. | ADR-006; product-owner-decisions.md | DECIDED |
| BR-11 | Financial deposits, applications, refunds and forfeitures follow FIN-01/financial-model.md. This SPEC must not introduce a parallel balance or deposit ledger. | financial-model.md | DECIDED |
| BR-12 | Financial refund and physical stock return are separate. Only selected, physically returned quantities may enter the stock-return workflow; inspection/disposition rules apply. | architecture-decisions.md; stock-and-refurbishment.md | DECIDED |
| BR-13 | The Shop has a simple, Shop-scoped Parts to Buy list with two visibly distinct sources: automatically calculated uncovered Product-part requirements from ServiceOrders and manually added reminders not planned on an order. Label each row/group as `ServiceOrder demand` or `Manual entry`; never merge manual quantities into calculated demand totals. Sort both sections by creation date descending (newest first): manual entries by their creation timestamp; ServiceOrder demand groups by the newest creation timestamp among their contributing ServiceOrders, with the contributing orders also listed newest first. Provide one search that searches Product names in ServiceOrder demand and Product-backed manual entries, and searches free-text descriptions in manual entries; update results automatically as the user types, without requiring Enter or a separate submit action. Search is case-insensitive and supports partial matches within Product names and free-text descriptions (for example, `mem` matches `Memória RAM`). When a search returns no matches, display the empty-state message `No parts found`. If both sources contain no items, display `No parts to buy yet` and keep the manual-entry action available. If only one source is empty, retain its section and show a concise `No items` message within that section. Keep results source-labeled and quantities separate. Automatic demand is grouped by Product and retains originating ServiceOrders and per-order quantities. Manual entries may select an existing Product or describe a free-text part, default quantity `1` (editable positive whole number), and use Product-catalogue URLs or entry-owned URLs as applicable. Manual entries may be duplicated, including entries referencing the same Product or using the same free-text description. Each manual entry remains independent, with its own quantity and reference URLs; do not automatically merge, group or deduplicate manual entries. A manual entry remains in the list until the operator explicitly deletes it, regardless of current stock availability or ServiceOrder reservations. Stock changes affect only calculated ServiceOrder demand, never manual reminders. Manual entries can be edited and permanently deleted. Quantity can be changed directly in the normal edit flow to any positive whole number, without a separate justification or additional confirmation; deletion is a separate explicit action. Trim leading/trailing whitespace from free-text descriptions and URL values; ignore blank URL rows rather than saving empty references. Do not create an edit-audit entry when the submitted edit makes no effective change. Deleting affects only the selected manual entry and its own URLs, never ServiceOrder requirements, events or Notes. Links are informational only. An unavailable or unreachable reference URL must not hide or disable its associated part entry; the user can edit or remove the URL. The URL field is a simple operator-managed text field: the operator is responsible for entering and removing links. Do not add URL availability checks, reachability checks or special URL validation rules. No supplier, purchase, receipt or delivery tracking. | Product-owner decisions 2026-11-20 | DECIDED |
| BR-14 | Cross-Shop ServiceOrder transfer follows SO-XFER-SPEC and is not implemented as a Company reassignment shortcut. | service-order-shop-transfer.md | DECIDED |
| BR-15 | A newly created ServiceOrder starts in `CREATED`, not `IN_PROGRESS`. | Product-owner decision 2026-11-19 | DECIDED |
| BR-16 | Device serial number is mandatory at ServiceOrder booking. Brand and model remain required as in the inspected legacy booking form. Serial-number uniqueness across the Company is not decided by this rule. | Product-owner decision 2026-11-19; NewServiceOrderView | DECIDED |
| BR-17 | `badSector` represents the device HDD bad-sector count. Each Service catalogue record has a `requiresBadSectorCount` flag, defaulting to `false`. The field is mandatory when at least one selected Service has this flag set to `true`; otherwise it is optional. The count must be a whole number greater than or equal to zero. No additional upper bound is defined in this SPEC. | Product-owner clarification 2026-11-19; legacy `spn_bad_sectors` | DECIDED |
| BR-18 | ServiceOrder workflows must not edit the linked Customer's master data. Equipment and service information may be edited through the ServiceOrder edit workflow; Customer master-data changes remain in the Customers feature. Payment and handover actions must not modify those details and may only append Notes. | Product-owner decision 2026-11-19 | DECIDED |
| BR-19 | Payment and customer-handover actions must not modify Customer, Device, Fault or ServiceOrder line details. They may append a separate Note; financial and handover records remain their own immutable events. | Product-owner decision 2026-11-19 | DECIDED |
| BR-20 | Pickup is allowed from either technical outcome, `FIXED` or `NOT_FIXED`, and changes lifecycle status to `PICKED` while preserving the technical outcome independently. Stock disposition differs by outcome and is defined in the Pickup command section below. | Product-owner decision 2026-11-20 | DECIDED |
| BR-21 | `EMPLOYEE`, `MANAGER` and `ADMIN` may mark a ServiceOrder `NOT_FIXED` and perform Pickup from either `FIXED` or `NOT_FIXED`. Cancellation and financial actions remain restricted to `MANAGER`/`ADMIN` where specified by this SPEC and FIN-01. | Product-owner decision 2026-11-20; employee-management.md | DECIDED |
| BR-22 | Every successfully committed action performed in the ServiceOrder context must append a chronological, employee-attributed entry to `Notes`. The entry describes the action and its result; entries are append-only. The structured ServiceOrder/financial event remains authoritative and the corresponding Note must be committed atomically with its action. Rejected or rolled-back actions must not create Notes. | Product-owner decision 2026-11-19; legacy OrderNote workflow | DECIDED |
| BR-23 | A payment `REFUND` for a ServiceOrder is eligible only when the order has a settled incoming `PAYMENT`, a positive refundable remainder, technical outcome `NOT_FIXED`, and lifecycle status `NOT_FIXED` or `PICKED`. A `DEPOSIT_REFUND` is a separate operation for an eligible refundable held deposit under its lifecycle rules. Refund is always explicit, never automatic on Pickup, requires a fresh `MANAGER` or `ADMIN` credential challenge, follows original-tender-only and refundable-balance rules, and must be recorded in `Notes`. | Product-owner decision 2026-11-19/21; FIN-01 | DECIDED |
| BR-24 | A `NOT_FIXED` device may be handed over even when the ServiceOrder has an outstanding balance or held deposit. Handover changes the lifecycle status to `PICKED` but does not settle, waive or automatically refund the outstanding amount. The financial balance and deposit remain governed by FIN-01; any eligible refund is a separate explicit `MANAGER`/`ADMIN` action. | Product-owner decision 2026-11-20 | DECIDED |
| BR-25 | Each ServiceOrder is associated with exactly one Device. Device serial number is mandatory but is not Company-unique; duplicate serial values are allowed. The operator may explicitly select/reuse an existing Device record during booking. | Product-owner decision 2026-11-20 | DECIDED |
| BR-26 | Product/Service lines cannot be added, edited, removed or repriced after the first stock reservation for the order or after any payment has been recorded (including partial payment), whichever occurs first. Before either event, line changes are permitted and must preserve the required history; issued document snapshots remain governed by REP-01. | Product-owner decision 2026-11-20 | DECIDED |
| BR-27 | `NOT_FIXED` is resumable to `IN_PROGRESS` while the device has not been handed over. `FINISHED` is not used in the target lifecycle. Cancellation is permitted only from `CREATED`, `IN_PROGRESS` or `AWAITING_PARTS`, including when payments or reservations exist; any required financial and stock adjustments must be completed through FIN-01 and the stock module. Cancellation is not permitted from `FIXED`, `NOT_FIXED` or `PICKED`. | Product-owner decision 2026-11-20 | DECIDED |
| BR-28 | ServiceOrder cancellation does not automatically refund payments, retain/forfeit deposits or create other financial adjustments. All refunds, deposit applications/forfeitures and related financial consequences are explicit actions governed exclusively by FIN-01. Cancellation releases applicable stock reservations; parts already consumed follow the inventory module's separate physical-return workflow. | Product-owner decision 2026-11-20 | DECIDED |
| BR-29 | Each Shop has its own sequential ServiceOrder display-number sequence. Every order also has a globally unique internal identifier. Display numbers are never reused, including after cancellation. Sequence allocation must be concurrency-safe. Version 1 UI language is English only; localization must remain centralized for future translations. | Product-owner decision 2026-11-20 | DECIDED |
| BR-30 | A `FIXED` ServiceOrder may return to `IN_PROGRESS` only while its balance due is greater than zero. The transition retains its active stock reservations and does not reverse financial or inventory postings. Once the balance reaches zero and reserved parts are consumed, direct reversal from `FIXED` is prohibited. Any later rework requires a separate corrective workflow and must not undo settled payments or stock movements. | Product-owner decision 2026-11-20; legacy `Undo Fixed` action constrained by target settlement model | DECIDED |
| BR-31 | Booking creates the ServiceOrder in `CREATED`; this does not mean repair work has started. An explicit start-work action moves it to `IN_PROGRESS` and requires a fresh employee code/password challenge so the acting employee is identified and recorded in the immutable event and `Notes`. If diagnosis/booking identifies an unfulfilled blocking part requirement before work starts, the order may move directly from `CREATED` to `AWAITING_PARTS`. Receipt/reservation of parts does not automatically start work; an explicit resume action is required to move from `AWAITING_PARTS` to `IN_PROGRESS`, also with fresh employee authentication. | Product-owner decision 2026-11-20 | DECIDED |
| BR-32 | Product-part requirements may be reserved partially as stock becomes available. The order remains `AWAITING_PARTS` while any blocking requirement is not fully reserved. It may resume to `IN_PROGRESS` only after every blocking requirement is fully reserved; partial reservation never permits resumption. | Product-owner decision 2026-11-20; stock reservation model | DECIDED |
| BR-33 | Parts to Buy combines an automatically recalculated projection of uncovered ServiceOrder part requirements with persisted manual entries. Automatic demand is grouped by Product with contributing ServiceOrders and per-order quantities, and changes as requirements, stock availability and reservations change. Manual entries are Shop-scoped and may reference an existing Product or use a free-text description for a part not yet catalogued. During editing, the user may change the selected Product or switch between a Product-backed entry and free text; a Product-backed entry displays that Product's catalogue reference links, while a free-text entry maintains its own links. For an existing Product, display its Product-catalogue reference links; for a free-text entry, allow the operator to add or remove reference URLs directly on that entry. Treat links as operator-maintained text; do not check URL availability or reachability. These URLs are informational only. Manual entries can be edited after creation, including changing the Product reference or switching between Product-backed and free-text modes, as well as changing quantity and applicable reference URLs; edits are audited with timestamp and acting-employee attribution. They can also be removed from the active list through a separate explicit delete action, without changing ServiceOrder data or history. Quantity changes are ordinary edits and require no separate justification or confirmation. No supplier, purchase-order, external-purchase, receipt or delivery-tracking workflow is created. | Product-owner decisions 2026-11-20 | DECIDED |
| BR-34 | `EMPLOYEE`, `MANAGER` and `ADMIN` may add, edit and remove manual Parts to Buy entries within the active Shop. All operations derive Shop scope from ShopSession and require a fresh employee code/password challenge; the backend resolves the acting employee and enforces active status, Company membership and role permission. Removal permanently deletes the manual entry and its entry-owned reference URLs; no removal history or restoration feature is required. The delete command remains Shop-scoped and requires the standard employee authorization challenge. | Product-owner decision 2026-11-20; employee-management.md | DECIDED |





## 5. Proposed redesign (REDESIGN)

### 5.1 Main workflow

1. Find or register the Company customer using CAT-01. The ServiceOrder workflow references the Customer and must not edit the Customer master record.
2. Register or select the device associated with the job. Brand, model and serial number are required; serial-number uniqueness is still to be decided.
3. Create the ServiceOrder in the active Shop with initial status `CREATED`, reported Faults, Product/Service lines, customer-facing description and work notes.
4. Capture the HDD bad-sector count when at least one selected Service has `requiresBadSectorCount = true` (operating-system installation is a confirmed example). The flag defaults to `false` for every Service; validate the count as a whole number greater than or equal to zero.
5. For each required Product part, create a ServicePartRequirement. Stock replenishment increases availability but never reserves or allocates stock automatically. An employee must explicitly select the ServiceOrder requirement(s) and quantities in a separate authorized reservation action. Partial reservations are supported. If a blocking requirement remains short, move the order to `AWAITING_PARTS`. The Parts to Buy view automatically reflects uncovered quantity; no purchase request or supplier workflow is created.
6. Record diagnosis, work notes and technical findings as append-only events/notes attributed to the employee who authorized each action. Every successfully committed ServiceOrder action (creation, edit, diagnosis, technical result, status change, part-requirement/reservation action, payment/deposit/refund, handover, cancellation or transfer) must append a human-readable `Notes` entry with the action, timestamp, employee and concise result/reference. Entries cannot be edited or deleted. The corresponding structured event/financial record remains authoritative; the Note and action commit atomically. Equipment and service information may be edited through the ServiceOrder's edit workflow; Customer master data may not. Product/Service lines may be added, edited, removed or repriced only until the first stock reservation or first recorded payment, whichever happens first; after that point all line and price changes are blocked. Payment and handover actions remain notes-only with respect to order details.
7. When blocking requirements are fully reserved, explicitly resume the order to `IN_PROGRESS`.
8. When repair is complete, an authorized `EMPLOYEE`, `MANAGER` or `ADMIN` may record `FIXED` or `NOT_FIXED`. `FIXED` is rejected while a blocking requirement remains short.
9. Receive payments, record deposits, apply deposits and process refunds through FIN-01. Each committed financial action associated with the ServiceOrder appends a `Notes` entry; financial ledger records remain authoritative. Payment/refund actions must not edit Customer, Device, Fault or ServiceOrder line details. When a `FIXED` order becomes fully settled, consume reserved Product parts in the same transaction as settlement. For `NOT_FIXED`, payment settlement alone never consumes or releases parts; stock usage is recorded separately during handover.
10. Perform Pickup from either `FIXED` or `NOT_FIXED`. Apply outcome-specific stock handling, fresh employee authentication, explicit confirmation, atomic event/Note persistence, idempotency and concurrency rules defined in the Pickup command section below. Pickup changes lifecycle status to `PICKED`, preserves technical outcome and never changes financial balances.
11. For a `NOT_FIXED` order, an authorized `MANAGER` or `ADMIN` may separately initiate a payment `REFUND` only when a settled incoming `PAYMENT` exists, a refundable remainder is positive, and the order's lifecycle status is `NOT_FIXED` or `PICKED` while its technical outcome remains `NOT_FIXED`. A refundable deposit balance may instead be returned through the distinct `DEPOSIT_REFUND` operation under FIN-01. Refunds are optional and explicit, never automatic on Pickup. Apply original-tender-only, refundable-balance and deposit-lifecycle rules; never refund the same funds twice. Record each committed financial action in `Notes`. Physical return/stock handling remains separate and follows the stock-return policy.
12. `NOT_FIXED` may return to `IN_PROGRESS` for another repair attempt, provided the device has not been handed over. Cancellation is allowed only while the order is `CREATED`, `IN_PROGRESS` or `AWAITING_PARTS`; it may still be cancelled when payments or stock reservations exist, but all required financial and stock adjustments must be handled through FIN-01 and the stock module. Cancellation is not allowed from `FIXED`, `NOT_FIXED` or `PICKED`. Physical stock return remains a separate, permission-gated workflow.


## 5.2 Target status vocabulary

A newly created order starts in `CREATED`. `REFUNDED` is a financial event/outcome, not a ServiceOrder lifecycle status. The target lifecycle vocabulary is:

- CREATED
- IN_PROGRESS
- AWAITING_PARTS
- FIXED
- NOT_FIXED
- PICKED
- CANCELLED

`FINISHED` is a legacy status and is not used in the target lifecycle. `REFUNDED` is a financial event/outcome, not a lifecycle status. The approved lifecycle transition matrix is:

| Current status | Allowed next status | Condition |
|---|---|---|
| `CREATED` | `IN_PROGRESS` | Explicit start-work command. |
| `CREATED` | `AWAITING_PARTS` | At least one blocking Product-part requirement is not fully reserved. |
| `CREATED` | `CANCELLED` | Explicit cancellation by an authorized `MANAGER` or `ADMIN`; financial and stock consequences remain separate FIN-01/stock operations. |
| `IN_PROGRESS` | `AWAITING_PARTS` | At least one blocking Product-part requirement is not fully reserved. |
| `IN_PROGRESS` | `FIXED` | Technical work is complete and every blocking part requirement is fully reserved. |
| `IN_PROGRESS` | `NOT_FIXED` | The technical outcome is explicitly recorded as not fixed. |
| `IN_PROGRESS` | `CANCELLED` | Explicit cancellation by an authorized `MANAGER` or `ADMIN`; financial and stock consequences remain separate FIN-01/stock operations. |
| `AWAITING_PARTS` | `IN_PROGRESS` | Every blocking part requirement is fully reserved and an explicit resume command is authorized. Receipt/reservation alone does not resume work. |
| `AWAITING_PARTS` | `CANCELLED` | Explicit cancellation by an authorized `MANAGER` or `ADMIN`; financial and stock consequences remain separate FIN-01/stock operations. |
| `FIXED` | `IN_PROGRESS` | Only while `balance_due > 0`; active reservations remain in place. After full settlement consumes reserved parts, direct reversal is prohibited and later rework requires a separate corrective workflow. |
| `FIXED` | `PICKED` | Explicit physical customer handover. |
| `NOT_FIXED` | `IN_PROGRESS` | Explicitly resume another repair attempt before physical handover. |
| `NOT_FIXED` | `PICKED` | Explicit physical customer handover; an outstanding balance or held deposit does not block it. |
| `PICKED` | — | Terminal lifecycle status; no direct undo or cancellation. |
| `CANCELLED` | — | Terminal lifecycle status. |

All transitions not listed above are rejected. Payment settlement does not automatically change lifecycle status to `PICKED`; Pickup is a separate physical handover command. Refunds and deposit refunds are financial operations and never change lifecycle status. Each successfully committed transition must atomically persist its structured event and append-only employee-attributed `Notes` entry. Authorization, stock effects, financial consequences, idempotency and concurrency rules remain governed by the relevant command contracts and FIN-01/stock specifications.

### 5.3 Proposed UI

- **Service Orders list:** search by global internal ID or Shop-scoped sequential display number, customer, contact, email, device brand/model and serial; filter by status and creation date after the filter policy is approved.
- **Create Service Order:** select or create a Customer through CAT-01, capture required device brand/model/serial, select one or more Faults, add Product/Service lines, and record initial notes. Display the HDD bad-sector count and require it only when a selected Service is configured to require it.
- **Service Order detail:** show customer/device identity, order status, reported faults, line items, payment/deposit summary supplied by FIN-01, part requirements/reservations, notes and append-only event history.
- **Technical actions:** diagnosis, technical note, add/update part requirement, resume from AWAITING_PARTS, Fixed/Not Fixed outcomes and customer handover as separately authorized actions.
- **Settings/navigation:** proposed primary navigation entry is Service Orders; the exact placement/order must be confirmed against the current application shell before UI implementation.
- All user-facing labels, localization and date/number formats must follow the product's global language/localization decision; this feature does not introduce a new language policy.

Loading, empty, validation, conflict, authorization-denied and retry states must be specified for each command before implementation.

## 6. Data requirements

Use the logical entities already recorded in domain-model.md and database-model.md; do not create a competing physical schema in this feature SPEC.

- ServiceOrder: globally unique internal ID, Shop ownership, immutable Shop-scoped sequential display number, Customer, exactly one non-null Device, initial status `CREATED`, lifecycle status, independently persisted technical outcome (including `NOT_FIXED` after status becomes `PICKED`), handover timestamp, currency, lifecycle timestamps and the integer HDD bad-sector count (`badSector` in the legacy model), conditionally required by selected Service.
- Financial summary: `balance_due` follows the canonical formula in [Financial Model](../02-architecture/financial-model.md): order total minus settled `PAYMENT` amounts targeting the order and committed `DepositApplication` credits. Unapplied deposit receipts, refunds, deposit refunds and internal transfer events are excluded. Payment/application commands cannot exceed the outstanding balance; any stored-vs-ledger mismatch blocks financial commands pending reconciliation.
- ServiceOrderItem: Product/Service line, whole-unit quantity for Product items, and monetary snapshot values as finalized by DATA-05. Line additions, edits, removals and repricing are blocked after the first stock reservation or first recorded payment, whichever occurs first. Before that lock, edits must preserve before/after values in the append-only ServiceOrderEvent history; removal is logical and must not hard-delete the original line. Issued document snapshots follow REP-01.
- Service catalogue configuration: each `Service` has `requiresBadSectorCount BOOLEAN NOT NULL DEFAULT FALSE`; selecting any Service with this flag enabled makes the order's HDD bad-sector count mandatory.
- ServiceOrderFault: links the order to Company-shared Fault catalogue entries. Removal of a Fault link is logical and must preserve its prior association in the append-only ServiceOrderEvent history.
- ServicePartRequirement: durable Product demand, required quantity, blocking flag and reservation state.
- ServiceOrderEvent: append-only status and operational event history with Shop, server-resolved actor Employee, previous/new state, event type/description, structured before/after payload where data is edited, and timestamp. No employee action-authorization token or authorization foreign key is stored.
- BusinessNote: append-only employee-attributed notes associated with the ServiceOrder. A human-readable Note is automatically appended for every successfully committed ServiceOrder action, including linked financial and handover actions; it records the server-resolved employee name snapshot, action, timestamp and concise result/reference. Notes are created atomically with their originating action and cannot be edited or deleted. No employee action-authorization token or authorization foreign key is stored.
- Financial records are owned by FIN-01; stock reservation/movement records are owned by the Stock module. Parts to Buy is a visibility list only and creates no procurement records.

Money uses C# decimal and PostgreSQL NUMERIC(19,2) in EUR. Store event timestamps in UTC. Allocate a sequential display number per Shop using a concurrency-safe mechanism; never reuse numbers after cancellation. Retain a globally unique internal identifier independently of the display number. Version 1 user-facing UI is English only, with centralized localization resources to support future translations. All Shop-scoped records must have same-Shop composite FK/integrity enforcement where specified by DATA-02.

## 7. API contracts

| Method / route | Purpose | Authorization |
|---|---|---|
| GET /api/v1/service-orders | Shop-scoped paginated list and search | ShopSession |
| GET /api/v1/service-orders/{serviceOrderId} | Read order detail and history | ShopSession; same-Shop check |
| POST /api/v1/service-orders | Create order and initial related records | Fresh employee code/password; EMPLOYEE, MANAGER or ADMIN |
| PUT /api/v1/service-orders/{serviceOrderId} | Update permitted order/device/line data (never Customer master data) | Fresh employee code/password; EMPLOYEE, MANAGER or ADMIN, subject to edit-state rules |
| POST /api/v1/service-orders/{serviceOrderId}/status-transitions | Execute an allowed status command | Fresh employee code/password; action-specific profile |
| POST /api/v1/service-orders/{serviceOrderId}/notes | Add a technical/operational note | Fresh employee code/password; EMPLOYEE, MANAGER or ADMIN |
| POST /api/v1/service-orders/{serviceOrderId}/part-requirements | Add/update required Product parts | Fresh employee code/password; EMPLOYEE, MANAGER or ADMIN |
| POST /api/v1/service-orders/{serviceOrderId}/resume | Explicitly resume from AWAITING_PARTS | Fresh employee code/password; EMPLOYEE, MANAGER or ADMIN |
| POST /api/v1/service-orders/{serviceOrderId}/pickup | Record physical customer handover from FIXED or NOT_FIXED | ShopSession plus fresh employee code/password in the request; EMPLOYEE, MANAGER or ADMIN; `Idempotency-Key` header required |

All routes other than Pickup remain candidate routes. FIN-01 owns payment/deposit/refund APIs; the Stock module owns inventory reservation, consumption and physical returns; SO-XFER-SPEC owns cross-Shop transfer.

### 7.2 Create ServiceOrder — customer reference

The create command must receive the identifier of an existing Customer (`customerId`) selected from the Company-shared customer directory.

### 7.3 Create ServiceOrder — Device identification

The create command must reference exactly one Device. Device `brand`, `model` and `serialNumber` are all required.

### 7.4 Create ServiceOrder — Fault selection

The create command must include at least one Fault identifier and may include multiple Faults from the Company-shared CAT-01 Fault catalogue.

### 7.5 Create ServiceOrder — Product and Service lines

The create command must include at least one Product or Service catalogue line and may include multiple lines.

### 7.6 Create ServiceOrder — HDD bad-sector count

The create request field `badSector` represents the HDD bad-sector count and, when supplied, must be a whole number greater than or equal to zero.

### 7.7 Create ServiceOrder — optional initial Note

The create request may include an optional `initialNote` free-text field.

### 7.8 Create ServiceOrder — deposits are separate financial commands

ServiceOrder creation must not accept or process an initial deposit, payment amount, payment method, tender allocation or any other financial receipt data.

### 7.9 Create ServiceOrder — Shop-scoped display number

The ServiceOrder display number is generated exclusively by the backend from a sequential counter scoped to the active Shop. The client must not submit or override the display number. Allocate the next number atomically within the ServiceOrder creation transaction so concurrent creations in the same Shop cannot receive the same number. A number allocated to a committed ServiceOrder is immutable and is never reused, including after cancellation. The global internal ServiceOrder identifier remains separate from this human-facing Shop-scoped display number. The ServiceOrder is created first; any deposit receipt is recorded afterward through the dedicated FIN-01 financial workflow and its own authorized transaction. This separation preserves independent financial transaction identity, tender/method attribution, ledger integrity and audit history. ServiceOrder creation must not create a Payment, DepositReceipt, DepositApplication or cash-drawer movement as a side effect. The server trims leading and trailing whitespace. If the value is omitted, `null`, empty or whitespace-only after trimming, no Note is created for this field. If non-empty, create exactly one append-only, employee-attributed Note as part of the same atomic ServiceOrder creation transaction, using the server-resolved employee identity/name snapshot and server timestamp. The Note cannot be edited or deleted. Raw employee credentials must not be included in the Note. It is conditionally required when at least one selected Service has `requiresBadSectorCount = true`; otherwise it may be omitted or sent as `null`. Negative, fractional and non-numeric values are rejected. No additional upper bound is defined. The server must evaluate the requirement against the current Service catalogue configuration during creation; the client cannot override the `requiresBadSectorCount` rule. Every selected catalogue item must exist, be active and belong to the same Company as the active Shop; inactive or foreign-Company items are rejected. Product lines require a positive whole-unit quantity. Service lines always have quantity `1`; fractional or multiple quantities for a Service line are not supported in v1. The ServiceOrder line must preserve immutable creation-time snapshots of the catalogue item name/description and unit price, plus the submitted quantity and calculated line total. Subsequent catalogue edits must not change the committed order-line snapshots. Every selected Fault must exist, be active and belong to the same Company as the active Shop. Inactive or foreign-Company Faults must be rejected. Duplicate Fault identifiers within one create request are invalid; each Fault may be linked to the ServiceOrder only once. During booking, the operator may explicitly select/reuse an existing Device record or register a new Device. Serial numbers are not unique within the Company; duplicate serial values are allowed. The backend must verify that a selected Device belongs to the same Company as the active Shop. ServiceOrder creation must not silently merge Device records based on matching serial numbers. It must not create, update, merge or otherwise mutate Customer master data as part of ServiceOrder creation. If the customer does not yet exist, the operator must create the Customer through CAT-01 and then select that record when creating the ServiceOrder. The backend must verify that the referenced Customer belongs to the active Shop's Company and is active/selectable for new operations; reject missing, inactive or foreign-Company Customer references.

### 7.10 Create ServiceOrder — authorization

Every ServiceOrder creation attempt is a protected employee command and requires a fresh employee credential challenge in the create request:
- Required fields: `employeeCode` and `employeePassword`. They are supplied for this attempt only and must not be cached or reused.
- `EMPLOYEE`, `MANAGER` and `ADMIN` may create ServiceOrders. `ADMIN` inherits the `MANAGER` permission set.
- The backend must validate the credentials directly, resolve the employee, and verify that the employee is active, belongs to the Company that owns the active ShopSession, and is authorized to create ServiceOrders.
- The active Shop and Company scope are derived from the authenticated ShopSession, never from client-supplied ownership fields.
- The server-resolved employee is the actor for the creation event and any initial Note. The client cannot supply or override the actor identity.
- Invalid credentials, inactive or foreign-Company employees, and unauthorized profiles must reject the command without creating a ServiceOrder, display number, event or Note.
- Never persist, echo or log the raw employee password. Credential fields must be excluded from request snapshots, idempotency payload hashes and application/audit logs.



### 7.11 Create ServiceOrder — response contract

On successful creation, return HTTP `201 Created` with a dedicated `ServiceOrderCreatedResponse` DTO; never serialize or expose the EF Core entity directly. Set the HTTP `Location` header to the canonical URI of the newly created ServiceOrder resource.

The response body must contain:
- `id`: globally unique internal ServiceOrder identifier.
- `displayNumber`: immutable Shop-scoped sequential display number allocated by the backend.
- `status`: the committed initial lifecycle status, either `CREATED` or `AWAITING_PARTS` when a blocking Product-part requirement is not fully reserved as part of creation.
- `technicalOutcome`: initially `null`.
- `customer`: customer identifier and display name.
- `device`: Device identifier, brand, model and serial number.
- `total`: server-calculated ServiceOrder total.
- `balanceDue`: initial outstanding balance calculated by the backend; creation itself records no payment or deposit.
- `createdAt`: server-generated creation timestamp.
- `createdBy`: server-resolved employee identifier and display-name snapshot.
- `notes`: the initial Note created by this command, if supplied and non-empty after trimming; otherwise an empty collection.

All monetary values use EUR decimal precision defined by FIN-01. The response must not contain employee credentials, raw passwords, internal persistence/navigation properties, or payment/deposit records that were not created. Values representing ownership or actor identity must come from server-resolved context, not client-supplied fields. The response reflects the committed result of the atomic creation transaction.



### 7.12 Create ServiceOrder — error contract

The create endpoint must use the following HTTP status mapping and a consistent error response body:
- `400 Bad Request`: malformed request, missing required fields, invalid primitive formats or request-shape validation failures.
- `401 Unauthorized`: missing or invalid authenticated ShopSession.
- `403 Forbidden`: the employee credential challenge is valid but the resolved employee is not authorized to create ServiceOrders.
- `404 Not Found`: a referenced Customer, Device, Fault or catalogue item does not exist within the applicable Company scope. Do not disclose whether a foreign-Company record exists.
- `409 Conflict`: concurrency or idempotency conflict, display-number allocation conflict, or another conflict with the current persisted resource state.
- `422 Unprocessable Entity`: structurally valid request rejected by a business rule, including inactive/non-selectable references, invalid catalogue state, conditional `badSector` requirement failure, or invalid Product/Service quantity or combination.

Every error response must include:
- `code`: stable machine-readable error identifier suitable for client handling.
- `message`: concise human-readable explanation.
- `errors`: optional field-level validation details; omit when no field-specific details apply.

The response must not expose credentials, password-validation details that facilitate credential guessing, internal exception messages, stack traces, database details or whether a foreign-Company identifier belongs to another Company. Validation failures and rejected commands must not create a ServiceOrder, allocate/consume a display number, or append an event or Note. All failures after transaction processing begins must roll back every creation side effect. The same error envelope and status semantics should be reused by the other ServiceOrder commands unless their owning feature contract explicitly defines a justified exception.



### 7.13 Create ServiceOrder — idempotency

The create endpoint requires an `Idempotency-Key` request header containing a valid UUID for every creation attempt. The key is scoped to the authenticated Shop and the ServiceOrder create operation.

- The backend must persist the idempotency record and the complete creation result atomically with the ServiceOrder, display-number allocation, initial event and optional initial Note.
- Repeating a committed request with the same key and the same normalized business payload returns the original HTTP status, response body and `Location` header. It must not create another ServiceOrder, allocate another display number, or append duplicate events/Notes.
- Reusing the same key with a different normalized business payload or a different resource scope returns `409 Conflict` with a stable error code such as `idempotency_key_reused`.
- A request that fails or rolls back before commit must not leave a committed idempotency record; the same key may be retried after correcting transient conditions.
- Concurrent requests using the same key must be serialized/guarded so that at most one creation transaction commits. A concurrent retry must return the committed original result when available, or a documented conflict/retryable response while the first request is still processing; it must never create a duplicate.
- The normalized business-payload hash and stored request snapshot must exclude `employeeCode` and `employeePassword`. Raw credentials must never be persisted, hashed into the idempotency payload, echoed or logged. Each attempt must still perform fresh employee credential validation before returning or executing the command, according to the direct employee-challenge policy.
- The idempotency key itself must be validated and scoped server-side; the client cannot choose Shop ownership or operation scope.
- A committed idempotency record is retained for 24 hours from the time the original command completes successfully. Do not expire or purge a key while its command is still processing. After the retention period, the idempotency record may be purged and the same key may be treated as a new attempt; the committed ServiceOrder and its business/audit history remain unaffected. The frontend should generate a new UUID for a genuinely new creation attempt.



### 7.14 Start Work command — idempotency

The explicit Start Work command that transitions a ServiceOrder from `CREATED` to `IN_PROGRESS` requires an `Idempotency-Key` request header containing a valid UUID. The key is scoped to the authenticated Shop, the Start Work operation and the target ServiceOrder.

- Every Start Work attempt requires a fresh employee code/password challenge. The backend resolves the employee and validates active status, Company membership and permission before processing the command. Do not cache or reuse employee authentication.
- Repeating a committed command with the same key and identical normalized request payload returns the original result without repeating the status transition, event or append-only Note.
- Reusing the same key with a different normalized payload, Shop, operation or ServiceOrder returns `409 Conflict` with a stable idempotency error code.
- The idempotency record, status transition to `IN_PROGRESS`, immutable transition event and corresponding employee-attributed Note must commit in one database transaction. Any failure rolls back all effects.
- A rolled-back attempt must not retain a committed idempotency record and may be retried with the same key.
- Concurrent commands for the same ServiceOrder must be guarded so that only one Start Work transition commits. A duplicate replay with the same key returns the original result; a distinct command that loses the state/concurrency race receives `409 Conflict` and must refresh the ServiceOrder.
- Exclude employee credentials from the normalized payload hash, stored request snapshot, response and logs. Each retry still performs fresh employee credential validation.
- Retain a committed idempotency record for 24 hours from successful command completion. Do not expire a key while processing. After expiry, the record may be purged; the ServiceOrder state and audit history remain unchanged.

This command applies only to `CREATED` → `IN_PROGRESS`. Resuming from `AWAITING_PARTS` is a separate explicit Resume command with its own authorization and idempotency contract; part receipt or reservation never starts work automatically.



### 7.15 Start Work command — response contract

On successful Start Work, return HTTP `200 OK` with a dedicated `ServiceOrderStatusResponse` DTO. Do not serialize or expose EF Core entities directly.

The response body must contain:
- `id`: globally unique internal ServiceOrder identifier.
- `displayNumber`: Shop-scoped ServiceOrder display number.
- `previousStatus`: `CREATED`.
- `status`: `IN_PROGRESS`.
- `technicalOutcome`: current technical outcome, initially `null`.
- `updatedAt`: server-generated timestamp for the committed transition.
- `updatedBy`: server-resolved employee identifier and display-name snapshot.
- `event`: the immutable transition event created by this command, including its identifier, event type and timestamp.
- `note`: the append-only employee-attributed Note created by this command, including its identifier, text and timestamp.

The response must represent the committed transaction and must not contain employee credentials, raw passwords, internal persistence/navigation properties or unrelated financial data. On an idempotent replay, return the original successful status code and response body without creating another transition event or Note.



### 7.16 Start Work command — error contract

A rejected Start Work command must return the shared API error envelope and must not change the ServiceOrder or create a transition event or append-only Note.

**Error response envelope**
- `code`: stable machine-readable error code.
- `message`: concise human-readable explanation.
- `errors`: optional field-level validation details; omit when not applicable.

**HTTP status mapping**
- `400 Bad Request` — malformed request, missing/invalid required header or invalid request shape.
- `401 Unauthorized` — missing or invalid employee credentials, or employee is inactive.
- `403 Forbidden` — credentials are valid, but the employee lacks Start Work permission or does not belong to the active Shop's Company.
- `404 Not Found` — the ServiceOrder does not exist or is outside the active Shop scope. Do not disclose whether an out-of-scope order exists.
- `409 Conflict` — the ServiceOrder is no longer in `CREATED` (for example, work was already started or the order was cancelled), a distinct concurrent command committed first, or the idempotency key was reused with a different normalized payload/resource scope.
- `422 Unprocessable Entity` — the request shape is valid, but an operational business rule prevents starting work.

Use stable, operation-specific error codes, including `service_order_not_found`, `service_order_start_work_conflict`, `idempotency_key_reused` and `start_work_business_rule_violation` where applicable. The response must not expose credentials, stack traces, internal database details or persistence implementation information.

For every rejected or rolled-back attempt, persist no status change, transition event, Note or committed idempotency result. A rolled-back attempt may be retried using the same key.


### 7.1 Pickup HTTP contract — approved

**Request**
`POST /api/v1/service-orders/{serviceOrderId}/pickup`

Headers:
- `Authorization`: authenticated Shop session (required by `ShopApi`).
- `Idempotency-Key`: required UUID string. The same key may be replayed only for the same Shop, operation, ServiceOrder and identical normalized request payload.

JSON body:
```json
{
  "employeeCode": "EMP001",
  "employeePassword": "employee-secret",
  "partsUsed": [
    {
      "reservationLineId": 123,
      "usedQuantity": 1
    }
  ]
}
```

- `employeeCode` and `employeePassword` are required for every attempt. The backend validates the employee directly against the existing employee credential, active status, Company membership and Pickup permission. Do not use an `EmployeeActionAuthorization` token or endpoint.
- `partsUsed` is optional for `FIXED` and must be omitted or empty. A non-empty list for `FIXED` is rejected.
- For `NOT_FIXED`, when active reservations exist, the request must include exactly one entry for every active reservation line, including lines with `usedQuantity: 0`. If no active reservations exist, an empty or omitted list is valid.
- `reservationLineId` must identify an active reservation line belonging to this ServiceOrder and active Shop. Duplicate, missing, foreign, released or otherwise inactive reservation lines are rejected.
- `usedQuantity` is an integer from zero through the currently active reserved quantity. The server calculates released quantity; clients cannot submit or override it.
- The request cannot include Product IDs, ServiceOrder line IDs, arbitrary stock quantities, ShopId, CompanyId, EmployeeId, actor role, stock movement values or financial adjustments.

**Successful response — HTTP 200**
```json
{
  "serviceOrderId": 456,
  "status": "PICKED",
  "technicalOutcome": "NOT_FIXED",
  "pickedAtUtc": "2026-11-21T12:00:00Z",
  "eventType": "SERVICE_ORDER_PICKED_UP",
  "note": "Service order picked up by Alex. Parts used: ... Parts released: ...",
  "stockDisposition": [
    {
      "reservationLineId": 123,
      "reservedQuantity": 2,
      "usedQuantity": 1,
      "releasedQuantity": 1
    }
  ]
}
```

The response is the canonical committed result. Before returning a stored result for any replay, the backend must revalidate the current employee code/password, active status, Company membership and Pickup permission; a prior successful request does not bypass current authentication or authorization. A replay with the same key and identical normalized business payload then returns the original HTTP status and response body, without repeating side effects. Employee credentials are excluded from the normalized request hash and request snapshot, and raw credentials must never be persisted or logged.

**Error contract**
- `400 invalid_request`: missing/invalid Idempotency-Key, malformed body, duplicate reservation line or invalid quantity shape.
- `401 invalid_employee_credentials`: employee code/password invalid or employee inactive.
- `403 action_not_permitted`: employee lacks Pickup permission or belongs to another Company.
- `404 service_order_not_found`: order does not exist in the active Shop.
- `409 service_order_pickup_conflict`: order is no longer eligible (including already PICKED) or another distinct Pickup command won the concurrency race; client must refresh.
- `409 idempotency_key_reused`: key was previously committed for a different normalized request payload or resource scope.
- `422 invalid_stock_disposition`: submitted reservation set is not exactly the current active set or used quantity exceeds the active reserved quantity.

Credential failures and rejected/rolled-back commands create no ServiceOrder event or Note. Never echo or log the submitted password.

### 7.2 Remaining contract work

The list/search, create, edit, status, notes, part-requirement, resume, payment, refund and transfer DTOs and error contracts remain outside this Pickup contract and must be approved by their owning feature specifications before implementation.

## 8. UI authorization and command invariants

- The API is authoritative for Shop ownership, Company ownership, record state and role checks.
- Do not accept ShopId, CompanyId, EmployeeId, actor role or audit actor from an untrusted request body.
- Each employee-protected command must validate the submitted employee code/password directly against the employee's `EmployeeCredential` at the start of processing. Validate active status, Company membership and action-specific profile permission before executing the command. Do not create, issue, consume, persist or reference an `EmployeeActionAuthorization`. Do not cache/reuse employee authentication. An operation that passed validation may finish if the employee is deactivated while it is in flight.
- Validate transitions against the current persisted state, with concurrency protection; never trust the status shown by a stale browser.
- Status transition, independently persisted technical outcome where applicable, resulting inventory reservation/release, immutable event and required `Notes` entry must commit atomically.
- Every successful ServiceOrder action must append exactly one corresponding `Notes` entry; Notes are a readable audit timeline, not a replacement for immutable domain or financial records.
- Pickup from either technical outcome changes lifecycle status to `PICKED` while retaining the independently stored technical outcome; it cannot itself create, settle or assume a refund.
- A refund during/around collection is a separate `MANAGER`/`ADMIN` FIN-01 action, limited to amounts still refundable under the original payment/deposit records; it must not duplicate or exceed a prior refund.
- Payment settlement must be idempotent and must consume reservations at most once.
- Preserve order, line and fault history. Do not delete/recreate child rows as a substitute for editing.
- Reject cross-Shop identifiers even when the Company-level customer or catalogue record is otherwise visible.
- A ServiceOrder command must not mutate the linked Customer master record. Customer updates use the separate CAT-01 customer workflow.
- Payment and handover commands may append a Note but must not mutate customer, device, fault or service-line details.

## 9. Acceptance criteria (DRAFT)

- [ ] AC-01: A Shop can list and search only its own ServiceOrders.
- [ ] AC-02: Search supports global internal ID, Shop-scoped sequential display number, customer identity/contact and device identity fields supported by the approved search policy.
- [ ] AC-03: An authorized employee can create a ServiceOrder using Company-owned customer/Fault/catalogue records and the active Shop context; the initial status is `CREATED`.
- [ ] AC-03a: Device brand, model and serial number are required at booking; a blank serial number is rejected.
- [ ] AC-04: The order records one or more approved Faults and Product/Service lines, subject to the final booking validation rules.
- [ ] AC-05: Device and serial validation follows the approved identity/reuse policy.
- [ ] AC-06: Order updates preserve historical line/fault references and create auditable events rather than hard-deleting prior rows. Before the first stock reservation or first recorded payment, line/fault edits append before/after snapshots to ServiceOrderEvent and removals are logical; after the lock, all line additions, edits, removals and repricing are rejected.
- [ ] AC-07: The UI shows the current status and append-only event/notes history.
- [ ] AC-08: A blocking Product requirement that is not fully reserved moves the order to AWAITING_PARTS from CREATED (before repair starts) or IN_PROGRESS (during repair).
- [ ] AC-08a: Creating a ServiceOrder leaves it in CREATED. Only an explicit start-work action with a fresh employee code/password challenge moves it to IN_PROGRESS; persist the server-resolved employee as the actor in the event and Notes. Receipt/reservation of parts does not start work automatically.
- [ ] AC-09: Only an explicit resume command with fresh employee code/password authentication resumes an order from AWAITING_PARTS, and only when all blocking requirements are fully reserved; record the acting employee in the event and Notes. Reject resume while any blocking requirement remains short.
- [ ] AC-09a: Support partial reservations for blocking part requirements as stock becomes available. Keep the order in AWAITING_PARTS until every blocking requirement is fully reserved; partial reservation must not permit resumption.
- [ ] AC-10: An order cannot become FIXED while a blocking requirement is short.
- [ ] AC-11: Full financial settlement consumes reserved Product quantities atomically; partial payment or unapplied deposit does not.
- [ ] AC-12: Payment completion does not mark the device as physically collected.
- [ ] AC-13: Cancellation is allowed only from CREATED, IN_PROGRESS or AWAITING_PARTS, including orders with payments or reservations; required adjustments are handled by FIN-01 and the stock module. Cancellation does not itself refund payments or retain/forfeit deposits; all such financial actions are explicit and governed by FIN-01. Applicable reservations are released, while consumed parts follow the inventory module's separate physical-return workflow. Cancellation, refund and physical stock return remain separately auditable.
- [ ] AC-14: Cross-Shop transfer is routed to SO-XFER-SPEC and cannot be implemented through direct Shop/Company reassignment.
- [ ] AC-15: Replayed or concurrent status/payment commands cannot cause duplicate events, stock movements or reservation consumption. Concurrent order creation cannot allocate duplicate Shop-scoped display numbers, and cancelled numbers are never reused.
- [ ] AC-16: Every Service catalogue record defaults `requiresBadSectorCount` to `false`; the ServiceOrder HDD bad-sector count is required if at least one selected Service has the flag set to `true`, and otherwise remains optional. The value accepts whole numbers greater than or equal to zero.
- [ ] AC-17: ServiceOrder editing never mutates Customer master data. Equipment/service information can be edited through the order edit workflow, separately from payment and handover actions.
- [ ] AC-18: Payment and handover commands do not alter Customer, Device, Fault or ServiceOrder line details; they append the corresponding `Notes` entry.
- [ ] AC-19: Pickup is accepted from `FIXED` and `NOT_FIXED`, changes lifecycle status to `PICKED`, preserves the technical outcome and applies the corresponding stock rules; EMPLOYEE, MANAGER and ADMIN may perform it.
- [ ] AC-20: Every successfully committed ServiceOrder action appends one chronological, employee-attributed `Notes` entry in the same transaction; failed or rolled-back actions do not create entries.
- [ ] AC-21: During collection of a `NOT_FIXED` order, a `MANAGER` or `ADMIN` can execute an eligible partial/total refund of settled payments or refundable deposit balances through FIN-01; refunds are explicit, original-tender only, capped by the unrefunded amount and are not automatic on pickup.
- [ ] AC-22: Allow `FIXED` → `IN_PROGRESS` only when `balance_due > 0`; preserve active stock reservations and do not reverse payments or inventory postings. Reject the transition when `balance_due = 0` and reserved parts have been consumed. A later rework request after settlement must use a separate corrective workflow.
- [ ] AC-23: Pickup is exposed as `POST /api/v1/service-orders/{serviceOrderId}/pickup`, requires an authenticated ShopSession, a fresh employee code/password challenge and a valid `Idempotency-Key`.
- [ ] AC-24: For `FIXED`, Pickup accepts an empty stock-disposition list and leaves every reservation, stock balance and movement unchanged, regardless of financial balance.
- [ ] AC-25: For `NOT_FIXED`, the request accounts for every currently active reservation line exactly once; only active reserved quantities may be used, unused quantities are released, and released quantity is calculated by the server.
- [ ] AC-26: Before a committed idempotent replay returns the original status/body, current employee credentials, active status, Company membership and Pickup permission are revalidated. The normalized business-payload hash and stored request snapshot exclude employee credentials; raw credentials are never persisted or logged. Reusing the key with a different business payload or resource scope returns `409 idempotency_key_reused`.
- [ ] AC-27: Two distinct concurrent Pickup commands for one ServiceOrder cannot both commit. Exactly one may transition the order to `PICKED`; the other receives `409 service_order_pickup_conflict`.
- [ ] AC-28: Any failure in the Pickup transaction rolls back the status, `picked_at`, event, Note, idempotency record and all applicable stock/reservation effects.
- [ ] AC-29: Pickup never changes payment, deposit, refund, balance-due, Customer, Device, Fault or ServiceOrder line data.

## 10. Test scenarios

- Shop isolation: list, detail, update, requirement and command attempts using another Shop's ServiceOrder ID.
- Company ownership: reject customer, catalogue or Fault IDs belonging to another Company.
- Booking validation: missing required fields, missing device serial, empty Fault list, empty lines, invalid Product/Service kind, invalid quantities and invalid amounts.
- Conditional HDD bad-sector validation: qualifying Service selected with missing count; non-qualifying Service selected with the field absent; order includes both qualifying and non-qualifying Services.
- Customer/device selection: existing customer, new customer, existing device by serial, changed device data and duplicate serial conflict.
- Status concurrency: stale transition, duplicate request, illegal transition, CREATED to IN_PROGRESS only through explicit start-work, CREATED/IN_PROGRESS to AWAITING_PARTS with blocking shortage, resume before/after full reservation, `NOT_FIXED` to `IN_PROGRESS` before handover, `FIXED` to `IN_PROGRESS` before full settlement with reservations retained, rejection after full settlement/stock consumption, prohibition on resuming after pickup, `FINISHED` rejection and FIXED with blocked requirement.
- Stock settlement: partial payment, explicitly applied deposit, zero balance, negative inventory balance, idempotent retry and reservation consumption exactly once.
- Pickup API: verify ShopSession authorization, route binding, required UUID `Idempotency-Key`, fresh employee code/password validation, permission matrix, request DTO validation and response contract.
- Pickup from `FIXED`: allow with zero, partial or outstanding balance; reject non-empty `partsUsed`; verify no reservation changes and no stock movements.
- Pickup from `NOT_FIXED`: test every active reservation line exactly once, used quantity at zero, partial and full reserved amount, release calculation, zero-use release-all, no active reservations, and rejection of missing/duplicate/foreign/released/unreserved reservation lines or over-quantity.
- Pickup atomicity: inject failures at status update, event insert, Note insert, stock movement, reservation update and idempotency persistence; verify complete rollback.
- Pickup idempotency: same key/same payload returns original response; same key/different payload or resource returns `409 idempotency_key_reused`; rolled-back request can be retried.
- Pickup concurrency: distinct keys racing on one order yield exactly one committed Pickup and one `409 service_order_pickup_conflict`; verify the loser refreshes.
- Pickup invariants: financial balance/deposit do not block Pickup; no payment/refund/balance mutation; explicit confirmation; technical outcome preserved; no direct undo; event/Note actor attribution; no order detail mutation.
- Notes traceability: verify creation, editing, diagnosis, status, parts, payment, deposit, refund, handover, cancellation and transfer actions each append one actor/time/action entry; verify entries are immutable and not created for failed/rolled-back commands.
- NOT_FIXED collection refunds: validate partial/full refund of eligible settled payments and held refundable deposits through FIN-01, `MANAGER`/`ADMIN` challenge, original-tender-only allocation, deposit/payment refundable caps, duplicate prevention and no automatic refund when status becomes PICKED.
- Authorization: EMPLOYEE/MANAGER/ADMIN profile matrix per action; fresh employee code/password challenge for every protected command; inactive/wrong-Company employee rejection; no cached authentication or EmployeeActionAuthorization token; server-resolved actor attribution; ShopSession-only reads.
- Audit: status change plus event/note atomicity, immutable prior lines/faults and event chronology.
- Cancellation/refund: allow cancellation from CREATED, IN_PROGRESS and AWAITING_PARTS with and without payments/reservations; reject cancellation from FIXED, NOT_FIXED and PICKED; verify no automatic refund or deposit forfeiture, explicit FIN-01 financial actions, reservation release, separate return of consumed parts, partial refund, refund cap and `MANAGER`/`ADMIN` authorization.

## 11. Owner decisions received — 2026-11-19

- **SO-OPEN-01 — RESOLVED:** New ServiceOrders start in `CREATED`.
- **SO-OPEN-02 — PARTIALLY RESOLVED:** Device serial number is mandatory. The inspected legacy form also requires brand, model, at least one Fault and at least one Product/Service line; no contrary change was requested.
- **SO-OPEN-03 — RESOLVED:** Each ServiceOrder references exactly one Device; serial number is mandatory but not Company-unique. The operator may explicitly select/reuse an existing Device.
- **SO-OPEN-04 — RESOLVED:** `badSector` means HDD bad-sector count. Each Service catalogue record has a `requiresBadSectorCount` flag, default `false`. The count is mandatory when any selected Service has the flag set to `true`; otherwise it is optional. It must be a whole number greater than or equal to zero, with no additional upper bound defined in this SPEC.
- **SO-OPEN-05 — RESOLVED:** The ServiceOrder workflow must not edit Customer master data. Equipment/service information may be edited subject to lifecycle restrictions. Payment and handover actions must not modify order details and may only append a Note alongside their own financial/handover event. Product/Service line changes are blocked after the first stock reservation or recorded payment, whichever occurs first.
- **SO-OPEN-07 — SUPERSEDED 2026-11-20:** Pickup is allowed from both `FIXED` and `NOT_FIXED`; it changes lifecycle status to `PICKED` and preserves technical outcome. An outstanding balance or held deposit does not block Pickup.
- **SO-OPEN-08 — RESOLVED:** EMPLOYEE, MANAGER and ADMIN may mark an order `NOT_FIXED` and hand over a `NOT_FIXED` order. Cancellation and financial operations remain restricted to MANAGER/ADMIN where specified by this SPEC and FIN-01.
- **SO-OPEN-11 — RESOLVED:** Every successfully committed action in the ServiceOrder context appends an immutable, chronological, employee-attributed `Notes` entry; the structured event/financial record remains authoritative.
- **SO-OPEN-12 — RESOLVED:** When collecting a `NOT_FIXED` order, eligible settled payments and refundable deposit balances may be refunded through FIN-01. Refund remains a separate, explicit `MANAGER`/`ADMIN` action, not an automatic pickup side effect; original-tender and refundable-balance controls apply.
- **SO-OPEN-07a — RESOLVED:** A `NOT_FIXED` device may be handed over with an outstanding balance or held deposit. Handover does not settle, waive or automatically refund the balance; FIN-01 continues to govern the financial records.
- **SO-OPEN-13 — RESOLVED/REFINED 2026-11-20:** `NOT_FIXED` payment settlement never consumes parts. At Pickup, consume actual used quantities only from active reservations and release the unused remainder atomically, regardless of balance. `FIXED` Pickup makes no stock changes; normal full-settlement consumption remains unchanged.

## 12. Product-owner decisions — all resolved

- [x] **SO-OPEN-03 — Device identity — RESOLVED:** Each ServiceOrder references exactly one Device. Serial number is mandatory but not Company-unique; duplicate serial values are allowed. During booking, the operator may explicitly select/reuse an existing Device record.
- [x] **SO-OPEN-04a — Bad-sector trigger — RESOLVED:** Configure `requiresBadSectorCount` as a boolean on each Service catalogue record, default `false`. Require the ServiceOrder count when any selected Service has the flag set to `true`; otherwise it is optional. Accept whole numbers greater than or equal to zero; no additional upper bound is defined in this SPEC.
- [x] **SO-OPEN-05a — Line and price history — RESOLVED:** Product/Service lines cannot be added, edited, removed or repriced after the first stock reservation or after any payment is recorded (including a partial payment), whichever happens first. Before that point, line changes are permitted and must preserve the required history. Issued document snapshots remain governed by REP-01.
- [x] **SO-OPEN-06 — State machine — RESOLVED:** `FINISHED` is not used in the target lifecycle. `NOT_FIXED` may return to `IN_PROGRESS` for another repair attempt until handover. Cancellation is allowed only from `CREATED`, `IN_PROGRESS` or `AWAITING_PARTS`, even with payments or stock reservations; required financial and stock adjustments follow FIN-01 and the stock module. Cancellation is prohibited from `FIXED`, `NOT_FIXED` and `PICKED`. Refunds are financial events, not lifecycle statuses.
- [x] **SO-OPEN-07a — Handover and balance — RESOLVED:** A `NOT_FIXED` device may be handed over with an outstanding balance or held deposit. Handover sets `PICKED` without settling, waiving or automatically refunding any amount. Financial balances remain under FIN-01; eligible refunds are separate explicit `MANAGER`/`ADMIN` actions.
- [x] **SO-OPEN-09 — Cancellation and deposits — RESOLVED:** Cancellation does not automatically refund payments, retain/forfeit deposits or create financial adjustments. Refunds, deposit applications/forfeitures and other financial consequences are separate explicit actions governed by FIN-01. Applicable stock reservations are released; already-consumed parts follow the inventory module's separate physical-return workflow.
- [x] **SO-OPEN-10 — Numbering and language — RESOLVED:** Each Shop has its own sequential ServiceOrder display-number sequence. Each order also has a globally unique internal identifier. Display numbers are never reused, including after cancellation, and allocation must be concurrency-safe. English is the only v1 UI language; localization remains centralized for future translations.

## 13. Technical reconciliation findings — 2026-11-20

- **Refund eligibility and permissions:** payment refunds on ServiceOrders require a settled incoming `PAYMENT`, positive refundable remainder, technical outcome `NOT_FIXED`, and lifecycle `NOT_FIXED` or `PICKED`; deposit refunds use the separate `DEPOSIT_REFUND` flow. Both `MANAGER` and `ADMIN` may execute eligible refunds with fresh credentials. Deposit forfeiture remains governed by its separate FIN-01 permission rule.
- **Employee profiles:** all `TECHNICIAN` references in the prior SO-01 draft are superseded. Supported profiles are exactly `EMPLOYEE`, `MANAGER` and `ADMIN`; `ADMIN` inherits `MANAGER`. This SPEC grants the operational ServiceOrder actions in BR-04 to `EMPLOYEE`.
- **Employee command authentication:** every protected command requires a fresh employee code/password challenge. The backend resolves the employee and validates active status, Company membership and action permission at command start. No employee identity/role is added to `ShopSession` or OIDC tokens. No `EmployeeActionAuthorization` token, entity, endpoint, foreign key or consumption step is used. Events and Notes use the server-resolved employee identity/name snapshot.
- **Read access:** list/detail/search remain ShopSession-scoped and do not require employee challenge; protected commands require the challenge described above.
- **Customer integrity:** ServiceOrder edits may update the order's Device and operational details, but never mutate the linked Customer master record.
- **State-machine boundary:** booking remains `CREATED` until an explicit start-work command; a blocking shortage may move it directly to `AWAITING_PARTS`, and receiving/reserving parts never starts work automatically. `NOT_FIXED` may return to `IN_PROGRESS` before handover. The legacy `Undo Fixed` action is preserved with the approved constraint: `FIXED` may return to `IN_PROGRESS` only while a balance remains due; reservations stay active. After full settlement consumes the reserved parts, direct reversal is rejected and later rework requires a separate corrective workflow. `FINISHED` and `REFUNDED` are not target lifecycle statuses.
- **Shared architecture documents:** `domain-model.md` and `database-model.md` now reflect the current `EMPLOYEE` / `MANAGER` / `ADMIN` model and direct employee credential validation. `authentication-persistence.md` retains the former employee authorization design only as an explicitly retired section; Shop/OIDC persistence remains applicable. Older employee-role and one-use authorization ADR text is historical under the supersession notice at the top of `architecture-decisions.md`.
- **Numbering/persistence:** enforce a globally unique internal ID and unique `(shop_id, display_number)`. Allocate the next Shop-scoped display number atomically in the order-creation transaction; never reuse allocated numbers. Before the line lock, persist line/fault changes as logical updates/removals and append before/after snapshots to ServiceOrderEvent; do not hard-delete historical child rows. Physical schema/migration details remain subject to DATA-05 review.

## 14. Approval

- Product approval: COMPLETE — all product-owner questions recorded in this SPEC are resolved.
- Technical approval: PENDING — complete shared architecture/persistence reconciliation, finalize the full transition matrix, DTOs, persistence constraints and command transactions.
- Implementation authorization: NOT GRANTED — implementation remains blocked until technical approval and required legacy/architecture reconciliation are complete.



## Simplified Parts to Buy view — owner decision

The Shop-scoped Parts to Buy list presents two visibly distinct sections or source labels: (1) **ServiceOrder demand**, automatically calculated uncovered Product-part requirements grouped by Product and broken down by originating ServiceOrder (order number, customer/device context and quantity still needed); and (2) **Manual entries**, reminders for additional parts not planned on a ServiceOrder. Keep these sources separate and never combine manual quantities into calculated ServiceOrder demand totals. Order both sections by creation date descending (newest first): manual entries by entry creation time; calculated Product groups by the newest contributing ServiceOrder creation time, with each group's contributing orders also ordered newest first. A manual entry may select an existing Product or use a free-text description for a part not yet in the catalogue. Its quantity defaults to `1` and may be changed to any positive whole number. Existing Products display all external reference links maintained centrally on the Product catalogue record. Free-text entries may store multiple external reference URLs directly on the manual entry. A manual entry can be permanently deleted; deletion affects only that manual entry and its entry-owned reference URLs, and does not modify ServiceOrder requirements, events or Notes. Links are informational only and do not represent tracked supplier or purchase records. The automatic ServiceOrder portion recalculates when requirements, stock availability or reservations change; manual entries remain separately identifiable. Employees use the list to see what parts need replenishment and buy externally using their preferred channel. The application does not record suppliers, purchase orders, external purchases, delivery/ETA, or supplier receipts.


## Pickup command — approved cross-outcome behavior (2026-11-20)

Pickup is the physical customer handover command and is allowed from either technical outcome, `FIXED` or `NOT_FIXED`. It changes the lifecycle status to `PICKED`, records `picked_at`, and preserves the technical outcome independently. It is not a payment, settlement, refund, cancellation or stock-settlement shortcut. An outstanding balance or held deposit never blocks Pickup and is not changed by it.

**Authorization and confirmation**
- `EMPLOYEE`, `MANAGER` and `ADMIN` may perform Pickup, subject to the current employee access matrix.
- Every attempt requires a fresh employee code/password challenge for the acting employee. Resolve and validate the employee server-side; attribute the command, event, stock movements and Note to that employee.
- Before confirmation, show the order, customer, acting employee and (for `NOT_FIXED`) each active reservation with reserved, used and released quantities. Require explicit final confirmation and communicate that Pickup has no direct undo/cancel action.
- Pickup must not edit Customer, Device, Fault, ServiceOrder lines or financial records.

**Stock disposition**
- For `FIXED`, Pickup creates no stock movement and does not consume, release or otherwise change reservations. The normal payment-settlement rule remains authoritative: active reservations are consumed atomically when settled payments and explicitly applied deposits reduce the balance to zero. If the order is still unpaid/part-paid at Pickup, reservations remain active until that normal settlement occurs.
- For `NOT_FIXED`, show every active reservation. For each line, initialize integer `used_quantity` to zero and calculate `released_quantity = reserved_quantity - used_quantity`. Require `0 <= used_quantity <= active_reserved_quantity`. Only quantities covered by active reservations may be consumed; no unreserved Product or manually added quantity is permitted.
- On successful `NOT_FIXED` Pickup, consume the used quantity and release the unused quantity for every active reservation in the same transaction. If all used quantities are zero, release all active reservations. If there are no active reservations, skip the stock-disposition step and allow Pickup.
- Stock disposition is independent of the financial balance, including a zero balance or an outstanding balance.

**Atomicity, concurrency and idempotency**
- One database transaction must persist lifecycle status `PICKED`, `picked_at`, immutable event `SERVICE_ORDER_PICKED_UP`, the required append-only Note, and applicable `NOT_FIXED` reservation/stock changes. Any failure rolls back every effect.
- Require an `IdempotencyKey` per command. Replaying a key for a committed command returns its original result without repeating effects. A rolled-back attempt may be retried. Distinct concurrent commands for the same order must be serialized/guarded so only one can commit; the loser receives a conflict and must refresh.
- Pickup cannot be directly undone or cancelled. Any correction is a separate auditable operation requiring employee, reason and timestamp, with explicit stock movements; it must not reactivate reservations automatically.

**Notes**
- General: `Service order picked up by {Employee}.`
- `NOT_FIXED` with used parts: append `Parts used: {details}. Parts released: {details}.`
- `NOT_FIXED` with active reservations and zero usage: append `All reserved parts released.`
- `NOT_FIXED` with no active reservations: use only the general Note.
- The timestamp is structured metadata and need not be repeated in Note text.
