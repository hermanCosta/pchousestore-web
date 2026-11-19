# SO-01 — Service Orders

**Status:** DRAFT — legacy discovery partially complete; lifecycle and booking rules require product-owner decisions. No implementation is authorized by this draft.  
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
- The order is initially created with status IN_PROGRESS, a creation timestamp, total, due amount, bad-sector integer and free-text note.
- Device records contain brand, model and serial number. The booking flow searches for an existing Device by serial number and reuses it when found. No serial-number uniqueness constraint was confirmed by this source inspection.
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

## 5. Proposed redesign (REDESIGN)

### 5.1 Main workflow

1. Find or register the Company customer using CAT-01.
2. Register or select the device associated with the job, subject to the decisions in Section 11.
3. Create the ServiceOrder in the active Shop with reported Faults, Product/Service lines, customer-facing description and technician notes.
4. For each required Product part, create a ServicePartRequirement. Reserve available quantity as appropriate. If a blocking requirement remains short, move the order to AWAITING_PARTS and create a procurement request through PROC-02.
5. Record diagnosis, work notes and technical findings as append-only events/notes attributed to the employee who authorized each action.
6. When blocking requirements are fully reserved, explicitly resume the order to IN_PROGRESS.
7. When repair is complete, an authorized employee records FIXED or NOT_FIXED. FIXED is rejected while a blocking requirement remains short.
8. Receive payment/deposit application through FIN-01. When the financial balance is fully settled, consume reserved Product parts in the same transaction as the settlement.
9. Record customer collection as a distinct handover event. Do not infer handover from a payment event.
10. Cancellation/refund and any physical return are separate, permission-gated workflows. Apply the rules in FIN-01 and the stock-return policy.

### 5.2 Target status vocabulary

The following statuses are referenced by the target architecture, but this table is not yet an approved complete state machine:

- IN_PROGRESS
- AWAITING_PARTS
- FIXED
- NOT_FIXED
- PICKED
- REFUNDED
- CANCELLED

The final treatment of legacy CREATED and FINISHED, and the permitted transitions involving NOT_FIXED, PICKED, REFUNDED and CANCELLED, must be resolved before implementation. Do not infer transitions from enum membership alone.

### 5.3 Proposed UI

- **Service Orders list:** search by order ID, customer, contact, email, device brand/model and serial; filter by status and creation date after the filter policy is approved.
- **Create Service Order:** select/create customer, capture device details, select one or more Faults, add Product/Service lines, and record initial notes.
- **Service Order detail:** show customer/device identity, order status, reported faults, line items, payment/deposit summary supplied by FIN-01, part requirements/reservations, notes and append-only event history.
- **Technical actions:** diagnosis, technical note, add/update part requirement, resume from AWAITING_PARTS, Fixed/Not Fixed outcomes and customer handover as separately authorized actions.
- **Settings/navigation:** proposed primary navigation entry is Service Orders; the exact placement/order must be confirmed against the current application shell before UI implementation.
- All user-facing labels, localization and date/number formats must follow the product's global language/localization decision; this feature does not introduce a new language policy.

Loading, empty, validation, conflict, authorization-denied and retry states must be specified for each command before implementation.

## 6. Data requirements

Use the logical entities already recorded in domain-model.md and database-model.md; do not create a competing physical schema in this feature SPEC.

- ServiceOrder: Shop ownership, Customer, Device, status, currency and lifecycle timestamps.
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

## 9. Acceptance criteria (DRAFT)

- [ ] AC-01: A Shop can list and search only its own ServiceOrders.
- [ ] AC-02: Search supports order ID, customer identity/contact and device identity fields supported by the approved search policy.
- [ ] AC-03: An authorized employee can create a ServiceOrder using Company-owned customer/Fault/catalogue records and the active Shop context.
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

## 10. Test scenarios

- Shop isolation: list, detail, update, requirement and command attempts using another Shop's ServiceOrder ID.
- Company ownership: reject customer, catalogue or Fault IDs belonging to another Company.
- Booking validation: missing required fields, empty Fault list, empty lines, invalid Product/Service kind, invalid quantities and invalid amounts.
- Customer/device selection: existing customer, new customer, existing device by serial, changed device data and duplicate serial conflict.
- Status concurrency: stale transition, duplicate request, illegal transition, AWAITING_PARTS with shortage, resume before/after full reservation and Fixed with blocked requirement.
- Stock settlement: partial payment, explicitly applied deposit, zero balance, negative inventory balance, idempotent retry and reservation consumption exactly once.
- Pickup: payment without pickup, pickup recorded independently, attempted pickup from ineligible state and repeated handover.
- Authorization: MANAGER vs TECHNICIAN per action, missing/expired/replayed authorization, actor attribution and ShopSession-only reads.
- Audit: status change plus event/note atomicity, immutable prior lines/faults and event chronology.
- Cancellation/refund: unpaid release, post-consumption selected physical return, partial refund, refund cap and Manager-only authorization.

## 11. Open questions — product-owner decision required

- [ ] **SO-OPEN-01 — Initial status:** Should a new order start as CREATED (explicit acceptance/diagnosis pending) or IN_PROGRESS as the legacy constructor does?
- [ ] **SO-OPEN-02 — Required booking data:** Must every order include brand, model and serial number? May serial be blank for devices without one? Is one Fault and one Product/Service line always mandatory?
- [ ] **SO-OPEN-03 — Device identity:** Is serial number unique across the Company, or may different devices share/reuse a serial? Should a ServiceOrder always contain exactly one Device?
- [ ] **SO-OPEN-04 — Bad sector:** What does the legacy badSector integer represent operationally, on which device types is it used, and should it remain in the web form?
- [ ] **SO-OPEN-05 — Edit policy:** Which fields/lines may be edited after diagnosis, after a part reservation, after FIXED, after payment or after pickup? How should price changes affect historical snapshots?
- [ ] **SO-OPEN-06 — State machine:** What are the intended meanings of CREATED and FINISHED? Should NOT_FIXED be terminal, and can it be resumed? Which current statuses permit CANCELLED?
- [ ] **SO-OPEN-07 — Handover rule:** May a customer collect a device while any ServiceOrder balance remains due, or is full settlement required before PICKED?
- [ ] **SO-OPEN-08 — Roles:** May TECHNICIAN mark an order NOT_FIXED and record customer handover, or should those actions be Manager-only? Existing role policy explicitly grants technical movement to FIXED, but does not completely define NOT_FIXED/handover.
- [ ] **SO-OPEN-09 — Cancellation and deposits:** What customer/deposit terms apply when a ServiceOrder is cancelled before work, after a special-order part, after repair or after full settlement? The legal wording must remain aligned with FIN-01 and Irish consumer review.
- [ ] **SO-OPEN-10 — Language and order numbering:** Confirm user-facing language and whether the order number is sequential per Shop, Company-wide or generated by another policy.

## 12. Approval

- Product approval: PENDING — resolve SO-OPEN-01 through SO-OPEN-10.
- Technical approval: PENDING — finalize transition matrix, DTOs, persistence constraints and command transactions.
- Implementation authorization: NOT GRANTED — this DRAFT is for discovery and product decisions only.
