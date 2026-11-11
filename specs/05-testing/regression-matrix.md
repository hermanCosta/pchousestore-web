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
| Service order | Mark picked after payment | One transition and one payment effect only |
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
- **Employee challenge isolation:** a valid Shop bearer token alone cannot commit a protected business operation; the operation requires a fresh employee code/password challenge and a one-time authorization. An employee challenge cannot be reused for another action or operation context.
- **OAuth prohibited flows:** Implicit and Resource Owner Password Credentials grants are unavailable; no client secret or raw token is present in the React bundle, storage, logs or analytics.
- **Refresh-token rotation:** if refresh tokens are enabled, each successful refresh invalidates the prior token; reuse of an invalidated token revokes the token family.

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
- **Sale/service stock timing:** a completed payment means the balance due reaches zero through settled payments (and explicitly applied deposits for service orders). Partial payment does not consume stock. A ServiceOrder in FIXED preserves reservations until full settlement consumes them; later PICKED records handover only. Test idempotent retries and negative stock balances.
- **Selected stock return:** REFUNDED/CANCELLED exposes a selected-return workflow; only selected consumed product quantities are restocked, and a refund transaction alone does not move stock.
- **Inter-Shop transfer:** dispatch removes source stock, received quantities add destination stock, in-transit stock is unavailable, only same-Company distinct Shops are allowed.
- **Refurbished inventory:** categories are configurable, attribute definitions drive technical fields, and each physical refurbished unit is uniquely serialized/tracked; transfer/sale/return use the exact unit identity.
- **Transaction payments:** a mixed cash/card event is persisted as one PaymentTransaction with two allocations; transaction gross is counted once; sum of applied allocations equals transaction amount; change is not revenue.
- **Refunds:** refund transaction stores positive magnitude, references original transaction, reduces net by its kind and cannot exceed the remaining refundable amount under concurrent requests.
- **Shop authentication / employee attribution:** a Shop login creates a ShopSession with no EmployeeId; the same Company-wide employee password works in all Shops; MANAGER/TECHNICIAN roles are global; two employees can complete separate operations within the same ShopSession; each operation consumes a new authorization matching active Shop, Company, role and action; replay, expiry and wrong-action use are rejected.
- **Shop/date closing:** enforce `UNIQUE(ShopId, BusinessDate)`; the same date can be closed once in each Shop. Test concurrent attempts to close the same Shop/date (exactly one succeeds), successful closes for two different Shops on the same date, and exclusion of transactions/cash movements from other Shops.


- **Refurbished commercial guarantee:** every sold refurbished physical unit receives exactly one 12-calendar-month commercial guarantee starting at recorded customer handover; reservation, booking, quote and deposit timestamps do not start the term. The customer-facing terms never imply that statutory Irish consumer rights are limited or replaced.
- **Image uploads:** multiple private images can be uploaded to each service procurement request line and refurbished physical unit; verify Shop authorization, actor audit, content/signature validation, generated storage keys, soft deletion and no public/guessable access.
- **Procurement approvals:** creating, editing, linking and receiving a supplier purchase requires the acting employee password/action permission but never a separate manager approval or approval-state transition.
- **VAT feature switch:** Company-level `vat_enabled` is switchable on/off; default false and setup `NOT_REVIEWED`; disabled flag is not interpreted as a legal exemption; tax calculation/document finalization requires reviewed configuration.

## Service parts procurement decision tests

- Adding a product part requirement with sufficient Shop availability can reserve the whole quantity; on-hand remains unchanged and available stock decreases.
- Adding a blocking part requirement with no/insufficient availability moves the order to `AWAITING_PARTS`; existing available units remain reserved and only the shortage is procured.
- One SupplierPurchase may include lines allocated to multiple ServiceOrders; one requirement may be sourced from multiple purchase lines. Allocation totals cannot exceed ordered/required quantities.
- Purchase record stores supplier/source, order reference/link, product page link, order date, ETA and tracking link/reference; every ETA/tracking/status change creates an immutable timeline event attributed to an employee authorization.
- A partial receipt increases ShopInventory only by actual accepted quantity, posts one `PURCHASE_RECEIPT_IN` per accepted quantity and reserves allocated units for the correct ServiceOrder atomically. Duplicate receipt submission does not post stock twice.
- Excess quantity received beyond service allocations becomes available Shop stock; it is not silently assigned to an order.
- An order can enter `AWAITING_PARTS` from `CREATED` immediately after booking/diagnosis, before repair starts, or from `IN_PROGRESS`; it remains there while any blocking requirement is short. It may resume to `IN_PROGRESS` only after all such requirements are fully reserved and an employee authorizes the transition.
- After repair, moving to `FIXED` keeps reserved parts; full settlement consumes them once while still `FIXED`; later `PICKED` records handover only.
- Cancelled service requirements release active reservations; received goods remain Shop inventory unless a separate supplier-return workflow is implemented.
- Supplier purchase and tracking URLs are treated as external references only; no credentials or payment card data can be stored in link fields.
- A single ServiceOrder can create multiple procurement request rounds at different times; all rounds remain searchable by ServiceOrder ID and independent histories are preserved.
- Request creation from the ServiceOrder screen and the purchase queue produces the same Shop-scoped model; cross-Shop ServiceOrder IDs are rejected.
- Request edits, line updates, buyer assignment, priority changes and image actions append an immutable `ServiceProcurementRequestEvent` attributed to the employee authorization used.
- Line status transitions are calculated from purchase allocations, accepted receipts and cancellation; header status aggregates active lines and cannot be manually forced to `FULFILLED`.
- Soft-deleting a reference image preserves the image metadata, uploader and deletion event; the file is no longer exposed in the normal active gallery but remains in authorized audit history.
- Each request can include multiple product requirement lines and multiple reference images per line; thumbnails/full-size views are authorized and cannot be accessed from another Shop.
- Procurement and refurbished-unit image uploads validate file content/MIME/size and use server-generated private storage keys; path traversal, public URL leakage, unsupported content and guessed image IDs are rejected.
- Cancelling a request round does not cancel the underlying ServicePartRequirement or another request round; unreceived allocations must be explicitly released/reallocated.
- The same outstanding shortage cannot be simultaneously requested/ordered twice across active requests under concurrent creation attempts.
- A consolidated SupplierPurchase can allocate lines to requests for several ServiceOrders in one Shop; a request line can be split across multiple purchases without over-allocating quantity.
- Queue header/line status aggregation correctly handles OPEN, IN_PROGRESS, PARTIALLY_ORDERED, ORDERED, PARTIALLY_RECEIVED, FULFILLED and CANCELLED; overdue is derived from the linked SupplierPurchase ETA.

## Additional finance-specific decision tests

- A `DEPOSIT_RECEIPT` during ServiceOrder booking is recorded under the current Shop and employee; it is not automatically a service payment or stock movement.
- A `DepositApplication` is a non-cash credit to its own ServiceOrder and cannot exceed the available held balance.
- `SECURITY` deposits do not become revenue or order payment automatically; refund/forfeiture must be separately authorized.
- Every `InstallmentSettlement` references a settled payment and the same Sale as its InstallmentPlan. Schedule and sale balances reconcile without duplicate gross revenue.
- `LAYAWAY` is the default plan fulfillment behavior. `CREDIT_SALE` is blocked unless an explicit deployment compliance feature flag and approved policy are in place.
- A `REFUND` references an original settled incoming transaction; refunds can be partial/multiple but not exceed the original refundable balance under concurrency.
- Refund allocation to a different tender than the original is only accepted when Shop policy and approval permit it.
- EUR amount persistence uses decimal arithmetic and two decimal currency precision. Cash rounding is a separate signed adjustment, voluntary/Shop-configured and only for eligible final cash amounts. VAT tests cover Company feature toggle, tax setup READY gate, effective-date selection and historical tax snapshots.
- Cash drawer calculations include cash deposit receipts/refunds; electronic tender and provider payout/fees are reconciled separately.


## New decision-specific tests — v11

- One Company-wide Employee record/password can authorize actions in every Shop; no per-Shop employee duplicate is created.
- `MANAGER` and `TECHNICIAN` roles are company-wide; API authorization is evaluated per action and the role matrix rejects disallowed actions.
- Quantity-tracked `quantity_on_hand` accepts negative signed values; `quantity_reserved` cannot be negative; available quantity may be negative; negative deficits are visible in stock/purchasing reports.
- Reservation creation only reserves currently positive available units; a subsequent fully settled sale/service may drive the same product balance below zero without deleting existing reservation history.
- A Sale or ServiceOrder with partial payments posts no stock movement; when the balance reaches zero, full product quantities are posted once in the same settlement transaction. For a ServiceOrder the deduction is not blocked on later `PICKED`/collection.
- Serialized refurbished units cannot be sold/transferred as phantom negative stock.
- Each sold refurbished unit creates a 12-calendar-month commercial warranty starting at recorded customer handover; the customer-facing terms explicitly state that statutory rights are unaffected.
- Procurement purchase creation, supplier linking and receipt do not require a separate manager approval record; each action still requires employee password authorization and immutable actor audit.
- VAT toggle is Company-scoped. `NOT_REVIEWED` tax setup blocks production tax-document finalization; enabling VAT requires active effective-dated tax codes/rates; flag/rate changes do not alter historical tax snapshots.
- Procurement request and refurbished-unit image uploads support multiple files and enforce private, Shop-authorized access, content validation, configurable size/count limits and audit events.

## Product-owner confirmations — v11

- An employee can enter the same password and authorize an allowed operation in any Shop of the Company; the persistent ShopSession contains no employee identity.
- `MANAGER` and `TECHNICIAN` are Company-wide roles. A user with an allowed role can act in every Shop; an action outside the role policy is rejected. Detailed per-action permissions remain a separate approval item.
- Payment completion is the inventory posting trigger: partial receipts do not deduct quantity-tracked stock; the command that reduces the Sale/ServiceOrder balance to zero posts the product deduction once. The later service `PICKED` action does not post a second deduction.
- A quantity-tracked Product balance may become negative after a completed sale/service settlement; it must be persisted and appear as a visible deficit. Reservation quantity remains non-negative; serialized refurbished units cannot be negative.
- `vat_enabled` can be toggled at Company level. The default is false and tax setup remains `NOT_REVIEWED` until configured and reviewed. No production tax document can be finalized with `NOT_REVIEWED`; changing the flag does not rewrite historical tax snapshots.
- Every sold refurbished physical unit receives a 12-month commercial guarantee starting on actual handover. Customer terms do not present this guarantee as replacing or limiting Irish statutory rights.
- Supplier purchase creation/linkage/update/receipt succeeds without a second manager approval; each write is still attributed to an employee authenticated by password and is audited.
- Multiple private images can be uploaded to a procurement request line and to a refurbished physical unit. Cross-Shop image reads are denied; content/signature checks, file size/count limits and audit events are enforced.


## Product-owner confirmation tests — v13

- Employee authorization resolves a Company-wide employee using the configured employee identifier plus password; password-only lookup must not attribute an action to an ambiguous employee.
- The same active employee identity/password and Company-wide role assignment works across all Shops; `ShopSession` stores no employee identity.
- MANAGER/TECHNICIAN role names are recognized globally; until the action-permission matrix is approved, do not ship guessed permissions.
- Supplier purchase creation, linking, update and receipt do not trigger a second-person approval workflow; the acting employee must still be authorized and audited.
- Multiple images per procurement request line and refurbished unit are supported; file limits, storage retention and allowed formats are configuration/security tests.
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
- Customer-facing exports never include internal-only notes, credentials or private procurement data.
