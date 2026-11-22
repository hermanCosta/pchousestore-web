# Feature: Cross-Shop ServiceOrder Transfer

**Status:** DRAFT — discovery recorded; initiation and acceptance permissions approved 2026-11-21; other business-critical transfer rules remain OPEN
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
| BR-07 | Define eligible statuses and whether transfer is allowed with payments, refunds, deposits, open complaints or procurement activity. | No complete transition matrix | OPEN |
| BR-08 | EMPLOYEE, MANAGER and ADMIN may initiate a transfer from the source Shop. EMPLOYEE, MANAGER and ADMIN may accept it at the destination Shop. Each protected command requires a fresh direct credential challenge from the acting employee; the backend validates active status, Company membership, the employee's role and source/destination Shop context. | Product Owner approval, 2026-11-21; employee-management authorization model | DECIDED |
| BR-09 | Define transfer cancellation/rejection, timeout and recovery after dispatch. | No evidence located | OPEN |
| BR-10 | Define mandatory event fields, reference identifiers and audit visibility/retention. | Internal event requirement exists; details absent | OPEN |

## 5. Proposed redesign (REDESIGN)

Treat transfer as an explicit audited workflow with `DRAFT -> IN_TRANSIT -> RECEIVED` (candidate states only; approval pending). Preserve one ServiceOrder identity and append source/destination events. Do not silently rewrite historical Shop ownership on settled transactions. Do not implement until BR-05 through BR-10 are resolved and this SPEC is approved.

## 6. Data requirements

Candidate records: `ServiceOrderTransfer` (order, source Shop, destination Shop, status, created/dispatched/received timestamps, employee authorizations, version) and append-only transfer events. Financial liability movement must be represented separately from cash transactions and linked to the source balances/deposit allocations. Exact schema and concurrency constraints remain OPEN pending rules above and DATA-05.

## 7. API contracts

Candidate commands: create transfer request, dispatch, accept/receive, reject/cancel where permitted. Every command must validate current order state and Shop ownership on the server and be idempotent under duplicate submission. DTO fields, routes, error codes and authorization policies are OPEN until the workflow and role decisions are approved.

## 8. UI flows and states

Candidate source-Shop flow: select eligible order and destination Shop, review outstanding balance/deposit and custody/parts summary, authorize, dispatch, then track status. Candidate destination-Shop flow: review transfer and accept physical custody, then assume operational work. Loading, stale-state, rejection and reconciliation screens depend on OPEN decisions.

## 9. Acceptance criteria

- [ ] AC-01: Transfer is restricted to two Shops in the same Company.
- [ ] AC-02: Historical settled transactions remain attributed to their original Shop.
- [ ] AC-03: Outstanding balance and eligible held deposit liability are reconciled through a non-cash audited event.
- [x] AC-04: Physical custody, device condition/observations and explicit destination acceptance are recorded before destination operational ownership begins.
- [ ] AC-05: Parts, procurement and authorization behavior match approved policies.
- [ ] AC-06: Duplicate or concurrent commands cannot create multiple transfers or move liability twice.
- [ ] AC-07: Every transition is attributable to an employee authorization and timestamp.

## 10. Test scenarios

After approval: same-Company and cross-Company validation; status eligibility; settled history preservation; liability transfer without cash movement; deposit cap/reconciliation; parts and procurement outcomes; custody acceptance; unauthorized role; duplicate and concurrent dispatch/receipt; audit event completeness; UI rejection/stale-state paths.

## 11. Open questions

- [x] What exact event establishes physical custody and destination acceptance? Destination records physical receipt, device condition and observations, then explicitly confirms acceptance; this transitions the transfer to `RECEIVED`.
- [x] Which source-Shop reservations, parts and procurement requests move, release or remain behind? Release source reservations; destination can reserve its own available stock; received parts stay at source unless physically transferred through the stock-transfer workflow; open procurement requests remain at source until supplier amendment; destination arranges remaining parts.
- [ ] Which ServiceOrder statuses and financial states are eligible?
- [x] Who can initiate/accept, and does each side require a fresh employee challenge? All three profiles may initiate at source and accept at destination; each command requires a fresh direct employee credential challenge.
- [ ] Can a dispatched transfer be rejected, cancelled or timed out, and how are liabilities reversed?
- [ ] What is the approved event/audit payload and how is reconciliation exposed?

## 12. Approval

- Product approval: PARTIAL — BR-01 through BR-06 and BR-08 decided; remaining business rules are open
- Technical approval: PENDING
- Data-model approval: PENDING
