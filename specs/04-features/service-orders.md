# SO-01 — Service Orders

**Status:** DRAFT — owner decisions partially recorded 2026-11-19; the remaining lifecycle, edit and data-policy questions still require resolution. No implementation is authorized by this draft.  
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
| BR-17 | `badSector` represents the device HDD bad-sector count. It is conditionally mandatory only when a selected Service requires this measurement; operating-system installation is a confirmed example. The exact Service trigger list/configuration and numeric bounds remain open. | Product-owner clarification 2026-11-19; legacy `spn_bad_sectors` | DECIDED WITH CONFIGURATION OPEN |
| BR-18 | ServiceOrder workflows must not edit the linked Customer's master data. Equipment and service information may be edited through the ServiceOrder edit workflow; Customer master-data changes remain in the Customers feature. Payment and handover actions must not modify those details and may only append Notes. | Product-owner decision 2026-11-19 | DECIDED |
| BR-19 | Payment and customer-handover actions must not modify Customer, Device, Fault or ServiceOrder line details. They may append a separate Note; financial and handover records remain their own immutable events. | Product-owner decision 2026-11-19 | DECIDED |
| BR-20 | Customer handover is permitted for an order whose technical outcome is `NOT_FIXED`; handover is prohibited while the order status is `FIXED`. Eligibility from other states, balance-due treatment and persisted post-handover status/event representation remain open. | Product-owner decision 2026-11-19 | PARTIALLY DECIDED |
| BR-21 | Both `MANAGER` and `TECHNICIAN` have the same permission to mark a ServiceOrder `NOT_FIXED` and to record handover of a `NOT_FIXED` order. This does not change the existing Manager-only payment, cancellation, deposit-forfeiture or refund permissions. | Product-owner decision 2026-11-19; employee-access-and-roles.md | DECIDED |

## 5. Proposed redesign (REDESIGN)

### 5.1 Main workflow

1. Find or register the Company customer using CAT-01. The ServiceOrder workflow references the Customer and must not edit the Customer master record.
2. Register or select the device associated with the job. Brand, model and serial number are required; serial-number uniqueness is still to be decided.
3. Create the ServiceOrder in the active Shop with initial status `CREATED`, reported Faults, Product/Service lines, customer-facing description and technician notes.
4. Capture the HDD bad-sector count when at least one selected Service requires it (operating-system installation is a confirmed example). Do not make this field universally mandatory until its service-trigger configuration is defined.
5. For each required Product part, create a ServicePartRequirement. Reserve available quantity as appropriate. If a blocking requirement remains short, move the order to `AWAITING_PARTS` and create a procurement request through PROC-02.
6. Record diagnosis, work notes and technical findings as append-only events/notes attributed to the employee who authorized each action. Equipment and service information may be edited through the ServiceOrder's edit workflow; Customer master data may not. Payment and handover actions are separate notes-only interactions for order details.
7. When blocking requirements are fully reserved, explicitly resume the order to `IN_PROGRESS`.
8. When repair is complete, an authorized Manager or Technician may record `FIXED` or `NOT_FIXED`. `FIXED` is rejected while a blocking requirement remains short.
9. Receive payment/deposit application through FIN-01. Payment actions must not edit Customer, Device, Fault or ServiceOrder line details; a separate Note may be appended. When the financial balance is fully settled, consume reserved Product parts in the same transaction as the settlement.
10. Customer handover is a separate action. It is allowed for `NOT_FIXED` and blocked for `FIXED`; it must not be inferred from payment. A handover may append a Note but must not edit order details.
11. Cancellation/refund and any physical return are separate, permission-gated workflows. Apply the rules in FIN-01 and the stock-return policy.

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

Owner decisions already made: `CREATED` is the initial target status; `NOT_FIXED` orders may be handed over to the customer; `FIXED` orders may not be handed over; both MANAGER and TECHNICIAN may perform the `NOT_FIXED` and eligible handover actions. The final treatment of `FINISHED`, whether `NOT_FIXED` remains the technical outcome after handover or transitions to a separate `PICKED` status, and the permitted transitions involving `REFUNDED` and `CANCELLED` remain open. Do not infer transitions from enum membership alone.

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

- ServiceOrder: Shop ownership, Customer, Device, initial status `CREATED`, currency, lifecycle timestamps and the integer HDD bad-sector count (`badSector` in the legacy model), conditionally required by selected Service.
- ServiceOrderItem: Product/Service line, whole-unit quantity for Product items, and monetary snapshot values as finalized by DATA-05.
- ServiceOrderFault: links the order to Company-shared Fault catalogue entries.
- ServicePartRequirement: durable Product demand, required quantity, blocking flag and reservation state.
- ServiceOrderEvent: append-only status and operational event history with Shop, actor Employee, action authorization, previous/new state and timestamp.
- BusinessNote: append-only employee-attributed notes associated with the ServiceOrder.
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
- Status transition, resulting inventory reservation/release when applicable, immutable event and required note must commit atomically.
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
- [ ] AC-06: Order updates preserve historical line/fault references and create auditable events rather than hard-deleting prior rows.
- [ ] AC-07: The UI shows the current status and append-only event/notes history.
- [ ] AC-08: A blocking Product requirement that is not fully reserved moves the order to AWAITING_PARTS.
- [ ] AC-09: Only an explicit authorized command resumes an order when all blocking requirements are fully reserved.
- [ ] AC-10: An order cannot become FIXED while a blocking requirement is short.
- [ ] AC-11: Full financial settlement consumes reserved Product quantities atomically; partial payment or unapplied deposit does not.
- [ ] AC-12: Payment completion does not mark the device as physically collected.
- [ ] AC-13: Cancellation, refund and physical stock return obey Manager/stock/finance rules and are separately auditable.
- [ ] AC-14: Cross-Shop transfer is routed to SO-XFER-SPEC and cannot be implemented through direct Shop/Company reassignment.
- [ ] AC-15: Replayed or concurrent status/payment commands cannot cause duplicate events, stock movements or reservation consumption.
- [ ] AC-16: The HDD bad-sector count is required when a selected Service is configured to require it (including the confirmed operating-system installation example); it is not globally mandatory for unrelated Services.
- [ ] AC-17: ServiceOrder editing never mutates Customer master data. Equipment/service information can be edited through the order edit workflow, separately from payment and handover actions.
- [ ] AC-18: Payment and handover commands do not alter Customer, Device, Fault or ServiceOrder line details; they may append a Note.
- [ ] AC-19: Handover is accepted from `NOT_FIXED` and rejected from `FIXED`; MANAGER and TECHNICIAN have equal permission for these two actions.

## 10. Test scenarios

- Shop isolation: list, detail, update, requirement and command attempts using another Shop's ServiceOrder ID.
- Company ownership: reject customer, catalogue or Fault IDs belonging to another Company.
- Booking validation: missing required fields, missing device serial, empty Fault list, empty lines, invalid Product/Service kind, invalid quantities and invalid amounts.
- Conditional HDD bad-sector validation: qualifying Service selected with missing count; non-qualifying Service selected with the field absent; order includes both qualifying and non-qualifying Services.
- Customer/device selection: existing customer, new customer, existing device by serial, changed device data and duplicate serial conflict.
- Status concurrency: stale transition, duplicate request, illegal transition, AWAITING_PARTS with shortage, resume before/after full reservation and Fixed with blocked requirement.
- Stock settlement: partial payment, explicitly applied deposit, zero balance, negative inventory balance, idempotent retry and reservation consumption exactly once.
- Handover: allow from `NOT_FIXED`; reject from `FIXED`; test roles MANAGER and TECHNICIAN; verify no implicit handover on payment; verify Note append without mutating order details.
- Authorization: MANAGER vs TECHNICIAN per action, missing/expired/replayed authorization, actor attribution and ShopSession-only reads.
- Audit: status change plus event/note atomicity, immutable prior lines/faults and event chronology.
- Cancellation/refund: unpaid release, post-consumption selected physical return, partial refund, refund cap and Manager-only authorization.

## 11. Owner decisions received — 2026-11-19

- **SO-OPEN-01 — RESOLVED:** New ServiceOrders start in `CREATED`.
- **SO-OPEN-02 — PARTIALLY RESOLVED:** Device serial number is mandatory. The inspected legacy form also requires brand, model, at least one Fault and at least one Product/Service line; no contrary change was requested.
- **SO-OPEN-04 — PARTIALLY RESOLVED:** `badSector` means HDD bad-sector count. It is mandatory only for selected Services that require the measurement; operating-system installation is a confirmed example. The exact trigger list/configuration and numeric bounds remain open.
- **SO-OPEN-05 — PARTIALLY RESOLVED:** The ServiceOrder workflow must not edit Customer master data. Equipment/service information may be edited subject to lifecycle restrictions. Payment and handover actions must not modify order details and may only append a Note alongside their own financial/handover event. The exact lock point after a payment is recorded remains open.
- **SO-OPEN-07 — PARTIALLY RESOLVED:** Handover is allowed when the technical outcome is `NOT_FIXED` and prohibited while the order status is `FIXED`. Whether an outstanding balance blocks a `NOT_FIXED` handover, and whether the technical outcome remains `NOT_FIXED` or transitions to a separate `PICKED` status, remain open.
- **SO-OPEN-08 — RESOLVED:** MANAGER and TECHNICIAN have the same permission to mark an order `NOT_FIXED` and to record a handover for a `NOT_FIXED` order. This does not change Manager-only payment, cancellation, deposit-forfeiture or refund permissions.

## 12. Open questions — product-owner decision required

- [ ] **SO-OPEN-03 — Device identity:** Is serial number unique across the Company, or may different devices share/reuse a serial? Should the target keep exactly one Device per ServiceOrder?
- [ ] **SO-OPEN-04a — Bad-sector trigger:** Which catalogue Services require HDD bad-sector count (operating-system installation is confirmed), and should this be configured on each Service catalogue record? What numeric range/validation is valid?
- [ ] **SO-OPEN-05a — Line and price history:** Can Product/Service lines or their prices be changed after a part reservation or partial/settled payment? How must the original line values and issued document snapshots be preserved?
- [ ] **SO-OPEN-06 — State machine:** What does `FINISHED` mean in the target? Is `NOT_FIXED` terminal or resumable? Which states allow cancellation?
- [ ] **SO-OPEN-07a — Handover and balance:** Can a `NOT_FIXED` device be handed over with an outstanding balance or held deposit, or must financial settlement/refund handling happen first?
- [ ] **SO-OPEN-07b — Handover representation:** After handover of a `NOT_FIXED` order, should the technical status remain `NOT_FIXED` with a separate immutable handover event/timestamp, or should it transition to `PICKED` while preserving the technical outcome separately?
- [ ] **SO-OPEN-09 — Cancellation and deposits:** What customer/deposit terms apply when a ServiceOrder is cancelled before work, after a special-order part, after repair or after full settlement? The legal wording must remain aligned with FIN-01 and Irish consumer review.
- [ ] **SO-OPEN-10 — Numbering and language:** Should the order number be sequential per Shop, Company-wide or generated by another policy? Confirm whether English remains the only v1 UI language.

## 12. Approval

- Product approval: PARTIAL — owner decisions recorded 2026-11-19; resolve the remaining open questions above.
- Technical approval: PENDING — finalize transition matrix, DTOs, persistence constraints and command transactions.
- Implementation authorization: NOT GRANTED — this DRAFT is for discovery and product decisions only.
