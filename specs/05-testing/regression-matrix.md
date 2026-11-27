# Legacy Regression Matrix — Initial

**Status:** DRAFT / DISCOVERY

| Area | Scenario | Expected verification |
|---|---|---|
| Shop scope | A session opened for Shop A requests or mutates operational records from Shop B | Access denied or not found; no cross-shop operational access |
| Customer | Create customer with existing phone in same company | Duplicate resolution follows approved rule |
| Catalog | Search product by name, price and note | Shared Company catalogue is visible from authorized Shops |
| Sale | Add multiple line items and quantities | Correct line totals and sale total using decimal arithmetic |
| Payment | Cash exact amount | No change, correct ledger values |
| Payment | Cash overpayment | Correct change and net drawer movement |
| Payment | Card-only | Correct card settlement total |
| Payment | Combined card + cash | Gross sale counted exactly once; cash/card split reconciles |
| Payment | Underpayment | Rejected unless approved deposit/installment path |
| Refund | Sale refund by cash/card | Net totals decrease by refund amount |
| Refund | Service-order refund by cash/card | Net totals decrease by refund amount; note linked to the order |
| Deposits | Service booking receives part-preorder deposit | Receipt is recorded at booking; no stock deduction; held balance is visible |
| Deposits | Apply only part of a held deposit to an order item | Service balance reduces by applied amount only; residual deposit remains held |
| Deposits | Refund or forfeit deposit | Tender/payment and deposit-event ledgers reconcile; forfeiture requires configured approval |
| Instalments | Weekly refurbished sale plan | Schedule totals match agreed price, due dates are weekly and receipts allocate exactly once |
| Instalments | Pay one schedule row in multiple receipts | Paid/outstanding amounts remain correct and idempotent |
| Instalments | Layaway unit before final payment | Exact refurbished unit remains RESERVED; no handover or stock consumption |
| Instalments | Early handover credit-sale plan without compliance gate | Rejected; no stock/payment side effects |
| Refunds | Partial refund followed by another partial refund | Cumulative amount never exceeds original settled receipt |
| Refunds | Full refund after earlier partial refund | Only remaining refundable amount is paid out |
| Refunds | Financial refund without physical return | No stock movement occurs |
| Tender methods | Disable a tender method for a Shop | New allocations using that method are rejected; history remains reportable |
| Ireland | Cash rounding enabled for eligible €10.22 bill | Rounds final cash total to €10.20; adjustment recorded; price/tax base unchanged |
| Ireland | Card/bank transfer or mixed tender | No cash rounding |
| Service order | Open with customer, device, item and fault | Persist all associations atomically |
| Service order | Invalid status transition | Rejected with no partial update |
| Service order | Status change plus note | Both persist or neither persists |
| Service order | Pickup from FIXED after full settlement | Lifecycle becomes PICKED; technical outcome remains FIXED; no second stock movement or reservation mutation |
| Service order | Pickup from FIXED with outstanding balance | Pickup succeeds; financial balance and active reservations remain unchanged |
| Service order | Pickup from NOT_FIXED with used and unused reserved parts | Used quantity posts SERVICE_ORDER_OUT; unused reservation is released without movement; all effects are atomic |
| Service order | Pickup from NOT_FIXED with zero parts used | All active reservations are released; no stock movement is posted |
| Service order | Pickup from NOT_FIXED with no active reservations | Pickup succeeds without a stock-disposition step or stock writes |
| Inventory | Reserve a whole product quantity | Reserved increases, on-hand stays unchanged, available decreases |
| Inventory | Try fractional product quantity | Rejected by API/domain validation |
| Inventory | Cancel/release reservation | Reserved decreases, on-hand unchanged; no inbound movement |
| Inventory | Pay after reservation was released | Settlement is allowed; deduction posts the full quantity and may make on-hand/available negative |
| Inventory | Settle sale with active reservation | Reservation consumed first; full quantity deducted once; negative signed balance is retained if shortage remains |
| Inventory | Mark service order FIXED | Product reservation remains active; no stock movement |
| Inventory | Fully settle service order while it remains FIXED, then move to PICKED | Reserved product quantities are consumed at settlement once; later PICKED causes no second movement |
| Inventory | Partial payment on service order | No stock consumption; full balance settlement consumes reserved parts even before later pickup/collection |
| Inventory | Cancel unpaid sale/order | Active reservations released; no stock return movement |
| Inventory | REFUNDED/CANCELLED after consumption | Only selected product lines/quantities are restocked |
| Inventory | Select more quantity to return than consumed net of prior returns | Rejected under concurrency-safe validation |
| Inventory | Attempt negative availability | Allowed for quantity-tracked products; balance remains signed and deficit appears in reports; reservation counter remains non-negative |
| Transfer | Dispatch between two Shops | Source on-hand decreases, destination unchanged, quantity is in transit |
| Transfer | Receive full/partial transfer | Destination on-hand increases by actual quantity only; status updated |
| Transfer | Transfer to same Shop or another Company | Rejected |
| Transfer | Cancel after dispatch | Status-only cancellation rejected; reverse transfer required |
| Refurbishment | Create PC/console/TV category and attributes | Category/spec fields are data-configurable without schema change |
| Refurbishment | Register refurbished physical unit | One asset/unit code; own serial, condition, technical values and multiple private images |
| Refurbishment | Reserve/sell/transfer/refund serialized unit | Exact physical unit identity and state are preserved; quantity always one; no negative phantom unit; 12-month commercial guarantee begins at customer handover |
| Cash | Cash-in and cash-out | Included correctly in expected drawer balance |
| Closing | Close a date twice | Second close rejected for correct business scope |
| Closing | Service-order payments included | Reconciliation includes service payments |
| Closing | Same business date across multiple Shops | Each Shop can close independently for that date; closing Shop A does not block Shop B |
| Reports | Daily closing | Totals reconcile with transaction ledger and cash movements |
| Security | Login failure/credential stuffing | Source/network endpoint rate limits enforced; repeated failures never lock/disable credentials or create credential-specific rejection windows |
| Security | Sensitive operation | Actor and audit trail are recorded |

- **OAuth/OIDC Shop API security:** React completes Authorization Code + PKCE S256 using a registered redirect URI; wrong verifier, replayed code, wrong issuer/audience/signature, expired/not-yet-valid token or missing `shop.api` scope is rejected.
- **Shop-only token identity:** access/ID tokens contain the Shop principal and active ShopSession context but no EmployeeId, employee code or employee role; a revoked ShopSession is rejected before token expiry.
- **Employee challenge isolation:** a valid Shop bearer token alone cannot commit a feature-protected business operation. The backend freshly validates the acting employee's code/password, active status, Company membership and feature-specific permission for each operation; no one-use authorization token/entity/endpoint is issued, persisted, consumed or reused. Raw credentials never enter logs, request hashes, idempotency snapshots or audit payloads.
- **OAuth prohibited flows:** Implicit and Resource Owner Password Credentials grants are unavailable; no client secret or raw token is present in the React bundle, storage, logs or analytics.
- **Refresh-token rotation:** if refresh tokens are enabled, each successful refresh invalidates the prior token; reuse of an invalidated token revokes the token family.


## ServiceOrder Pickup API and transaction regression tests

- FIXED Pickup with zero, partial or outstanding balance succeeds and does not alter stock reservations, on-hand quantity or inventory movements.
- FIXED Pickup rejects any non-empty stock-disposition payload.
- NOT_FIXED Pickup accepts a complete disposition for every active reservation line; zero use releases all; partial use consumes only the used amount and releases the remainder; full use consumes the full active reservation.
- NOT_FIXED Pickup with every active reservation used at zero releases all reservations and creates no stock movement.
- NOT_FIXED Pickup with no active reservations succeeds without a stock-disposition step or stock writes.
- Reject unreserved Product quantities and reservation lines that are foreign, inactive, released, duplicated, omitted or outside the ServiceOrder/Shop.
- Require a fresh employee code/password challenge; reject invalid/inactive/wrong-Company employees and unauthorized profiles without creating business Notes/events. Confirm no employee authorization token/entity/endpoint is created or consumed.
- Verify `EMPLOYEE`, `MANAGER` and `ADMIN` can perform Pickup from both `FIXED` and `NOT_FIXED`; reject any profile not granted Pickup by the approved employee access matrix.
- Verify the generated Note text for `FIXED`, `NOT_FIXED` with used parts, `NOT_FIXED` with zero use, and `NOT_FIXED` without active reservations; timestamp remains structured metadata.
- Require an Idempotency-Key. Same key and same normalized payload returns the original committed response; same key with changed payload or resource scope returns conflict.
- A rolled-back Pickup leaves no idempotency record or business effects and can be retried.
- Distinct concurrent Pickup keys on one ServiceOrder produce one success and one conflict; no duplicate movements, reservation disposition, event or Note.
- Fault-inject each write boundary and verify atomic rollback of status, picked_at, event, Note, stock movements, reservation updates and idempotency record.
- Verify Pickup does not settle, waive or refund money and does not mutate Customer, Device, Fault or ServiceOrder lines.

## Money calculation invariants

- All amounts are calculated with decimal arithmetic.
- Every tender component belongs to one payment event.
- A combined tender contributes its due/gross amount once.
- Change is not revenue and must not inflate net cash.
- Refunds reduce net revenue and the correct payment channel.
- Report totals must be derivable from persisted financial transactions and documented formulas.

## Decision-specific tests

- **One Company / multiple Shops:** all Shops belong to the provisioned Company; each persistent session has one Shop context; operational lookups, searches and mutations remain within that Shop; tampered Shop IDs are rejected. Shared master data remains company-wide.
- **Product-only stock:** Product has a separate integer balance per Shop; Service has no stock balance and cannot be passed to inventory movement operations. No fractional product quantity is accepted.
- **Reservations:** reserve/release affects availability but not on-hand; release creates no inbound movement; consumption updates reservation counter, balance and movement atomically.
- **Sale/service stock timing:** partial payment never consumes stock. Sale settlement and `FIXED` ServiceOrder settlement consume the applicable stock exactly once when the balance reaches zero; a later `PICKED` event creates no second movement. `NOT_FIXED` ServiceOrder settlement never consumes or releases reservations: Pickup consumes only explicitly recorded used quantities from active reservations and releases the unused remainder, even if a balance remains due. Test idempotent retries and negative quantity-tracked balances only where the relevant settlement rule permits them.
- **Selected stock return:** REFUNDED/CANCELLED exposes a selected-return workflow; only selected consumed product quantities are restocked, and a refund transaction alone does not move stock.
- **Inter-Shop transfer:** dispatch removes source stock, received quantities add destination stock, in-transit stock is unavailable, only same-Company distinct Shops are allowed.
- **Refurbished inventory:** categories are configurable, attribute definitions drive technical fields, and each physical refurbished unit is uniquely serialized/tracked; transfer/sale/return use the exact unit identity.
- **Transaction payments:** a mixed cash/card event is persisted as one PaymentTransaction with two allocations; transaction gross is counted once; sum of applied allocations equals transaction amount; change is not revenue.
- **Refunds:** refund transaction stores positive magnitude, references original transaction, reduces net by its kind and cannot exceed the remaining refundable amount under concurrent requests. In v1, every refund must use the original tender; any different tender is rejected regardless of Shop policy or approval.
- **Shop authentication / employee attribution:** a Shop login creates a ShopSession with no EmployeeId; the same Company-wide employee credentials work in all Shops; profiles are `EMPLOYEE`, `MANAGER` and `ADMIN`; two employees can complete separate operations within the same ShopSession; each protected operation validates a fresh employee code/password directly against the active employee, Company membership and action permission. No employee authorization token is issued or replayed.
- **Shop/date closing:** enforce `UNIQUE(ShopId, BusinessDate)`; the same date can be closed once in each Shop. Test concurrent attempts to close the same Shop/date (exactly one succeeds), successful closes for two different Shops on the same date, and exclusion of transactions/cash movements from other Shops.


- **Refurbished commercial guarantee:** every sold refurbished physical unit receives exactly one 12-calendar-month commercial guarantee starting at recorded customer handover; reservation, booking, quote and deposit timestamps do not start the term. The customer-facing terms never imply that statutory Irish consumer rights are limited or replaced.
- **VAT feature switch:** Company-level `vat_enabled` is switchable on/off; default false and setup `NOT_REVIEWED`; disabled flag is not interpreted as a legal exemption; tax calculation/document finalization requires reviewed configuration.

## Parts to Buy decision tests

- Calculated ServiceOrder demand includes only uncovered Product-part requirements; group by Product while retaining each contributing ServiceOrder and its separate whole-number quantity.
- Manual reminders remain a distinct source from ServiceOrder demand. Never merge manual quantities into calculated demand or automatically merge/deduplicate manual entries.
- Sort both sources newest first: manual entries by creation timestamp; ServiceOrder demand groups by the newest contributing ServiceOrder, with contributing orders also newest first.
- Search updates as the operator types, is case-insensitive and supports partial Product-name matches in calculated demand and Product-backed manual entries, plus free-text description matches in manual entries.
- Verify the correct empty states: `No parts found` when a search has no matches; `No parts to buy yet` when both sources are empty while keeping manual-entry creation available; and `No items` within an individually empty source section.
- Manual entries may reference an existing Product or use a free-text description, default quantity to 1, and allow only positive whole-number quantities.
- Duplicate manual entries, including repeated Product references or identical free-text descriptions, remain independent with separate quantities and reference URLs.
- Manual entries persist until explicitly deleted, regardless of stock availability or ServiceOrder reservations. Stock changes affect calculated demand only.
- Manual entries can be edited and permanently deleted; quantity edits require no justification or extra confirmation, while deletion is a separate explicit action.
- Trim leading/trailing whitespace from descriptions and URL values; ignore blank URL rows. A no-op edit creates no audit entry. Deleting one manual entry does not affect other entries, ServiceOrder requirements, events or Notes.
- Reference URLs are informational and operator-managed. Do not check availability/reachability or apply special URL validation; an unreachable link must not hide or disable its entry.
- No supplier selection, purchase records, receipt workflow, delivery tracking or procurement image workflow exists in v1.

## Additional finance-specific decision tests

- A `DEPOSIT_RECEIPT` during ServiceOrder booking is recorded under the current Shop and employee; it is not automatically a service payment or stock movement.
- A `DepositApplication` is a non-cash credit to its own ServiceOrder and cannot exceed the available held balance.
- A settled payment or explicit `DepositApplication` that reduces a `FIXED` ServiceOrder balance to zero atomically persists the financial record, balance update, Stock domain consumption, `InventoryMovement`, events, employee-attributed `BusinessNote` and committed idempotency result in one PostgreSQL transaction; fault injection at each boundary rolls back every effect.
- Partial payment and unapplied deposit do not consume stock. `NOT_FIXED` payment settlement or deposit application never consumes/releases reservations; only Pickup performs its approved stock disposition.
- Every committed ServiceOrder financial action (payment, deposit receipt/application, payment refund, deposit refund) appends exactly one employee-attributed `BusinessNote` in the same transaction; rollback/rejection leaves no Note.
- Payment settlement and deposit application idempotent replay returns the original committed response without duplicating ledger, stock, event or Note effects; rolled-back attempts can be retried.
- `SECURITY` deposits do not become revenue or order payment automatically; refund/forfeiture must be separately authorized.
- Every `InstallmentSettlement` references a settled payment and the same Sale as its InstallmentPlan. Schedule and sale balances reconcile without duplicate gross revenue.
- `LAYAWAY` is the default plan fulfillment behavior. `CREDIT_SALE` is blocked unless an explicit deployment compliance feature flag and approved policy are in place.
- A `REFUND` references an original settled incoming `PAYMENT`; refunds can be partial/multiple but not exceed the original refundable balance under concurrency. A ServiceOrder payment refund is exposed/accepted only when a settled payment exists, refundable remainder is positive, technical outcome is `NOT_FIXED`, and lifecycle status is `NOT_FIXED` or `PICKED`.
- A `DEPOSIT_REFUND` references the original deposit receipt and is validated against the deposit's refundable held balance/lifecycle; it is not treated as a `REFUND` or deducted from a payment's refundable remainder.
- `MANAGER` and `ADMIN` can execute eligible payment and deposit refunds using fresh credentials; `EMPLOYEE` cannot. Deposit forfeiture remains `MANAGER`-only; verify that `ADMIN` is rejected for forfeiture despite inheriting the general Manager permission set.
- EUR amount persistence uses decimal arithmetic and two decimal currency precision. Cash rounding is a separate signed adjustment, voluntary/Shop-configured and only for eligible final cash amounts. VAT tests cover Company feature toggle, tax setup READY gate, effective-date selection and historical tax snapshots.
- Cash drawer calculations include cash deposit receipts/refunds; electronic tender and provider payout/fees are reconciled separately.


## New decision-specific tests — v11

- One Company-wide Employee record/password can authorize actions in every Shop; no per-Shop employee duplicate is created.
- `EMPLOYEE`, `MANAGER` and `ADMIN` are Company-wide single profiles; API authorization is evaluated per action and the approved feature permission matrix rejects disallowed actions.
- Quantity-tracked `quantity_on_hand` accepts negative signed values; `quantity_reserved` cannot be negative; available quantity may be negative; negative deficits are visible in stock/purchasing reports.
- Reservation creation only reserves currently positive available units; a subsequent fully settled sale/service may drive the same product balance below zero without deleting existing reservation history.
- A Sale or `FIXED` ServiceOrder with partial payments posts no stock movement; when the balance reaches zero, applicable product quantities are posted once in the settlement transaction. For `NOT_FIXED` ServiceOrders, payment settlement never consumes or releases reservations; Pickup consumes only explicitly used reserved quantities and releases unused reservations, independently of balance due.
- Serialized refurbished units cannot be sold/transferred as phantom negative stock.
- Each sold refurbished unit creates a 12-calendar-month commercial warranty starting at recorded customer handover; the customer-facing terms explicitly state that statutory rights are unaffected.
- VAT toggle is Company-scoped. `NOT_REVIEWED` tax setup blocks production tax-document finalization; enabling VAT requires active effective-dated tax codes/rates; flag/rate changes do not alter historical tax snapshots.

## Product-owner confirmations — v11

- An employee can enter the same password and authorize an allowed operation in any Shop of the Company; the persistent ShopSession contains no employee identity.
- `EMPLOYEE`, `MANAGER` and `ADMIN` are Company-wide single profiles. A user with an allowed profile can act in every Shop; an action outside the approved feature permission policy is rejected. `ADMIN` inherits the full `MANAGER` permission set.
- Payment completion is the inventory posting trigger for Sales and `FIXED` ServiceOrders: partial receipts do not deduct quantity-tracked stock; the command that reduces the balance to zero posts applicable deductions once. `NOT_FIXED` ServiceOrders are the exception: payment settlement does not consume/release reservations; physical Pickup posts only used reserved quantities and releases the rest.
- A quantity-tracked Product balance may become negative after a completed sale/service settlement; it must be persisted and appear as a visible deficit. Reservation quantity remains non-negative; serialized refurbished units cannot be negative.
- `vat_enabled` can be toggled at Company level. The default is false and tax setup remains `NOT_REVIEWED` until configured and reviewed. No production tax document can be finalized with `NOT_REVIEWED`; changing the flag does not rewrite historical tax snapshots.
- Every sold refurbished physical unit receives a 12-month commercial guarantee starting on actual handover. Customer terms do not present this guarantee as replacing or limiting Irish statutory rights.


## Product-owner confirmation tests — v13

- Employee authorization resolves a Company-wide employee using the configured employee identifier plus password; password-only lookup must not attribute an action to an ambiguous employee.
- The same active employee identity/password and Company-wide role assignment works across all Shops; `ShopSession` stores no employee identity.
- `EMPLOYEE`/`MANAGER`/`ADMIN` profile names are recognized globally; enforce the approved feature-specific action permissions and do not infer permissions from UI visibility.
- Each sold refurbished unit snapshots a 12-calendar-month commercial guarantee from actual handover; customer wording and receipts must not imply that the guarantee replaces Irish statutory rights.
- VAT enable/disable is a Company-scoped feature flag; `NOT_REVIEWED` is distinct from disabled, and neither state is treated as a legal conclusion.


## Reports and document printing

- Generate the service-order intake form and verify customer, equipment, faults, items, estimate, notes and authorisation area.
- Generate a service payment receipt for a partial payment; the receipt states the amount actually settled and does not imply that the order is fully paid.
- Generate a sale receipt and a refurbished-sale receipt; verify Shop identity, item lines, EUR totals and CASH/CARD breakdown.
- Generate a 12-month refurbished guarantee certificate from actual handover date; the output never contains the legacy six-month text and preserves the statutory-rights notice.
- Generate deposit and refund receipts only after their transactions are settled. A deposit application must not be counted or receipted as a second cash receipt.
- Generate a daily closing report and reconcile it to the Shop's cash ledger; CASH and CARD are displayed separately.
- Generate both A4 and 80 mm PDFs and verify no clipping, missing fields or unreadable totals.
- Cross-Shop document retrieval/generation is rejected even when the client supplies another Shop's SourceId.
- Historical reprint uses the stored snapshot and pinned template version; it appends one `DocumentReprintEvent` and does not duplicate sale/payment/refund/stock/cash transactions.
- Reprinting a voided/corrected document shows the correct copy/void status and cannot mutate the original snapshot.
- VAT-off rendering omits tax calculation and does not assert legal exemption; VAT-on rendering requires a valid effective-dated tax snapshot.
- PDF generation uses `Europe/Dublin`, `en-IE` formatting and EUR currency.


## Approved cash-closing policy tests

- Expected physical cash equals opening float plus settled cash receipts net of change, plus posted cash-in, minus posted cash-out, settled cash refunds, settled deposit refunds and safe drops.
- Card, online, bank-transfer and store-credit allocations never affect physical drawer cash; electronic tender is reported separately.
- A settled cash refund is deducted once through its refund allocation and is not also recorded as a duplicate cash-out movement.
- Deposit receipt is counted when cash is actually received; applying an existing deposit to an order balance creates no second cash receipt.
- Cash rounding uses actual tender less change; the separate rounding adjustment is not applied to the drawer formula a second time.
- Only `MANAGER` and `ADMIN` can open/close the cash day and create/approve closing corrections, using fresh employee credentials; `EMPLOYEE` is rejected.
- Enforce one `DailyClosing` per Shop/business date. Concurrent close attempts for the same Shop/date produce exactly one successful close; another Shop can close independently.
- A non-zero variance requires a reason. Closing preserves the original expected/count/variance snapshot; corrections append a separately visible record and never rewrite the original close.
