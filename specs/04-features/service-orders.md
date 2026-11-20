# SO-01 — Service Orders

**Status:** DRAFT — owner decisions updated 2026-11-19; the remaining lifecycle, edit and data-policy questions still require resolution. No implementation is authorized by this draft.  
**Type:** Shop-scoped operational workflow.  
**Depends on:** AUTH-01/AUTH-02, CAT-01, DATA-02, approved employee access matrix, stock and financial policies.  
**Related work:** PROC-01/PROC-02 (parts procurement), FIN-01 (payments/deposits/refunds), SO-XFER-SPEC (cross-Shop transfer), REP-01 (documents and receipts).

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
- The form requests an employee password before creating the order. This is legacy behavior only; the target uses employee code + password and a fresh one-use EmployeeActionAuthorization.
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
| BR-03 | Each committed employee-protected command requires the executing employee's fresh code/password challenge and a short-lived, one-use authorization consumed atomically with the business write. | employee-access-and-roles.md | DECIDED |
| BR-04 | MANAGER has full access. TECHNICIAN may create/edit ServiceOrders and perform approved technical/service actions; cancellation, payment, deposit forfeiture and refund remain Manager-only where the matrix assigns them. | employee-access-and-roles.md | DECIDED |
| BR-05 | A part requirement is linked to a Product ServiceOrderItem and uses a positive whole-unit quantity. SERVICE catalogue lines never create stock requirements. | stock-and-refurbishment.md; ADR-013 | DECIDED |
| BR-06 | If any blocking part requirement is not fully reserved, the ServiceOrder enters AWAITING_PARTS. It can originate during booking/diagnosis or later work. | ADR-013; domain-model.md | DECIDED |
| BR-07 | When all blocking requirements are fully reserved, an employee-authorized explicit resume command returns the order to IN_PROGRESS; receipt alone does not restart repair. | ADR-013 | DECIDED |
| BR-08 | FIXED requires all blocking requirements to be fully reserved and retains active reservations while the repaired device awaits payment/collection. | ADR-006; stock-and-refurbishment.md | DECIDED |
| BR-09 | Settled payments and explicitly applied deposits reducing the ServiceOrder balance to zero consume reserved Product parts atomically. Partial payments and unapplied deposits do not consume stock. | ADR-006; financial-model.md | DECIDED |
| BR-10 | Physical pickup is a separate event from payment completion and stock consumption. PICKED must not be set merely because a payment was recorded. | ADR-006; product-owner-decisions.md | DECIDED |
| BR-11 | Financial deposits, applications, refunds and forfeitures follow FIN-01/financial-model.md. This SPEC must not introduce a parallel balance or deposit ledger. | financial-model.md | DECIDED |
| BR-12 | Financial refund and physical stock return are separate. Only selected, physically returned quantities may enter the stock-return workflow; inspection/disposition rules apply. | architecture-decisions.md; stock-and-refurbishment.md | DECIDED |
| BR-13 | Service-order sourcing rounds and supplier purchases belong to PROC-01/PROC-02; they remain linked to the durable ServicePartRequirement. | service-parts-procurement.md | DECIDED |
| BR-14 | Cross-Shop ServiceOrder transfer follows SO-XFER-SPEC and is not implemented as a Company reassignment shortcut. | service-order-shop-transfer.md | DECIDED |
| BR-15 | A newly created ServiceOrder starts in `CREATED`, not `IN_PROGRESS`. | Product-owner decision 2026-11-19 | DECIDED |
| BR-16 | Device serial number is mandatory at ServiceOrder booking. Brand and model remain required as in the inspected legacy booking form. Serial-number uniqueness across the Company is not decided by this rule. | Product-owner decision 2026-11-19; NewServiceOrderView | DECIDED |
| BR-17 | `badSector` represents the device HDD bad-sector count. Each Service catalogue record has a `requiresBadSectorCount` flag, defaulting to `false`. The field is mandatory when at least one selected Service has this flag set to `true`; otherwise it is optional. The count must be a whole number greater than or equal to zero. No additional upper bound is defined in this SPEC. | Product-owner clarification 2026-11-19; legacy `spn_bad_sectors` | DECIDED WITH CONFIGURATION OPEN |
| BR-18 | ServiceOrder workflows must not edit the linked Customer's master data. Equipment and service information may be edited through the ServiceOrder edit workflow; Customer master-data changes remain in the Customers feature. Payment and handover actions must not modify those details and may only append Notes. | Product-owner decision 2026-11-19 | DECIDED |
| BR-19 | Payment and customer-handover actions must not modify Customer, Device, Fault or ServiceOrder line details. They may append a separate Note; financial and handover records remain their own immutable events. | Product-owner decision 2026-11-19 | DECIDED |
| BR-20 | Customer handover is allowed only from `NOT_FIXED` and is prohibited while the current order status is `FIXED`. On handover, the lifecycle status changes to `PICKED` while the technical outcome `NOT_FIXED` is preserved independently. | Product-owner decision 2026-11-19 | DECIDED |
| BR-21 | Both `MANAGER` and `TECHNICIAN` have the same permission to mark a ServiceOrder `NOT_FIXED` and to record handover of a `NOT_FIXED` order. This does not change the existing Manager-only payment, cancellation, deposit-forfeiture or refund permissions. | Product-owner decision 2026-11-19; employee-access-and-roles.md | DECIDED |
| BR-22 | Every successfully committed action performed in the ServiceOrder context must append a chronological, employee-attributed entry to `Notes`. The entry describes the action and its result; entries are append-only. The structured ServiceOrder/financial event remains authoritative and the corresponding Note must be committed atomically with its action. Rejected or rolled-back actions must not create Notes. | Product-owner decision 2026-11-19; legacy OrderNote workflow | DECIDED |
| BR-23 | When a `NOT_FIXED` order is handed over, eligible amounts already received for the order, including refundable deposit balances, may be refunded through FIN-01. A refund is a separate explicit financial action, not an automatic consequence of handover; it remains Manager-only, follows the original-tender and refundable-balance rules, and must be recorded in `Notes`. | Product-owner decision 2026-11-19; FIN-01 | DECIDED |
| BR-24 | A `NOT_FIXED` device may be handed over even when the ServiceOrder has an outstanding balance or held deposit. Handover changes the lifecycle status to `PICKED` but does not settle, waive or automatically refund the outstanding amount. The financial balance and deposit remain governed by FIN-01; any eligible refund is a separate explicit Manager-only action. | Product-owner decision 2026-11-20 | DECIDED |
| BR-25 | Each ServiceOrder is associated with exactly one Device. Device serial number is mandatory but is not Company-unique; duplicate serial values are allowed. The operator may explicitly select/reuse an existing Device record during booking. | Product-owner decision 2026-11-20 | DECIDED |\n| BR-26 | Product/Service lines cannot be added, edited, removed or repriced after the first stock reservation for the order or after any payment has been recorded (including partial payment), whichever occurs first. Before either event, line changes are permitted and must preserve the required history; issued document snapshots remain governed by REP-01. | Product-owner decision 2026-11-20 | DECIDED |\n| BR-27 | `NOT_FIXED` is resumable to `IN_PROGRESS` while the device has not been handed over. `FINISHED` is not used in the target lifecycle. Cancellation is permitted only from `CREATED`, `IN_PROGRESS` or `AWAITING_PARTS`, including when payments or reservations exist; any required financial and stock adjustments must be completed through FIN-01 and the stock module. Cancellation is not permitted from `FIXED`, `NOT_FIXED` or `PICKED`. | Product-owner decision 2026-11-20 | DECIDED |\n| BR-28 | ServiceOrder cancellation does not automatically refund payments, retain/forfeit deposits or create other financial adjustments. All refunds, deposit applications/forfeitures and related financial consequences are explicit actions governed exclusively by FIN-01. Cancellation releases applicable stock reservations; parts already consumed follow the inventory module's separate physical-return workflow. | Product-owner decision 2026-11-20 | DECIDED |

## 5. Proposed redesign (REDESIGN)

### 5.1 Main workflow

1. Find or register the Company customer using CAT-01. The ServiceOrder workflow references the Customer and must not edit the Customer master record.
2. Register or select the device associated with the job. Brand, model and serial number are required; serial-number uniqueness is still to be decided.
3. Create the ServiceOrder in the active Shop with initial status `CREATED`, reported Faults, Product/Service lines, customer-facing description and technician notes.
4. Capture the HDD bad-sector count when at least one selected Service has `requiresBadSectorCount = true` (operating-system installation is a confirmed example). The flag defaults to `false` for every Service; validate the count as a whole number greater than or equal to zero.
5. For each required Product part, create a ServicePartRequirement. Reserve available quantity as appropriate. If a blocking requirement remains short, move the order to `AWAITING_PARTS` and create a procurement request through PROC-02.
6. Record diagnosis, work notes and technical findings as append-only events/notes attributed to the employee who authorized each action. Every successfully committed ServiceOrder action (creation, edit, diagnosis, technical result, status change, parts/reservation/procurement action, payment/deposit/refund, handover, cancellation or transfer) must append a human-readable `Notes` entry with the action, timestamp, employee and concise result/reference. Entries cannot be edited or deleted. The corresponding structured event/financial record remains authoritative; the Note and action commit atomically. Equipment and service information may be edited through the ServiceOrder's edit workflow; Customer master data may not. Product/Service lines may be added, edited, removed or repriced only until the first stock reservation or first recorded payment, whichever happens first; after that point all line and price changes are blocked. Payment and handover actions remain notes-only with respect to order details.
7. When blocking requirements are fully reserved, explicitly resume the order to `IN_PROGRESS`.
8. When repair is complete, an authorized Manager or Technician may record `FIXED` or `NOT_FIXED`. `FIXED` is rejected while a blocking requirement remains short.
9. Receive payments, record deposits, apply deposits and process refunds through FIN-01. Each committed financial action associated with the ServiceOrder appends a `Notes` entry; financial ledger records remain authoritative. Payment/refund actions must not edit Customer, Device, Fault or ServiceOrder line details. When the financial balance is fully settled, consume reserved Product parts in the same transaction as the settlement.
10. Customer handover is a separate action and is allowed only when the order's technical outcome is `NOT_FIXED`; it is blocked while the current status is `FIXED`. An outstanding balance or held deposit does not block handover. On successful handover, set the lifecycle status to `PICKED` and preserve `technicalOutcome = NOT_FIXED` independently. Handover does not settle, waive or automatically refund any financial balance. Both MANAGER and TECHNICIAN may perform this handover. Append the handover action to `Notes`; do not edit order details.
11. During collection of a `NOT_FIXED` order, a MANAGER may separately initiate a refund of eligible settled payments and refundable deposit balances through FIN-01. This is optional and explicit, not automatic on pickup. Apply original-tender, refundable-balance and deposit-lifecycle rules; never refund the same funds twice. Record the refund in `Notes`. Physical return/stock handling remains separate and follows the stock-return policy.
12. `NOT_FIXED` may return to `IN_PROGRESS` for another repair attempt, provided the device has not been handed over. Cancellation is allowed only while the order is `CREATED`, `IN_PROGRESS` or `AWAITING_PARTS`; it may still be cancelled when payments or stock reservations exist, but all required financial and stock adjustments must be handled through FIN-01 and the stock module. Cancellation is not allowed from `FIXED`, `NOT_FIXED` or `PICKED`. Physical stock return remains a separate, permission-gated workflow.

### 5.2 Target status vocabulary

A newly created order starts in `CREATED`. The statuses below are referenced by the target architecture, but this table is not yet an approved complete state machine:

- CREATED
- IN_PROGRESS
- AWAITING_PARTS
- FIXED
- NOT_FIXED
- PICKED
- REFUNDED
- CANCELLED

Owner decisions already made: `CREATED` is the initial target status; handover is allowed only from technical outcome `NOT_FIXED` and prohibited while the current status is `FIXED`; handover changes the lifecycle status to `PICKED` and preserves `technicalOutcome = NOT_FIXED` independently; both MANAGER and TECHNICIAN may record this handover. `PICKED` therefore represents physical collection, not payment completion. `FINISHED`, the full transition matrix and the permitted transitions involving `REFUNDED` and `CANCELLED` remain open. Do not infer transitions from enum membership alone.

### 5.3 Proposed UI

- **Service Orders list:** search by order ID, customer, contact, email, device brand/model and serial; filter by status and creation date after the filter policy is approved.
- **Create Service Order:** select or create a Customer through CAT-01, capture required device brand/model/serial, select one or more Faults, add Product/Service lines, and record initial notes. Display the HDD bad-sector count and require it only when a selected Service is configured to require it.
- **Service Order detail:** show customer/device identity, order status, reported faults, line items, payment/deposit summary supplied by FIN-01, part requirements/reservations, notes and append-only event history.
- **Technical actions:** diagnosis, technical note, add/update part requirement, resume from AWAITING_PARTS, Fixed/Not Fixed outcomes and customer handover as separately authorized actions.
- **Settings/navigation:** proposed primary navigation entry is Service Orders; the exact placement/order must be confirmed against the current application shell before UI implementation.
- All user-facing labels, localization and date/number formats must follow the product's global language/localization decision; this feature does not introduce a new language policy.

Loading, empty, validation, conflict, authorization-denied and retry states must be specified for each command before implementation.

## 6. Data requirements

Use the logical entities already recorded in domain-model.md and database-model.md; do not create a competing physical schema in this feature SPEC.

- ServiceOrder: Shop ownership, Customer, exactly one Device, initial status `CREATED`, lifecycle status, independently persisted technical outcome (including `NOT_FIXED` after status becomes `PICKED`), handover timestamp, currency, lifecycle timestamps and the integer HDD bad-sector count (`badSector` in the legacy model), conditionally required by selected Service.
- ServiceOrderItem: Product/Service line, whole-unit quantity for Product items, and monetary snapshot values as finalized by DATA-05. Each Service catalogue record includes `requiresBadSectorCount` (boolean, default `false`) to indicate whether selecting that Service makes the ServiceOrder HDD bad-sector count mandatory. Line additions, edits, removals and repricing are blocked after the first stock reservation or first recorded payment, whichever occurs first; issued document snapshots follow REP-01.
- ServiceOrderFault: links the order to Company-shared Fault catalogue entries.
- ServicePartRequirement: durable Product demand, required quantity, blocking flag and reservation state.
- ServiceOrderEvent: append-only status and operational event history with Shop, actor Employee, action authorization, previous/new state and timestamp.
- BusinessNote: append-only employee-attributed notes associated with the ServiceOrder. A human-readable Note is automatically appended for every successfully committed ServiceOrder action, including linked financial and handover actions; it records action, timestamp, actor and concise result/reference. Notes are created atomically with their originating action and cannot be edited or deleted.
- Financial records are owned by FIN-01; procurement records are owned by PROC-01/PROC-02; stock reservation/movement records are owned by the inventory module.

Money uses C# decimal and PostgreSQL NUMERIC(19,2) in EUR. Store event timestamps in UTC. All Shop-scoped records must have same-Shop composite FK/integrity enforcement where specified by DATA-02.

## 7. Candidate API contracts (not approved)

| Method / route | Purpose | Authorization |
|---|---|---|
| GET /api/v1/service-orders | Shop-scoped paginated list and search | ShopSession |
| GET /api/v1/service-orders/{serviceOrderId} | Read order detail and history | ShopSession; same-Shop check |
| POST /api/v1/service-orders | Create order and initial related records | Employee challenge; MANAGER or TECHNICIAN |
| PUT /api/v1/service-orders/{serviceOrderId} | Update permitted order/customer/device/line data | Employee challenge; MANAGER or TECHNICIAN, subject to edit-state rules |
| POST /api/v1/service-orders/{serviceOrderId}/status-transitions | Execute an allowed status command | Employee challenge; action-specific role |
| POST /api/v1/service-orders/{serviceOrderId}/notes | Add a technical/operational note | Employee challenge; action-specific role |
| POST /api/v1/service-orders/{serviceOrderId}/part-requirements | Add/update required Product parts | Employee challenge; MANAGER or TECHNICIAN |
| POST /api/v1/service-orders/{serviceOrderId}/resume | Explicitly resume from AWAITING_PARTS | Employee challenge; MANAGER or TECHNICIAN |

These are route proposals only. FIN-01 owns payment/deposit/refund APIs; PROC-01/PROC-02 own procurement APIs; SO-XFER-SPEC owns cross-Shop transfer. Request/response DTOs, pagination contract, error codes, idempotency and action codes must be finalized before implementation.

## 8. UI authorization and command invariants

- The API is authoritative for Shop ownership, Company ownership, record state and role checks.
- Do not accept ShopId, CompanyId, EmployeeId, actor role or audit actor from an untrusted request body.
- Each employee-protected command must consume one fresh action authorization in the same database transaction as the business operation and append its event.
- Validate transitions against the current persisted state, with concurrency protection; never trust the status shown by a stale browser.
- Status transition, independently persisted technical outcome where applicable, resulting inventory reservation/release, immutable event and required `Notes` entry must commit atomically.
- Every successful ServiceOrder action must append exactly one corresponding `Notes` entry; Notes are a readable audit timeline, not a replacement for immutable domain or financial records.
- A `NOT_FIXED` handover changes lifecycle status to `PICKED` while retaining `technicalOutcome = NOT_FIXED`; the pickup command cannot itself create, settle or assume a refund.
- A refund during/around collection is a separate Manager-only FIN-01 action, limited to amounts still refundable under the original payment/deposit records; it must not duplicate or exceed a prior refund.
- Payment settlement must be idempotent and must consume reservations at most once.
- Preserve order, line and fault history. Do not delete/recreate child rows as a substitute for editing.
- Reject cross-Shop identifiers even when the Company-level customer or catalogue record is otherwise visible.
- A ServiceOrder command must not mutate the linked Customer master record. Customer updates use the separate CAT-01 customer workflow.
- Payment and handover commands may append a Note but must not mutate customer, device, fault or service-line details.

## 9. Acceptance criteria (DRAFT)

- [ ] AC-01: A Shop can list and search only its own ServiceOrders.
- [ ] AC-02: Search supports order ID, customer identity/contact and device identity fields supported by the approved search policy.
- [ ] AC-03: An authorized employee can create a ServiceOrder using Company-owned customer/Fault/catalogue records and the active Shop context; the initial status is `CREATED`.
- [ ] AC-03a: Device brand, model and serial number are required at booking; a blank serial number is rejected.
- [ ] AC-04: The order records one or more approved Faults and Product/Service lines, subject to the final booking validation rules.
- [ ] AC-05: Device and serial validation follows the approved identity/reuse policy.
- [ ] AC-06: Order updates preserve historical line/fault references and create auditable events rather than hard-deleting prior rows. Product/Service lines can be changed only before the first stock reservation or first recorded payment, whichever occurs first; all line additions, edits, removals and repricing are rejected afterward.
- [ ] AC-07: The UI shows the current status and append-only event/notes history.
- [ ] AC-08: A blocking Product requirement that is not fully reserved moves the order to AWAITING_PARTS.
- [ ] AC-09: Only an explicit authorized command resumes an order when all blocking requirements are fully reserved.
- [ ] AC-10: An order cannot become FIXED while a blocking requirement is short.
- [ ] AC-11: Full financial settlement consumes reserved Product quantities atomically; partial payment or unapplied deposit does not.
- [ ] AC-12: Payment completion does not mark the device as physically collected.
- [ ] AC-13: Cancellation is allowed only from CREATED, IN_PROGRESS or AWAITING_PARTS, including orders with payments or reservations; required adjustments are handled by FIN-01 and the stock module. Cancellation does not itself refund payments or retain/forfeit deposits; all such financial actions are explicit and governed by FIN-01. Applicable reservations are released, while consumed parts follow the inventory module's separate physical-return workflow. Cancellation, refund and physical stock return remain separately auditable.
- [ ] AC-14: Cross-Shop transfer is routed to SO-XFER-SPEC and cannot be implemented through direct Shop/Company reassignment.
- [ ] AC-15: Replayed or concurrent status/payment commands cannot cause duplicate events, stock movements or reservation consumption.
- [ ] AC-16: Every Service catalogue record defaults `requiresBadSectorCount` to `false`; the ServiceOrder HDD bad-sector count is required if at least one selected Service has the flag set to `true`, and otherwise remains optional. The value accepts whole numbers greater than or equal to zero.
- [ ] AC-17: ServiceOrder editing never mutates Customer master data. Equipment/service information can be edited through the order edit workflow, separately from payment and handover actions.
- [ ] AC-18: Payment and handover commands do not alter Customer, Device, Fault or ServiceOrder line details; they append the corresponding `Notes` entry.
- [ ] AC-19: Handover is accepted only from technical outcome `NOT_FIXED`, rejected while status is `FIXED`, changes lifecycle status to `PICKED` and preserves `technicalOutcome = NOT_FIXED`; MANAGER and TECHNICIAN have equal permission.
- [ ] AC-20: Every successfully committed ServiceOrder action appends one chronological, employee-attributed `Notes` entry in the same transaction; failed or rolled-back actions do not create entries.
- [ ] AC-21: During collection of a `NOT_FIXED` order, a MANAGER can execute an eligible partial/total refund of settled payments or refundable deposit balances through FIN-01; refunds are explicit, original-tender by default, capped by the unrefunded amount and are not automatic on pickup.

## 10. Test scenarios

- Shop isolation: list, detail, update, requirement and command attempts using another Shop's ServiceOrder ID.
- Company ownership: reject customer, catalogue or Fault IDs belonging to another Company.
- Booking validation: missing required fields, missing device serial, empty Fault list, empty lines, invalid Product/Service kind, invalid quantities and invalid amounts.
- Conditional HDD bad-sector validation: qualifying Service selected with missing count; non-qualifying Service selected with the field absent; order includes both qualifying and non-qualifying Services.
- Customer/device selection: existing customer, new customer, existing device by serial, changed device data and duplicate serial conflict.
- Status concurrency: stale transition, duplicate request, illegal transition, AWAITING_PARTS with shortage, resume before/after full reservation, `NOT_FIXED` to `IN_PROGRESS` before handover, prohibition on resuming after pickup, `FINISHED` rejection and FIXED with blocked requirement.
- Stock settlement: partial payment, explicitly applied deposit, zero balance, negative inventory balance, idempotent retry and reservation consumption exactly once.
- Handover: allow from technical outcome `NOT_FIXED`; reject while current status is `FIXED`; on success set status `PICKED` and preserve `technicalOutcome = NOT_FIXED`; test MANAGER and TECHNICIAN; verify no implicit handover on payment; verify Notes entry and no order-detail mutation.
- Notes traceability: verify creation, editing, diagnosis, status, parts, payment, deposit, refund, handover, cancellation and transfer actions each append one actor/time/action entry; verify entries are immutable and not created for failed/rolled-back commands.
- NOT_FIXED collection refunds: validate partial/full refund of eligible settled payments and held refundable deposits through FIN-01, Manager-only challenge, original-tender allocation, deposit/payment refundable caps, duplicate prevention and no automatic refund when status becomes PICKED.
- Authorization: MANAGER vs TECHNICIAN per action, missing/expired/replayed authorization, actor attribution and ShopSession-only reads.
- Audit: status change plus event/note atomicity, immutable prior lines/faults and event chronology.
- Cancellation/refund: allow cancellation from CREATED, IN_PROGRESS and AWAITING_PARTS with and without payments/reservations; reject cancellation from FIXED, NOT_FIXED and PICKED; verify no automatic refund or deposit forfeiture, explicit FIN-01 financial actions, reservation release, separate return of consumed parts, partial refund, refund cap and Manager-only authorization.

## 11. Owner decisions received — 2026-11-19

- **SO-OPEN-01 — RESOLVED:** New ServiceOrders start in `CREATED`.
- **SO-OPEN-02 — PARTIALLY RESOLVED:** Device serial number is mandatory. The inspected legacy form also requires brand, model, at least one Fault and at least one Product/Service line; no contrary change was requested.
- **SO-OPEN-03 — RESOLVED:** Each ServiceOrder references exactly one Device; serial number is mandatory but not Company-unique. The operator may explicitly select/reuse an existing Device.
- **SO-OPEN-04 — RESOLVED:** `badSector` means HDD bad-sector count. Each Service catalogue record has a `requiresBadSectorCount` flag, default `false`. The count is mandatory when any selected Service has the flag set to `true`; otherwise it is optional. It must be a whole number greater than or equal to zero, with no additional upper bound defined in this SPEC.
- **SO-OPEN-05 — PARTIALLY RESOLVED:** The ServiceOrder workflow must not edit Customer master data. Equipment/service information may be edited subject to lifecycle restrictions. Payment and handover actions must not modify order details and may only append a Note alongside their own financial/handover event. The line/price lock point is resolved in SO-OPEN-05a.
- **SO-OPEN-07 — PARTIALLY RESOLVED:** Handover is allowed only for technical outcome `NOT_FIXED` and is prohibited while current status is `FIXED`. Handover changes lifecycle status to `PICKED` while preserving `technicalOutcome = NOT_FIXED`. Whether an outstanding balance blocks handover remains open.
- **SO-OPEN-08 — RESOLVED:** MANAGER and TECHNICIAN have the same permission to mark an order `NOT_FIXED` and to record a handover for a `NOT_FIXED` order. This does not change Manager-only payment, cancellation, deposit-forfeiture or refund permissions.
- **SO-OPEN-11 — RESOLVED:** Every successfully committed action in the ServiceOrder context appends an immutable, chronological, employee-attributed `Notes` entry; the structured event/financial record remains authoritative.
- **SO-OPEN-12 — RESOLVED:** When collecting a `NOT_FIXED` order, eligible settled payments and refundable deposit balances may be refunded through FIN-01. Refund remains a separate, explicit Manager-only action, not an automatic pickup side effect; original-tender and refundable-balance controls apply.
- **SO-OPEN-07a — RESOLVED:** A `NOT_FIXED` device may be handed over with an outstanding balance or held deposit. Handover does not settle, waive or automatically refund the balance; FIN-01 continues to govern the financial records.

## 12. Open questions — product-owner decision required

- [x] **SO-OPEN-03 — Device identity — RESOLVED:** Each ServiceOrder references exactly one Device. Serial number is mandatory but not Company-unique; duplicate serial values are allowed. During booking, the operator may explicitly select/reuse an existing Device record.
- [x] **SO-OPEN-04a — Bad-sector trigger — RESOLVED:** Configure `requiresBadSectorCount` as a boolean on each Service catalogue record, default `false`. Require the ServiceOrder count when any selected Service has the flag set to `true`; otherwise it is optional. Accept whole numbers greater than or equal to zero; no additional upper bound is defined in this SPEC.
- [x] **SO-OPEN-05a — Line and price history — RESOLVED:** Product/Service lines cannot be added, edited, removed or repriced after the first stock reservation or after any payment is recorded (including a partial payment), whichever happens first. Before that point, line changes are permitted and must preserve the required history. Issued document snapshots remain governed by REP-01.
- [x] **SO-OPEN-06 — State machine — RESOLVED:** `FINISHED` is not used in the target lifecycle. `NOT_FIXED` may return to `IN_PROGRESS` for another repair attempt until handover. Cancellation is allowed only from `CREATED`, `IN_PROGRESS` or `AWAITING_PARTS`, even with payments or stock reservations; required financial and stock adjustments follow FIN-01 and the stock module. Cancellation is prohibited from `FIXED`, `NOT_FIXED` and `PICKED`. Refunds are financial events, not lifecycle statuses.
- [x] **SO-OPEN-07a — Handover and balance — RESOLVED:** A `NOT_FIXED` device may be handed over with an outstanding balance or held deposit. Handover sets `PICKED` without settling, waiving or automatically refunding any amount. Financial balances remain under FIN-01; eligible refunds are separate explicit Manager-only actions.
- [x] **SO-OPEN-09 — Cancellation and deposits — RESOLVED:** Cancellation does not automatically refund payments, retain/forfeit deposits or create financial adjustments. Refunds, deposit applications/forfeitures and other financial consequences are separate explicit actions governed by FIN-01. Applicable stock reservations are released; already-consumed parts follow the inventory module's separate physical-return workflow.
- [ ] **SO-OPEN-10 — Numbering and language:** Should the order number be sequential per Shop, Company-wide or generated by another policy? Confirm whether English remains the only v1 UI language.

## 12. Approval

- Product approval: PARTIAL — owner decisions recorded 2026-11-19 and 2026-11-20, including device identity/reuse, configurable bad-sector requirement (default disabled), line/price lock after first stock reservation or recorded payment, lifecycle rules (`NOT_FIXED` resumable, `FINISHED` retired, cancellation state restrictions), cancellation/deposit handling delegated to FIN-01 and stock module, `NOT_FIXED` to `PICKED` handover representation, independent preservation of technical outcome, handover with outstanding balance, Notes traceability and permission to refund eligible payments/deposits at collection; resolve the remaining open questions above.
- Technical approval: PENDING — finalize transition matrix, DTOs, persistence constraints and command transactions.
- Implementation authorization: NOT GRANTED — this DRAFT is for discovery and product decisions only.
