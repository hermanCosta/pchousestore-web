# SO-01 — Service Orders

**Status:** PRODUCT DECISIONS COMPLETE — TECHNICAL RECONCILIATION IN PROGRESS (2026-11-20). No implementation is authorized. The latest Employee Management SPEC supersedes older employee-role and authorization designs. Shared domain/database architecture documents still contain obsolete employee authorization references and must be reconciled before technical approval.  
**Type:** Shop-scoped operational workflow.  
**Depends on:** AUTH-01/AUTH-02, CAT-01, DATA-02, approved employee access matrix, stock and financial policies.  
**Related work:** PROC-01/PROC-02 (parts Parts to Buy view), FIN-01 (payments/deposits/refunds), SO-XFER-SPEC (cross-Shop transfer), REP-01 (documents and receipts).

## 1. Objective and scope

Define the end-to-end Service Order workflow used to receive a customer's device, record the reported faults, diagnose and perform work, identify required Product parts, track technical progress, record service notes, and hand the device back to the customer.

A ServiceOrder is an operational record owned by one Shop. Customers and catalogue definitions remain Company-shared master data. The active Shop is always derived from the authenticated ShopSession.

This SPEC is limited to the Service Order workflow and its integration points. It does not redefine the financial ledger, procurement queue, stock ledger, cross-Shop transfer or document-generation modules.

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
| BR-04 | `EMPLOYEE` may create and edit ServiceOrders before the line/price lock, add operational notes, perform diagnosis and technical actions, manage part requirements, resume eligible orders, record `FIXED`/`NOT_FIXED` and hand over a `NOT_FIXED` device. `MANAGER` has full ServiceOrder access, including cancellation and ServiceOrder financial actions assigned to managers by FIN-01. `ADMIN` inherits the full `MANAGER` permission set. The retired `TECHNICIAN` profile is not used. | employee-management.md; product-owner decisions | DECIDED |
| BR-05 | A part requirement is linked to a Product ServiceOrderItem and uses a positive whole-unit quantity. SERVICE catalogue lines never create stock requirements. | stock-and-refurbishment.md; ADR-013 | DECIDED |
| BR-06 | If any blocking part requirement is not fully reserved, the ServiceOrder enters AWAITING_PARTS. It can originate during booking/diagnosis or later work. | ADR-013; domain-model.md | DECIDED |
| BR-07 | An order in `AWAITING_PARTS` may return to `IN_PROGRESS` only when every blocking part requirement is fully reserved. The transition requires an explicit resume command with a fresh employee code/password challenge; receipt or reservation alone never restarts repair. Record the server-resolved employee in the immutable event and `Notes`. | Product-owner decision 2026-11-20; ADR-013 | DECIDED |
| BR-08 | FIXED requires all blocking requirements to be fully reserved and retains active reservations while the repaired device awaits payment/collection. | ADR-006; stock-and-refurbishment.md | DECIDED |
| BR-09 | Settled payments and explicitly applied deposits reducing the ServiceOrder balance to zero consume reserved Product parts atomically. Partial payments and unapplied deposits do not consume stock. | ADR-006; financial-model.md | DECIDED |
| BR-10 | Physical pickup is a separate event from payment completion and stock consumption. PICKED must not be set merely because a payment was recorded. | ADR-006; product-owner-decisions.md | DECIDED |
| BR-11 | Financial deposits, applications, refunds and forfeitures follow FIN-01/financial-model.md. This SPEC must not introduce a parallel balance or deposit ledger. | financial-model.md | DECIDED |
| BR-12 | Financial refund and physical stock return are separate. Only selected, physically returned quantities may enter the stock-return workflow; inspection/disposition rules apply. | architecture-decisions.md; stock-and-refurbishment.md | DECIDED |
| BR-13 | The Shop has a simple, Shop-scoped Parts to Buy list with two visibly distinct sources: automatically calculated uncovered Product-part requirements from ServiceOrders and manually added reminders not planned on an order. Label each row/group as `ServiceOrder demand` or `Manual entry`; never merge manual quantities into calculated demand totals. Sort both sections by creation date descending (newest first): manual entries by their creation timestamp; ServiceOrder demand groups by the newest creation timestamp among their contributing ServiceOrders, with the contributing orders also listed newest first. Provide one search that searches Product names in ServiceOrder demand and Product-backed manual entries, and searches free-text descriptions in manual entries; update results automatically as the user types, without requiring Enter or a separate submit action. Keep results source-labeled and quantities separate. Automatic demand is grouped by Product and retains originating ServiceOrders and per-order quantities. Manual entries may select an existing Product or describe a free-text part, default quantity `1` (editable positive whole number), and use Product-catalogue URLs or entry-owned URLs as applicable. Manual entries can be edited and permanently deleted; deleting affects only the manual entry and its own URLs, never ServiceOrder requirements, events or Notes. Links are informational only. No supplier, purchase, receipt or delivery tracking. | Product-owner decisions 2026-11-20 | DECIDED |
| BR-14 | Cross-Shop ServiceOrder transfer follows SO-XFER-SPEC and is not implemented as a Company reassignment shortcut. | service-order-shop-transfer.md | DECIDED |
| BR-15 | A newly created ServiceOrder starts in `CREATED`, not `IN_PROGRESS`. | Product-owner decision 2026-11-19 | DECIDED |
| BR-16 | Device serial number is mandatory at ServiceOrder booking. Brand and model remain required as in the inspected legacy booking form. Serial-number uniqueness across the Company is not decided by this rule. | Product-owner decision 2026-11-19; NewServiceOrderView | DECIDED |
| BR-17 | `badSector` represents the device HDD bad-sector count. Each Service catalogue record has a `requiresBadSectorCount` flag, defaulting to `false`. The field is mandatory when at least one selected Service has this flag set to `true`; otherwise it is optional. The count must be a whole number greater than or equal to zero. No additional upper bound is defined in this SPEC. | Product-owner clarification 2026-11-19; legacy `spn_bad_sectors` | DECIDED |
| BR-18 | ServiceOrder workflows must not edit the linked Customer's master data. Equipment and service information may be edited through the ServiceOrder edit workflow; Customer master-data changes remain in the Customers feature. Payment and handover actions must not modify those details and may only append Notes. | Product-owner decision 2026-11-19 | DECIDED |
| BR-19 | Payment and customer-handover actions must not modify Customer, Device, Fault or ServiceOrder line details. They may append a separate Note; financial and handover records remain their own immutable events. | Product-owner decision 2026-11-19 | DECIDED |
| BR-20 | Customer handover is allowed only from `NOT_FIXED` and is prohibited while the current order status is `FIXED`. On handover, the lifecycle status changes to `PICKED` while the technical outcome `NOT_FIXED` is preserved independently. | Product-owner decision 2026-11-19 | DECIDED |
| BR-21 | `EMPLOYEE`, `MANAGER` and `ADMIN` may mark a ServiceOrder `NOT_FIXED` and record handover of a `NOT_FIXED` order. Cancellation and financial actions remain restricted to `MANAGER`/`ADMIN` where specified by this SPEC and FIN-01. | Product-owner decision 2026-11-19; employee-management.md | DECIDED |
| BR-22 | Every successfully committed action performed in the ServiceOrder context must append a chronological, employee-attributed entry to `Notes`. The entry describes the action and its result; entries are append-only. The structured ServiceOrder/financial event remains authoritative and the corresponding Note must be committed atomically with its action. Rejected or rolled-back actions must not create Notes. | Product-owner decision 2026-11-19; legacy OrderNote workflow | DECIDED |
| BR-23 | When a `NOT_FIXED` order is handed over, eligible amounts already received for the order, including refundable deposit balances, may be refunded through FIN-01. A refund is a separate explicit financial action, not an automatic consequence of handover; it remains restricted to `MANAGER`/`ADMIN`, follows the original-tender and refundable-balance rules, and must be recorded in `Notes`. | Product-owner decision 2026-11-19; FIN-01 | DECIDED |
| BR-24 | A `NOT_FIXED` device may be handed over even when the ServiceOrder has an outstanding balance or held deposit. Handover changes the lifecycle status to `PICKED` but does not settle, waive or automatically refund the outstanding amount. The financial balance and deposit remain governed by FIN-01; any eligible refund is a separate explicit `MANAGER`/`ADMIN` action. | Product-owner decision 2026-11-20 | DECIDED |
| BR-25 | Each ServiceOrder is associated with exactly one Device. Device serial number is mandatory but is not Company-unique; duplicate serial values are allowed. The operator may explicitly select/reuse an existing Device record during booking. | Product-owner decision 2026-11-20 | DECIDED |
| BR-26 | Product/Service lines cannot be added, edited, removed or repriced after the first stock reservation for the order or after any payment has been recorded (including partial payment), whichever occurs first. Before either event, line changes are permitted and must preserve the required history; issued document snapshots remain governed by REP-01. | Product-owner decision 2026-11-20 | DECIDED |
| BR-27 | `NOT_FIXED` is resumable to `IN_PROGRESS` while the device has not been handed over. `FINISHED` is not used in the target lifecycle. Cancellation is permitted only from `CREATED`, `IN_PROGRESS` or `AWAITING_PARTS`, including when payments or reservations exist; any required financial and stock adjustments must be completed through FIN-01 and the stock module. Cancellation is not permitted from `FIXED`, `NOT_FIXED` or `PICKED`. | Product-owner decision 2026-11-20 | DECIDED |
| BR-28 | ServiceOrder cancellation does not automatically refund payments, retain/forfeit deposits or create other financial adjustments. All refunds, deposit applications/forfeitures and related financial consequences are explicit actions governed exclusively by FIN-01. Cancellation releases applicable stock reservations; parts already consumed follow the inventory module's separate physical-return workflow. | Product-owner decision 2026-11-20 | DECIDED |
| BR-29 | Each Shop has its own sequential ServiceOrder display-number sequence. Every order also has a globally unique internal identifier. Display numbers are never reused, including after cancellation. Sequence allocation must be concurrency-safe. Version 1 UI language is English only; localization must remain centralized for future translations. | Product-owner decision 2026-11-20 | DECIDED |
| BR-30 | A `FIXED` ServiceOrder may return to `IN_PROGRESS` only while its balance due is greater than zero. The transition retains its active stock reservations and does not reverse financial or inventory postings. Once the balance reaches zero and reserved parts are consumed, direct reversal from `FIXED` is prohibited. Any later rework requires a separate corrective workflow and must not undo settled payments or stock movements. | Product-owner decision 2026-11-20; legacy `Undo Fixed` action constrained by target settlement model | DECIDED |
| BR-31 | Booking creates the ServiceOrder in `CREATED`; this does not mean repair work has started. An explicit start-work action moves it to `IN_PROGRESS` and requires a fresh employee code/password challenge so the acting employee is identified and recorded in the immutable event and `Notes`. If diagnosis/booking identifies an unfulfilled blocking part requirement before work starts, the order may move directly from `CREATED` to `AWAITING_PARTS`. Receipt/reservation of parts does not automatically start work; an explicit resume action is required to move from `AWAITING_PARTS` to `IN_PROGRESS`, also with fresh employee authentication. | Product-owner decision 2026-11-20 | DECIDED |
| BR-32 | Product-part requirements may be reserved partially as stock becomes available. The order remains `AWAITING_PARTS` while any blocking requirement is not fully reserved. It may resume to `IN_PROGRESS` only after every blocking requirement is fully reserved; partial reservation never permits resumption. | Product-owner decision 2026-11-20; stock reservation model | DECIDED |
| BR-33 | Parts to Buy combines an automatically recalculated projection of uncovered ServiceOrder part requirements with persisted manual entries. Automatic demand is grouped by Product with contributing ServiceOrders and per-order quantities, and changes as requirements, stock availability and reservations change. Manual entries are Shop-scoped and may reference an existing Product or use a free-text description for a part not yet catalogued. During editing, the user may change the selected Product or switch between a Product-backed entry and free text; a Product-backed entry displays that Product's catalogue reference links, while a free-text entry maintains its own links. For an existing Product, display all Product-catalogue external reference links; for a free-text entry, allow multiple reference URLs to be maintained on that entry. These URLs are informational only. Manual entries can be edited after creation, including changing the Product reference or switching between Product-backed and free-text modes, as well as changing quantity and applicable reference URLs; edits are audited with timestamp and acting-employee attribution. They can also be removed from the active list without changing ServiceOrder data or history. No supplier, purchase-order, external-purchase, receipt or delivery-tracking workflow is created. | Product-owner decisions 2026-11-20 | DECIDED |
| BR-34 | `EMPLOYEE`, `MANAGER` and `ADMIN` may add, edit and remove manual Parts to Buy entries within the active Shop. All operations derive Shop scope from ShopSession and require a fresh employee code/password challenge; the backend resolves the acting employee and enforces active status, Company membership and role permission. Removal permanently deletes the manual entry and its entry-owned reference URLs; no removal history or restoration feature is required. The delete command remains Shop-scoped and requires the standard employee authorization challenge. | Product-owner decision 2026-11-20; employee-management.md | DECIDED |





## 5. Proposed redesign (REDESIGN)

### 5.1 Main workflow

1. Find or register the Company customer using CAT-01. The ServiceOrder workflow references the Customer and must not edit the Customer master record.
2. Register or select the device associated with the job. Brand, model and serial number are required; serial-number uniqueness is still to be decided.
3. Create the ServiceOrder in the active Shop with initial status `CREATED`, reported Faults, Product/Service lines, customer-facing description and work notes.
4. Capture the HDD bad-sector count when at least one selected Service has `requiresBadSectorCount = true` (operating-system installation is a confirmed example). The flag defaults to `false` for every Service; validate the count as a whole number greater than or equal to zero.
5. For each required Product part, create a ServicePartRequirement and reserve available quantity as appropriate. If a blocking requirement remains short, move the order to `AWAITING_PARTS`. The Parts to Buy view automatically reflects the uncovered quantity; no purchase request or supplier workflow is created.
6. Record diagnosis, work notes and technical findings as append-only events/notes attributed to the employee who authorized each action. Every successfully committed ServiceOrder action (creation, edit, diagnosis, technical result, status change, part-requirement/reservation action, payment/deposit/refund, handover, cancellation or transfer) must append a human-readable `Notes` entry with the action, timestamp, employee and concise result/reference. Entries cannot be edited or deleted. The corresponding structured event/financial record remains authoritative; the Note and action commit atomically. Equipment and service information may be edited through the ServiceOrder's edit workflow; Customer master data may not. Product/Service lines may be added, edited, removed or repriced only until the first stock reservation or first recorded payment, whichever happens first; after that point all line and price changes are blocked. Payment and handover actions remain notes-only with respect to order details.
7. When blocking requirements are fully reserved, explicitly resume the order to `IN_PROGRESS`.
8. When repair is complete, an authorized `EMPLOYEE`, `MANAGER` or `ADMIN` may record `FIXED` or `NOT_FIXED`. `FIXED` is rejected while a blocking requirement remains short.
9. Receive payments, record deposits, apply deposits and process refunds through FIN-01. Each committed financial action associated with the ServiceOrder appends a `Notes` entry; financial ledger records remain authoritative. Payment/refund actions must not edit Customer, Device, Fault or ServiceOrder line details. When the financial balance is fully settled, consume reserved Product parts in the same transaction as the settlement.
10. Customer handover is a separate action and is allowed only when the order's technical outcome is `NOT_FIXED`; it is blocked while the current status is `FIXED`. An outstanding balance or held deposit does not block handover. On successful handover, set the lifecycle status to `PICKED` and preserve `technicalOutcome = NOT_FIXED` independently. Handover does not settle, waive or automatically refund any financial balance. `EMPLOYEE`, `MANAGER` and `ADMIN` may perform this handover. Append the handover action to `Notes`; do not edit order details.
11. During collection of a `NOT_FIXED` order, a `MANAGER` or `ADMIN` may separately initiate a refund of eligible settled payments and refundable deposit balances through FIN-01. This is optional and explicit, not automatic on pickup. Apply original-tender, refundable-balance and deposit-lifecycle rules; never refund the same funds twice. Record the refund in `Notes`. Physical return/stock handling remains separate and follows the stock-return policy.
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

`FINISHED` is a legacy status and is not used in the target lifecycle. `NOT_FIXED` can return to `IN_PROGRESS` for another repair attempt until handover occurs. `FIXED` can return to `IN_PROGRESS` only while `balance_due > 0`, retaining reservations; after full settlement and stock consumption, direct reversal is prohibited and later rework requires a separate corrective workflow. Cancellation is permitted only from `CREATED`, `IN_PROGRESS` or `AWAITING_PARTS`, even if payments or stock reservations exist; required financial and inventory adjustments remain owned by FIN-01 and the stock module. Cancellation is prohibited from `FIXED`, `NOT_FIXED` and `PICKED`. `PICKED` represents physical collection, not payment completion. The full transition matrix remains subject to technical verification.

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
- ServiceOrderItem: Product/Service line, whole-unit quantity for Product items, and monetary snapshot values as finalized by DATA-05. Line additions, edits, removals and repricing are blocked after the first stock reservation or first recorded payment, whichever occurs first. Before that lock, edits must preserve before/after values in the append-only ServiceOrderEvent history; removal is logical and must not hard-delete the original line. Issued document snapshots follow REP-01.
- Service catalogue configuration: each `Service` has `requiresBadSectorCount BOOLEAN NOT NULL DEFAULT FALSE`; selecting any Service with this flag enabled makes the order's HDD bad-sector count mandatory.
- ServiceOrderFault: links the order to Company-shared Fault catalogue entries. Removal of a Fault link is logical and must preserve its prior association in the append-only ServiceOrderEvent history.
- ServicePartRequirement: durable Product demand, required quantity, blocking flag and reservation state.
- ServiceOrderEvent: append-only status and operational event history with Shop, server-resolved actor Employee, previous/new state, event type/description, structured before/after payload where data is edited, and timestamp. No employee action-authorization token or authorization foreign key is stored.
- BusinessNote: append-only employee-attributed notes associated with the ServiceOrder. A human-readable Note is automatically appended for every successfully committed ServiceOrder action, including linked financial and handover actions; it records the server-resolved employee name snapshot, action, timestamp and concise result/reference. Notes are created atomically with their originating action and cannot be edited or deleted. No employee action-authorization token or authorization foreign key is stored.
- Financial records are owned by FIN-01; procurement records are owned by PROC-01/PROC-02; stock reservation/movement records are owned by the inventory module.

Money uses C# decimal and PostgreSQL NUMERIC(19,2) in EUR. Store event timestamps in UTC. Allocate a sequential display number per Shop using a concurrency-safe mechanism; never reuse numbers after cancellation. Retain a globally unique internal identifier independently of the display number. Version 1 user-facing UI is English only, with centralized localization resources to support future translations. All Shop-scoped records must have same-Shop composite FK/integrity enforcement where specified by DATA-02.

## 7. Candidate API contracts (not approved)

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

These are route proposals only. FIN-01 owns payment/deposit/refund APIs; PROC-01/PROC-02 own procurement APIs; SO-XFER-SPEC owns cross-Shop transfer. Request/response DTOs, pagination contract, error codes, idempotency and action codes must be finalized before implementation.

## 8. UI authorization and command invariants

- The API is authoritative for Shop ownership, Company ownership, record state and role checks.
- Do not accept ShopId, CompanyId, EmployeeId, actor role or audit actor from an untrusted request body.
- Each employee-protected command must validate the submitted employee code/password directly against the employee's `EmployeeCredential` at the start of processing. Validate active status, Company membership and action-specific profile permission before executing the command. Do not create, issue, consume, persist or reference an `EmployeeActionAuthorization`. Do not cache/reuse employee authentication. An operation that passed validation may finish if the employee is deactivated while it is in flight.
- Validate transitions against the current persisted state, with concurrency protection; never trust the status shown by a stale browser.
- Status transition, independently persisted technical outcome where applicable, resulting inventory reservation/release, immutable event and required `Notes` entry must commit atomically.
- Every successful ServiceOrder action must append exactly one corresponding `Notes` entry; Notes are a readable audit timeline, not a replacement for immutable domain or financial records.
- A `NOT_FIXED` handover changes lifecycle status to `PICKED` while retaining `technicalOutcome = NOT_FIXED`; the pickup command cannot itself create, settle or assume a refund.
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
- [ ] AC-19: Handover is accepted only from technical outcome `NOT_FIXED`, rejected while status is `FIXED`, changes lifecycle status to `PICKED` and preserves `technicalOutcome = NOT_FIXED`; EMPLOYEE, MANAGER and ADMIN may perform the action.
- [ ] AC-20: Every successfully committed ServiceOrder action appends one chronological, employee-attributed `Notes` entry in the same transaction; failed or rolled-back actions do not create entries.
- [ ] AC-21: During collection of a `NOT_FIXED` order, a `MANAGER` or `ADMIN` can execute an eligible partial/total refund of settled payments or refundable deposit balances through FIN-01; refunds are explicit, original-tender by default, capped by the unrefunded amount and are not automatic on pickup.
- [ ] AC-22: Allow `FIXED` → `IN_PROGRESS` only when `balance_due > 0`; preserve active stock reservations and do not reverse payments or inventory postings. Reject the transition when `balance_due = 0` and reserved parts have been consumed. A later rework request after settlement must use a separate corrective workflow.

## 10. Test scenarios

- Shop isolation: list, detail, update, requirement and command attempts using another Shop's ServiceOrder ID.
- Company ownership: reject customer, catalogue or Fault IDs belonging to another Company.
- Booking validation: missing required fields, missing device serial, empty Fault list, empty lines, invalid Product/Service kind, invalid quantities and invalid amounts.
- Conditional HDD bad-sector validation: qualifying Service selected with missing count; non-qualifying Service selected with the field absent; order includes both qualifying and non-qualifying Services.
- Customer/device selection: existing customer, new customer, existing device by serial, changed device data and duplicate serial conflict.
- Status concurrency: stale transition, duplicate request, illegal transition, CREATED to IN_PROGRESS only through explicit start-work, CREATED/IN_PROGRESS to AWAITING_PARTS with blocking shortage, resume before/after full reservation, `NOT_FIXED` to `IN_PROGRESS` before handover, `FIXED` to `IN_PROGRESS` before full settlement with reservations retained, rejection after full settlement/stock consumption, prohibition on resuming after pickup, `FINISHED` rejection and FIXED with blocked requirement.
- Stock settlement: partial payment, explicitly applied deposit, zero balance, negative inventory balance, idempotent retry and reservation consumption exactly once.
- Handover: allow from technical outcome `NOT_FIXED`; reject while current status is `FIXED`; on success set status `PICKED` and preserve `technicalOutcome = NOT_FIXED`; test EMPLOYEE, MANAGER and ADMIN; verify no implicit handover on payment; verify Notes entry and no order-detail mutation.
- Notes traceability: verify creation, editing, diagnosis, status, parts, payment, deposit, refund, handover, cancellation and transfer actions each append one actor/time/action entry; verify entries are immutable and not created for failed/rolled-back commands.
- NOT_FIXED collection refunds: validate partial/full refund of eligible settled payments and held refundable deposits through FIN-01, `MANAGER`/`ADMIN` challenge, original-tender allocation, deposit/payment refundable caps, duplicate prevention and no automatic refund when status becomes PICKED.
- Authorization: EMPLOYEE/MANAGER/ADMIN profile matrix per action; fresh employee code/password challenge for every protected command; inactive/wrong-Company employee rejection; no cached authentication or EmployeeActionAuthorization token; server-resolved actor attribution; ShopSession-only reads.
- Audit: status change plus event/note atomicity, immutable prior lines/faults and event chronology.
- Cancellation/refund: allow cancellation from CREATED, IN_PROGRESS and AWAITING_PARTS with and without payments/reservations; reject cancellation from FIXED, NOT_FIXED and PICKED; verify no automatic refund or deposit forfeiture, explicit FIN-01 financial actions, reservation release, separate return of consumed parts, partial refund, refund cap and `MANAGER`/`ADMIN` authorization.

## 11. Owner decisions received — 2026-11-19

- **SO-OPEN-01 — RESOLVED:** New ServiceOrders start in `CREATED`.
- **SO-OPEN-02 — PARTIALLY RESOLVED:** Device serial number is mandatory. The inspected legacy form also requires brand, model, at least one Fault and at least one Product/Service line; no contrary change was requested.
- **SO-OPEN-03 — RESOLVED:** Each ServiceOrder references exactly one Device; serial number is mandatory but not Company-unique. The operator may explicitly select/reuse an existing Device.
- **SO-OPEN-04 — RESOLVED:** `badSector` means HDD bad-sector count. Each Service catalogue record has a `requiresBadSectorCount` flag, default `false`. The count is mandatory when any selected Service has the flag set to `true`; otherwise it is optional. It must be a whole number greater than or equal to zero, with no additional upper bound defined in this SPEC.
- **SO-OPEN-05 — RESOLVED:** The ServiceOrder workflow must not edit Customer master data. Equipment/service information may be edited subject to lifecycle restrictions. Payment and handover actions must not modify order details and may only append a Note alongside their own financial/handover event. Product/Service line changes are blocked after the first stock reservation or recorded payment, whichever occurs first.
- **SO-OPEN-07 — RESOLVED:** Handover is allowed only for technical outcome `NOT_FIXED` and is prohibited while current status is `FIXED`. Handover changes lifecycle status to `PICKED` while preserving `technicalOutcome = NOT_FIXED`. An outstanding balance or held deposit does not block handover.
- **SO-OPEN-08 — RESOLVED:** EMPLOYEE, MANAGER and ADMIN may mark an order `NOT_FIXED` and hand over a `NOT_FIXED` order. Cancellation and financial operations remain restricted to MANAGER/ADMIN where specified by this SPEC and FIN-01.
- **SO-OPEN-11 — RESOLVED:** Every successfully committed action in the ServiceOrder context appends an immutable, chronological, employee-attributed `Notes` entry; the structured event/financial record remains authoritative.
- **SO-OPEN-12 — RESOLVED:** When collecting a `NOT_FIXED` order, eligible settled payments and refundable deposit balances may be refunded through FIN-01. Refund remains a separate, explicit `MANAGER`/`ADMIN` action, not an automatic pickup side effect; original-tender and refundable-balance controls apply.
- **SO-OPEN-07a — RESOLVED:** A `NOT_FIXED` device may be handed over with an outstanding balance or held deposit. Handover does not settle, waive or automatically refund the balance; FIN-01 continues to govern the financial records.

## 12. Product-owner decisions — all resolved

- [x] **SO-OPEN-03 — Device identity — RESOLVED:** Each ServiceOrder references exactly one Device. Serial number is mandatory but not Company-unique; duplicate serial values are allowed. During booking, the operator may explicitly select/reuse an existing Device record.
- [x] **SO-OPEN-04a — Bad-sector trigger — RESOLVED:** Configure `requiresBadSectorCount` as a boolean on each Service catalogue record, default `false`. Require the ServiceOrder count when any selected Service has the flag set to `true`; otherwise it is optional. Accept whole numbers greater than or equal to zero; no additional upper bound is defined in this SPEC.
- [x] **SO-OPEN-05a — Line and price history — RESOLVED:** Product/Service lines cannot be added, edited, removed or repriced after the first stock reservation or after any payment is recorded (including a partial payment), whichever happens first. Before that point, line changes are permitted and must preserve the required history. Issued document snapshots remain governed by REP-01.
- [x] **SO-OPEN-06 — State machine — RESOLVED:** `FINISHED` is not used in the target lifecycle. `NOT_FIXED` may return to `IN_PROGRESS` for another repair attempt until handover. Cancellation is allowed only from `CREATED`, `IN_PROGRESS` or `AWAITING_PARTS`, even with payments or stock reservations; required financial and stock adjustments follow FIN-01 and the stock module. Cancellation is prohibited from `FIXED`, `NOT_FIXED` and `PICKED`. Refunds are financial events, not lifecycle statuses.
- [x] **SO-OPEN-07a — Handover and balance — RESOLVED:** A `NOT_FIXED` device may be handed over with an outstanding balance or held deposit. Handover sets `PICKED` without settling, waiving or automatically refunding any amount. Financial balances remain under FIN-01; eligible refunds are separate explicit `MANAGER`/`ADMIN` actions.
- [x] **SO-OPEN-09 — Cancellation and deposits — RESOLVED:** Cancellation does not automatically refund payments, retain/forfeit deposits or create financial adjustments. Refunds, deposit applications/forfeitures and other financial consequences are separate explicit actions governed by FIN-01. Applicable stock reservations are released; already-consumed parts follow the inventory module's separate physical-return workflow.
- [x] **SO-OPEN-10 — Numbering and language — RESOLVED:** Each Shop has its own sequential ServiceOrder display-number sequence. Each order also has a globally unique internal identifier. Display numbers are never reused, including after cancellation, and allocation must be concurrency-safe. English is the only v1 UI language; localization remains centralized for future translations.

## 13. Technical reconciliation findings — 2026-11-20

- **Employee profiles:** all `TECHNICIAN` references in the prior SO-01 draft are superseded. Supported profiles are exactly `EMPLOYEE`, `MANAGER` and `ADMIN`; `ADMIN` inherits `MANAGER`. This SPEC grants the operational ServiceOrder actions in BR-04 to `EMPLOYEE`.
- **Employee command authentication:** every protected command requires a fresh employee code/password challenge. The backend resolves the employee and validates active status, Company membership and action permission at command start. No employee identity/role is added to `ShopSession` or OIDC tokens. No `EmployeeActionAuthorization` token, entity, endpoint, foreign key or consumption step is used. Events and Notes use the server-resolved employee identity/name snapshot.
- **Read access:** list/detail/search remain ShopSession-scoped and do not require employee challenge; protected commands require the challenge described above.
- **Customer integrity:** ServiceOrder edits may update the order's Device and operational details, but never mutate the linked Customer master record.
- **State-machine boundary:** booking remains `CREATED` until an explicit start-work command; a blocking shortage may move it directly to `AWAITING_PARTS`, and receiving/reserving parts never starts work automatically. `NOT_FIXED` may return to `IN_PROGRESS` before handover. The legacy `Undo Fixed` action is preserved with the approved constraint: `FIXED` may return to `IN_PROGRESS` only while a balance remains due; reservations stay active. After full settlement consumes the reserved parts, direct reversal is rejected and later rework requires a separate corrective workflow. `FINISHED` and `REFUNDED` are not target lifecycle statuses.
- **Shared architecture documents:** `domain-model.md`, `database-model.md` and authentication persistence drafts still contain superseded `TECHNICIAN`/`EmployeeActionAuthorization` designs. Their global reconciliation is required before technical approval and implementation; this SO-01 update does not claim those shared documents have been fully migrated.
- **Numbering/persistence:** enforce a globally unique internal ID and unique `(shop_id, display_number)`. Allocate the next Shop-scoped display number atomically in the order-creation transaction; never reuse allocated numbers. Before the line lock, persist line/fault changes as logical updates/removals and append before/after snapshots to ServiceOrderEvent; do not hard-delete historical child rows. Physical schema/migration details remain subject to DATA-05 review.

## 14. Approval

- Product approval: COMPLETE — all product-owner questions recorded in this SPEC are resolved.
- Technical approval: PENDING — complete shared architecture/persistence reconciliation, finalize the full transition matrix, DTOs, persistence constraints and command transactions.
- Implementation authorization: NOT GRANTED — implementation remains blocked until technical approval and required legacy/architecture reconciliation are complete.



## Simplified Parts to Buy view — owner decision

The Shop-scoped Parts to Buy list presents two visibly distinct sections or source labels: (1) **ServiceOrder demand**, automatically calculated uncovered Product-part requirements grouped by Product and broken down by originating ServiceOrder (order number, customer/device context and quantity still needed); and (2) **Manual entries**, reminders for additional parts not planned on a ServiceOrder. Keep these sources separate and never combine manual quantities into calculated ServiceOrder demand totals. Order both sections by creation date descending (newest first): manual entries by entry creation time; calculated Product groups by the newest contributing ServiceOrder creation time, with each group's contributing orders also ordered newest first. A manual entry may select an existing Product or use a free-text description for a part not yet in the catalogue. Its quantity defaults to `1` and may be changed to any positive whole number. Existing Products display all external reference links maintained centrally on the Product catalogue record. Free-text entries may store multiple external reference URLs directly on the manual entry. A manual entry can be permanently deleted; deletion affects only that manual entry and its entry-owned reference URLs, and does not modify ServiceOrder requirements, events or Notes. Links are informational only and do not represent tracked supplier or purchase records. The automatic ServiceOrder portion recalculates when requirements, stock availability or reservations change; manual entries remain separately identifiable. Employees use the list to see what parts need replenishment and buy externally using their preferred channel. The application does not record suppliers, purchase orders, external purchases, delivery/ETA, or supplier receipts.
