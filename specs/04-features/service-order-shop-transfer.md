# Feature: Cross-Shop ServiceOrder Transfer

**Status:** DRAFT — BR-01 through BR-13 business rules and transfer lifecycle approved 2026-11-21; technical/data approval and audit governance remain OPEN
**Owner:** Product Owner

## 1. Objective and scope

Transfer an existing service order from one Shop to another within the same Company while preserving financial history and recording responsibility changes. This SPEC covers the Shop-to-Shop transfer command, custody, financial liability, parts/reservations and audit trail. It does not cover Product stock transfers, which have a separate lifecycle.

## 2. Legacy references

| Type | Path | Class/method | Evidence |
|---|---|---|---|
| Swing View | `views/modals/TransferingOrderModal.java` | transfer form/action | CONFIRMED by discovery: requests employee credential and target Company. |
| Controller | `controllers/ServiceOrderController.java` | `transferServiceOrderController` | CONFIRMED by discovery: delegates transfer to DAO. |
| DAO | `dao/ServiceOrderDAO.java` | `transferServiceOrderDAO` | CONFIRMED by discovery: changes Company reference on the existing order and persists an `OrderNote`. |
| Model | `models/ServiceOrder.java` | Company relationship | CONFIRMED by discovery: existing order is reassigned to destination Company. |
| Model | `models/OrderNote.java` | order note association | CONFIRMED by discovery: transfer note is persisted. |
| Enum/config | `enums/OrderStatus.java` | status values | See [service-order lifecycle discovery](../01-discovery/service-order-lifecycle.md); transfer-specific status effect is not established. |

Legacy source references are recorded in `specs/01-discovery/deep-dive-findings.md` from inspection of the legacy repository. Exact source line numbers and complete call-chain review remain to be added during final discovery.

## 3. Existing behavior (CONFIRMED)

- The desktop transfer dialog prompts for an employee credential and a target Company.
- The controller delegates to the DAO.
- The DAO changes the existing ServiceOrder's Company reference and writes an OrderNote.
- The target web requirement is transfer between Shops of one Company. The legacy Company target therefore cannot be carried over literally.

## 4. Business rules

| ID | Rule | Evidence | Status |
|---|---|---|---|
| BR-01 | Source and destination are Shops belonging to the same Company. | One-Company/multiple-Shop decision; transfer requirement | DECIDED |
| BR-02 | Destination assumes operational responsibility after an accepted transfer; future transactions use destination Shop. | Product-owner decision recorded in discovery | DECIDED |
| BR-03 | Settled transactions remain attributed to the source Shop. | Product-owner decision recorded in discovery | DECIDED |
| BR-04 | Outstanding balance and held/unapplied deposit liability move through an audited internal event and do not create a cash receipt. | Product-owner decision recorded in discovery | DECIDED |
| BR-05 | The source Shop records dispatch and the transfer enters `IN_TRANSIT`. The destination Shop must physically receive the device, record its condition and any observations, and confirm acceptance. Only then does the transfer enter `RECEIVED` and operational responsibility move to the destination. If the device does not arrive or a discrepancy is found, the transfer remains pending and responsibility does not change automatically. | Product Owner approval, 2026-11-21 | DECIDED |
| BR-06 | On transfer acceptance, release active service-part reservations at the source Shop; the destination may create new reservations only against its own available stock. Parts already physically received by the source remain there unless moved through a separately recorded physical Product stock transfer. Outstanding procurement requests remain attributed to the source Shop until formally amended with the supplier; they do not move automatically with the ServiceOrder. The destination Shop becomes responsible for arranging any parts still required to complete the service. | Product Owner approval, 2026-11-21 | DECIDED |
| BR-07 | A ServiceOrder may be transferred from lifecycle statuses `CREATED`, `IN_PROGRESS`, `AWAITING_PARTS`, `FIXED` or `NOT_FIXED`. Transfer is not allowed after physical customer handover (`PICKED`) or cancellation (`CANCELLED`). `FINISHED` is a legacy-only status and is not part of the target lifecycle. Existing payments (partial or full), deposits (held or applied), recorded refunds, open complaints and pending procurement activity do not by themselves block transfer; their existing financial and operational records remain governed by their owning feature rules and the approved transfer-liability rules. `REFUNDED` is a financial outcome/event, not a lifecycle status. | Product Owner approval, 2026-11-21; service-orders.md target lifecycle | DECIDED |
| BR-08 | EMPLOYEE, MANAGER and ADMIN may initiate a transfer from the source Shop. EMPLOYEE, MANAGER and ADMIN may accept it at the destination Shop. Each protected command requires a fresh direct credential challenge from the acting employee; the backend validates active status, Company membership, the employee's role and source/destination Shop context. | Product Owner approval, 2026-11-21; employee-management authorization model | DECIDED |
| BR-09 | Before dispatch, the source Shop may cancel the transfer. After dispatch, the source Shop cannot cancel it unilaterally. The destination Shop may reject physical receipt and must provide a reason; rejection does not automatically cancel the transfer or change ServiceOrder responsibility. The transfer remains pending until the source Shop records physical return of the device or both Shops complete and document an agreed resolution. No automatic cancellation or closure occurs due to timeout. | Product Owner approval, 2026-11-21 | DECIDED |
| BR-10 | Maintain an immutable, append-only audit history for the transfer. Each event records the transfer ID, ServiceOrder ID, source and destination Shop IDs, previous and resulting state/action, acting employee ID and employee-name snapshot, timestamp, and references to related financial-liability events. Rejection and cancellation events include the mandatory reason; destination receipt/acceptance events include the recorded device condition and observations; return or agreed-resolution events include their supporting references and outcome. Events cannot be edited or deleted. Audit visibility and retention policy remain subject to technical/data governance review. | Product Owner approval, 2026-11-21; technical visibility/retention pending | DECIDED — event payload; OPEN — visibility/retention |
| BR-11 | Destination acceptance (`RECEIVED`) must atomically update the ServiceOrder's current Shop, transfer eligible outstanding balance and held/unapplied deposit liability through a non-cash internal financial event, release source-Shop service-part reservations, update transfer state and append the required audit/ServiceOrder history. If any operation fails, the complete transaction rolls back. Settled payments remain attributed to the source Shop and are not recreated or moved. | Product Owner approval, 2026-11-21 | DECIDED |
| BR-12 | For each ServiceDeposit, transfer only its actual outstanding held/unapplied liability at acceptance, derived from immutable deposit events and applications—not the original amount received or target amount. Previously applied, refunded or forfeited amounts are excluded. `RELEASED` means administrative unhold/release only and does not extinguish the financial obligation or reduce transferable liability. Actual extinguishment is recorded as `FORFEITED`, with a mandatory reason and the authorization required by the deposit lifecycle; it reduces the liability. Preserve immutable origin Shop and all receipt/payment history; update only current responsible Shop and transfer net outstanding liability. | Product Owner approval, 2026-11-21 | DECIDED — net-liability and RELEASED/FORFEITED distinction |
| BR-13 | Each deposit is transferred independently. Calculate and move only that deposit's own outstanding held/unapplied balance; do not pool, redistribute, offset or reallocate balances between deposits. Preserve every prior application against its original deposit and its original ServiceOrder allocation. Partial applications remain historical and are not recalculated or moved to another deposit as part of Shop transfer. | Product Owner approval, 2026-11-21 | DECIDED |

## 5. Proposed redesign (REDESIGN)

Use the following approved candidate lifecycle for the transfer aggregate:

| Current state | Command / condition | Next state |
|---|---|---|
| `DRAFT` | Source dispatches the device | `IN_TRANSIT` |
| `DRAFT` | Source cancels before dispatch | `CANCELLED` |
| `IN_TRANSIT` | Destination physically receives, inspects and accepts | `RECEIVED` |
| `IN_TRANSIT` | Destination physically receives/inspects but rejects, with mandatory reason | `REJECTED` |
| `REJECTED` | Both Shops document an agreed resolution and destination still has custody; destination completes acceptance | `RECEIVED` |
| `REJECTED` | Device is dispatched back to source | `RETURN_IN_TRANSIT` |
| `RETURN_IN_TRANSIT` | Source physically receives and records the returned device | `RETURNED_TO_SOURCE` |

No other state transitions are allowed. In particular, the source cannot cancel after dispatch; a rejection alone does not return the device or change responsibility; and a rejected transfer can be accepted later only while the destination retains physical custody and both Shops record the agreed resolution. Once return dispatch begins, the destination cannot accept that same transfer. No timeout automatically changes or closes a transfer.

Only transition to `RECEIVED` transfers operational responsibility, changes the ServiceOrder's current Shop and moves the outstanding balance/held deposit liability. Those changes must commit atomically. `CANCELLED` and `RETURNED_TO_SOURCE` leave the ServiceOrder and its financial liability with the source. Preserve one ServiceOrder identity and all settled transaction attribution. This lifecycle is business-approved; exact persistence/concurrency implementation remains subject to technical and data approval.

## 6. Data requirements

### Deposit liability calculation (BR-12)

For each deposit linked to the ServiceOrder, calculate the outstanding liability from authoritative, immutable deposit ledger records inside the acceptance transaction. Do not use `target_amount` or the original receipt amount as the transfer amount.

- Start from successfully received/settled deposit funds.
- Exclude amounts already applied to the ServiceOrder, refunded, or forfeited.
- Include only the remaining amount that is still held/unapplied and represents an outstanding obligation.
- `RELEASED` is an administrative unhold/release only. It does not extinguish the obligation and must not reduce the deposit's outstanding liability or transferable amount.
- `FORFEITED` records actual extinguishment of the customer's deposit entitlement/obligation. It requires a mandatory reason and the authorization required by the deposit lifecycle; its amount reduces outstanding liability.
- Never use `RELEASED` as a synonym for forfeiture. Preserve both events in the immutable ledger.
- Transfer each deposit's net outstanding liability and update its current responsible `shop_id` to the destination; preserve immutable `origin_shop_id`, original receipt transaction, applications, refunds, forfeitures and all historical events.
- The sum of per-deposit outstanding liabilities must reconcile to the deposit-liability component recorded in the transfer's internal non-cash liability event. Do not double-count deposit applications as both deposit liability and outstanding ServiceOrder balance. Each deposit is calculated independently: never pool, offset, redistribute or reallocate one deposit's balance to another. Existing partial/full applications remain attached to the original deposit and retain their original allocation; transfer does not rewrite or replay them.

If the ledger is inconsistent, contains unresolved events, or cannot determine the outstanding liability unambiguously, acceptance must fail and roll back; it must not guess or silently write off the difference.

### Atomic acceptance and liability handoff (BR-11)

The destination's acceptance command is one database transaction. The implementation must:

1. Begin a transaction and lock the transfer aggregate and its ServiceOrder (or use an equivalent concurrency-control strategy that prevents competing transitions and duplicate liability movement).
2. Validate the current transfer state and allowed transition, expected version, source/destination relationship to the same Company, current ServiceOrder ownership/status, destination Shop context, fresh employee credential authorization, and recorded physical receipt/condition/explicit acceptance.
3. Recalculate the outstanding balance and eligible held/unapplied deposit liability from authoritative records inside the transaction; do not trust client-supplied totals. Preserve settled PaymentTransactions and their source-Shop attribution.
4. Record a non-cash, immutable internal liability-transfer event with source/destination, amounts and references to the underlying balance/deposit records. Do not create a cash receipt, cash movement or new payment transaction.
5. Release active service-part reservations at the source Shop as required by BR-06.
6. Update the ServiceOrder's current `shop_id` to the destination and update the current Shop ownership of eligible held/unapplied deposits, without changing their immutable origin or rewriting historical receipt/payment attribution.
7. Append the transfer acceptance event and the corresponding ServiceOrder history/Note, including the acting employee and financial-event references; set the transfer to `RECEIVED` and persist the resulting version.
8. Commit only after every operation succeeds. Any validation, persistence, concurrency or audit failure must roll back all changes, leaving the transfer unaccepted and all operational/financial responsibility at the source.

The command must be safe under retries and concurrent requests: a repeated acceptance must not duplicate the liability event, release stock twice, or transfer the Shop twice. Use a stable transfer/acceptance identity and a database-enforced uniqueness/concurrency invariant; exact keys, locking strategy and event-table design remain technical decisions. The financial event is a liability reassignment, not a cash transaction. Only outstanding balance and held/unapplied deposit liability move; settled payments and historical transactions remain with the source.

Candidate records: `ServiceOrderTransfer` (order, source Shop, destination Shop, status, created/dispatched/received timestamps, employee authorizations, version) and append-only transfer events. Each event must retain the approved audit payload in BR-10, including immutable employee attribution and links to related financial-liability events. Financial liability movement must be represented separately from cash transactions and linked to the source balances/deposit allocations. Exact schema, concurrency constraints, audit visibility and retention remain OPEN pending technical review and DATA-05.

## 7. API contracts

Candidate commands: create transfer request, dispatch, accept/receive, reject, agree-and-accept after rejection, dispatch return, confirm return receipt, and cancel before dispatch. Every command must validate current transfer state, physical-custody preconditions and Shop ownership on the server and be idempotent under duplicate submission. The transition table in Section 5 is exhaustive; invalid transitions must be rejected. DTO fields, routes and error codes remain OPEN pending technical approval.

## 8. UI flows and states

Source-Shop flow: create, review and dispatch; cancel only while `DRAFT`; track all in-transit, rejected and return states. Destination-Shop flow: inspect physical device, record condition/observations, then accept or reject with a reason. After rejection, both Shops may document an agreement and destination may accept only if it still has custody, or destination may dispatch the device back. Source confirms physical return. Only `RECEIVED` moves Shop and financial responsibility.

## 9. Acceptance criteria

- [ ] AC-01: Transfer is restricted to two Shops in the same Company.
- [ ] AC-02: Historical settled transactions remain attributed to their original Shop.
- [ ] AC-03: Outstanding balance and eligible held deposit liability are reconciled through a non-cash audited event.
- [x] AC-04: Physical custody, device condition/observations and explicit destination acceptance are recorded before destination operational ownership begins.
- [ ] AC-05: Parts, procurement and authorization behavior match approved policies.
- [ ] AC-06: Duplicate or concurrent commands cannot create multiple transfers or move liability twice.
- [x] AC-08: Transfer lifecycle follows only the approved transitions in Section 5; cancellation is pre-dispatch only, rejection requires a reason, and no timeout auto-closes a transfer.
- [x] AC-09: A rejected transfer can be accepted only after documented agreement while destination retains custody, or returned through `RETURN_IN_TRANSIT` and source-confirmed `RETURNED_TO_SOURCE`.
- [x] AC-10: Only `RECEIVED` changes current Shop and transfers outstanding balance/held deposit liability atomically; cancellation and return to source leave responsibility at source.
- [x] AC-11: Acceptance recalculates liability from authoritative records and commits Shop reassignment, liability event, eligible deposit ownership, source reservation release, transfer state and audit/history records in one transaction; any failure rolls back all changes.
- [x] AC-12: Retries/concurrent acceptance cannot duplicate financial liability events or stock reservation release; settled payment history remains attributed to source.
- [x] AC-13: Each deposit transfers only its ledger-derived outstanding held/unapplied liability; applied, refunded and forfeited amounts are excluded, origin and historical records remain unchanged, and unresolved ledger inconsistencies block acceptance.
- [x] AC-14: `RELEASED` is administrative only and does not reduce liability; only a valid `FORFEITED` event with mandatory reason and required authorization extinguishes liability.
- [x] AC-15: Each deposit's liability is calculated and transferred independently; no pooling, offsetting, redistribution or reallocation between deposits occurs, and historical applications remain attached to their original deposit.
- [ ] AC-07: Every transition is attributable to an employee authorization and timestamp.

## 10. Test scenarios

Test the exhaustive transition matrix, including invalid transitions; cancel-before-dispatch; prohibited cancel-after-dispatch; reject with mandatory reason; agreed acceptance after rejection while destination retains custody; prohibit acceptance after return dispatch; return dispatch and source receipt; no timeout closure; atomic Shop/liability handoff only on `RECEIVED`; forced rollback on failure at each transaction stage; retry/concurrent acceptance idempotency; no duplicate liability event or reservation release; settled history preservation; per-deposit net liability reconciliation (including partial applications, refunds, forfeitures, administrative `RELEASED` events and authorized `FORFEITED` events); verify no pooling, offset, redistribution or reallocation and preservation of historical application-to-deposit links; audit event completeness and stale-state handling.

## 11. Open questions

- [x] What exact event establishes physical custody and destination acceptance? Destination records physical receipt, device condition and observations, then explicitly confirms acceptance; this transitions the transfer to `RECEIVED`.
- [x] Which source-Shop reservations, parts and procurement requests move, release or remain behind? Release source reservations; destination can reserve its own available stock; received parts stay at source unless physically transferred through the stock-transfer workflow; open procurement requests remain at source until supplier amendment; destination arranges remaining parts.
- [x] Which ServiceOrder statuses and financial states are eligible? Eligible lifecycle statuses: `CREATED`, `IN_PROGRESS`, `AWAITING_PARTS`, `FIXED`, `NOT_FIXED`; `PICKED` and `CANCELLED` are excluded. Payments, deposits, refunds, open complaints and procurement activity do not independently block transfer.
- [x] Who can initiate/accept, and does each side require a fresh employee challenge? All three profiles may initiate at source and accept at destination; each command requires a fresh direct employee credential challenge.
- [x] Can a dispatched transfer be rejected, cancelled or timed out, and how are liabilities reversed? Source may cancel only before dispatch; destination may reject receipt with a mandatory reason; after dispatch there is no unilateral source cancellation and no automatic timeout closure. The transfer remains pending until physical return is recorded or both Shops document an agreed resolution. Financial liability does not move before destination acceptance.
- [x] What is the approved event/audit payload? Every immutable event records transfer/order IDs, both Shop IDs, action and previous/resulting state, employee ID/name snapshot, timestamp, relevant financial-event references, and action-specific reasons, device condition/observations or return/resolution references. Events cannot be edited or deleted.
- [x] What transfer states and transitions govern dispatch, acceptance, rejection and physical return? See the exhaustive lifecycle table in Section 5.
- [x] How is Shop and financial responsibility handed over? In one atomic destination-acceptance transaction as specified in BR-11 and Section 6; settled payments remain attributed to source.
- [x] Which deposit amount moves? Only each deposit's actual outstanding held/unapplied liability, net of applied/refunded/forfeited amounts. `RELEASED` is administrative and preserves liability; `FORFEITED` extinguishes it with reason and required authorization.
- [x] Are deposits pooled or reallocated during transfer? No. Each deposit is transferred independently; prior applications remain attached to the original deposit and are not redistributed.
- [ ] How are audit visibility, retention and financial reconciliation exposed? These remain technical/data-governance decisions.

## 12. Approval

- Product approval: BUSINESS RULES APPROVED — BR-01 through BR-12, transfer state lifecycle, atomic acceptance and net deposit-liability principle, RELEASED/FORFEITED distinction and independent per-deposit treatment approved 2026-11-21; audit visibility/retention governance remains open
- Technical approval: PENDING
- Data-model approval: PENDING
