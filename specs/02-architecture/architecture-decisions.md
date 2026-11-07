# Architecture Decision Records — pcHouseStore Web

**Status:** DOMAIN BASELINE RECORDED — v19 (OAuth/OIDC API security added).  
**Overall model status:** DRAFT. The recorded product-owner decisions below are stable; physical DDL and implementation still require review.

## ADR-001 — One company with multiple shops

**Decision:** The product represents one legal/business `Company` with multiple operational `Shop` records (stores/branches). `Company` is not a tenant selector and is not the employee's active login context. `Shop` is the operational context.

### Consequences

- `Company 1:N Shop`.
- Company-level master data is shared by its shops by default: people, customers, employees, catalogue items, products, services, devices and fault catalogue.
- Shop-level operational data is attributed to a `Shop`: sales, service orders, inventory, inventory movements, transfers, payment transactions and cash movements.
- A shop is selected/authenticated at system entry. Every API operation derives the active `CompanyId` and `ShopId` from the validated shop session; a client-supplied `ShopId` cannot override that context.
- Remove `CompanyMembership` and multi-company tenant assumptions. Employee identity and roles are Company-wide; the same account/password works in every Shop belonging to the Company. No duplicate employee account per Shop is required.
- The initial deployment is provisioned for one Company. Supporting multiple legal companies later would be a separate architecture decision, not an implicit property of the current model.

## ADR-002 — Shop session, employee authorization per action

**Decision:** The persistent application session represents the authenticated shop, not an employee. Employees authenticate individually when they execute a business operation that requires attribution.

### Session and operation flow

1. The operator opens the application and authenticates using the selected shop's access credential.
2. The server establishes a `ShopSession` with `CompanyId`, `ShopId`, session identifier and expiry. It must not contain an active `EmployeeId` or employee role as the session identity.
3. Any authorized employee can use that active shop session on the workstation. The session does not imply that the employee who last used it is still operating.
4. Before committing a sale, service-order action, payment, refund, reservation, stock transfer, stock return, stock adjustment, cash movement, closing or other protected business command, the application requests the executing employee's own credential. One atomic command may persist several related rows under the same authenticated actor.
5. The server validates that the employee is active, belongs to the Company that owns the active Shop and is authorized by a Company-wide role for the specific action. It creates a short-lived, single-use `EmployeeActionAuthorization` scoped to the shop session and action code.
6. The business command consumes that authorization atomically and stores the employee attribution on every row that requires actor traceability. The next operation requires a new employee authentication; the previous employee is never silently reused.

### Security consequences

- A shop credential authenticates the shop context only. It does not authorize employee-level business actions.
- Employee credentials are never stored in the shop session or returned to the browser as reusable authentication state.
- Store password hashes and employee credential hashes using a vetted password-hashing algorithm; never store plaintext, Base64-encoded passwords or reversible secrets.
- Rate-limit and audit failed employee credential challenges. Apply lockout/protection without allowing one employee's failed attempts to invalidate the shared shop session for everyone.
- An `EmployeeActionAuthorization` is one-use, short-lived and action-scoped. It is consumed server-side in the same database transaction as the business operation. Employee password credentials are the selected challenge mechanism; the ShopSession remains employee-free.
- The employee actor on the resulting record is derived from or constrained to the consumed authorization. Do not trust an `EmployeeId` submitted by React. The same employee password is accepted in every Shop belonging to the Company.

## ADR-003 — Product-only stock, integer quantities and balances per shop

**Decision:** `product_only` — only catalogue entries classified as products are stock-managed. Each shop maintains its own stock. Product and stock quantities are whole units; fractional product quantities are not permitted because the inventory consists of discrete electronic parts/equipment.

### Consequences

- Keep a common `CatalogItem` identity with `Product` and `Service` subtypes.
- `ShopInventory` holds `quantity_on_hand` and a reservation counter for a `(Shop, Product)` pair when the product uses quantity-based tracking.
- `Service` items have no stock balance and cannot be reserved, transferred or moved in inventory.
- Stock quantities, reservations, transfers, returns and inventory movements use integer quantities. A product line in a sale or service order must have a whole-number quantity.
- Stock changes and their `InventoryMovement` ledger entries are committed atomically.
- Quantity-tracked stock is allowed to become negative; negative balance is a recorded shortage, not a validation error. `quantity_reserved` remains non-negative; `available_quantity = on_hand - reserved` may be negative. Reservation commands only reserve currently positive available stock.
- The catalogue and product definition are shared by all shops. Balances and movements are not.
- Serialized refurbished products are tracked by their physical `RefurbishedUnit` records, one row per physical unit; their availability is derived from unit status and current shop rather than a manually edited aggregate quantity.

## ADR-004 — Payment transaction with allocations

**Decision:** `transaction` — one financial transaction represents one payment/deposit/refund event and can contain multiple tender allocations. Financial details and Ireland localization are in [Financial Model](financial-model.md).

### Consequences

- `PaymentTransaction` stores the financial amount once and belongs to exactly one Sale or ServiceOrder.
- Each `PaymentTransaction` is attributed to the active `Shop` and the employee whose one-time authorization was consumed.
- `PaymentAllocation` stores tender components through a configurable `PaymentMethod` catalogue. Initial enabled methods for the Irish deployment are CASH and CARD only. Card is recorded after approval on an external terminal; processor API integration is deferred.
- A combined €100 payment (€60 cash + €40 card) is one transaction with two allocations, not two duplicated €100 transactions.
- Partial payments are separate transactions against the same Sale or ServiceOrder.
- Refund amounts are stored as positive magnitudes and reduce net totals by transaction kind; each refund references its original transaction.
- Inventory consumption is triggered by business payment settlement rules in ADR-006, not by creating a payment allocation row in isolation.

### Consequences

- Deposits, instalment receipts, ordinary payments and refunds are recorded as distinct transaction kinds and remain traceable to their financial target.
- Payment allocations support mixed tenders, external references and provider metadata without storing card PAN/CVV.
- Only settled/confirmed transactions count as money received. Pending or failed attempts never reduce an outstanding balance.

### v1 operational policy

- Enabled tenders: CASH and CARD only.
- Card processing occurs on an external terminal. The employee records approval/reference; no processor integration is selected or implemented.
- Refunds are Manager-only, partial or total, and return to original tender by default. No amount threshold or second Manager approval is used.
- See [Financial Model](financial-model.md) for deposit expiry/cancellation/forfeiture policy and card-tender handling.

## ADR-005 — Daily closing per Shop and business date

**Decision:** `shop_date` — each Shop has at most one `DailyClosing` for each business date, unique on `(ShopId, BusinessDate)`.

### Consequences

- A Shop closes and reconciles only its own sales, payment allocations, refunds and cash movements for that Shop's business date.
- A different Shop may close the same business date independently. A Shop cannot create a second close for the same business date.
- `DailyClosing` references `shop_id`; it does not need a direct `company_id`, because the owning Company is reached through Shop.
- The closing employee and `EmployeeActionAuthorization` must belong to the same Shop and the authorization is consumed once for the close operation.
- The business date is calculated using the Shop's configured time zone. Event timestamps remain stored consistently (UTC).
- Company-wide reporting may aggregate completed Shop closings, but this must not create a consolidated company-wide `DailyClosing` or mix the cash drawers.

### Still to define

The operational formula, opening-float capture, refund/change/cash movement treatment and immutable correction workflow are specified in [Cash Closing Policy](cash-closing-policy.md). Accounting chart/tax mappings and electronic processor reconciliation still require finance review.

## ADR-006 — Stock reservations, payment posting and selected returns

**Decision:** Reservations are explicit, cancellable holds on currently available product stock. A reservation does not reduce physical on-hand stock. Quantity-tracked stock is allowed to become negative. Stock is deducted atomically when the Sale or ServiceOrder balance reaches zero through settled payments (and explicitly applied deposits, where applicable); partial payments do not deduct stock. A cancelled reservation leaves on-hand unchanged.

### Sales

- A sale may reserve some or all product lines before payment. A reservation is optional and explicit; service catalogue lines are never reserved.
- For a standard immediately paid sale, the stock deduction occurs in the same atomic command as successful settlement. For a weekly refurbished sale plan, the physical unit is reserved while payments are outstanding. The plan must declare its handover model: `LAYAWAY` (handover after full settlement) or `CREDIT_SALE` (handover before full settlement, with stock consumed at handover). The latter is disabled until the Irish regulatory review is completed.
- An active reservation reduces `available_quantity = quantity_on_hand - quantity_reserved`, but does not create an outbound stock movement.
- Releasing/cancelling a reservation decreases reserved quantity only. It does not increase on-hand quantity because no physical stock had been deducted.
- On full sale settlement, all product quantities are deducted in one atomic posting. Active reservation quantities are consumed first. If the resulting on-hand/available balance is negative, preserve the signed deficit and complete settlement; make the deficit visible in inventory reports.

### Service orders

- Product parts needed by a ServiceOrder are represented by `ServicePartRequirement` records linked to product line items. Available quantities may be reserved when the requirement is recorded; a shortage can be procured from a supplier.
- If any blocking part requirement is not fully reserved, the ServiceOrder enters `AWAITING_PARTS`. The parts remain reserved as they become available, but no physical deduction occurs.
- When all blocking requirements are fully reserved, an employee-authorized resume command returns the order to `IN_PROGRESS`. When the repair is complete, transition to `FIXED` keeps reservations active while the repaired device awaits payment/pickup.
- When settled payments and explicitly applied deposits reduce the ServiceOrder balance to zero, product quantities are consumed in the same atomic payment command. This is independent of the later physical `PICKED`/collection status. The `FIXED` transition requires all blocking product parts needed to complete the repair to be reserved.
- Partial payments and unapplied deposits do not consume service-order parts. A deposit received at booking is financially recorded but does not consume inventory. Supplier receipts add actual received stock and may create reservations; they do not consume stock.

### Cancellation, refund and physical return

- Cancelling an unpaid sale/order releases any active reservation and creates no return movement.
- When the Sale or ServiceOrder status is `REFUNDED` or `CANCELLED`, the operator may select which previously consumed product lines and quantities are physically returned to stock.
- `StockReturn`/`StockReturnLine` records that selection. Only selected quantities produce positive inventory movements; the whole sale/order is never restocked automatically.
- A return quantity cannot exceed the consumed quantity minus quantities already returned. A returned serialized refurbished unit must be the exact original unit.
- Financial refund and stock return are separate records linked where relevant: a monetary refund does not, by itself, prove that goods physically returned to inventory.

### Consequences and safeguards

- `InventoryReservation` and `InventoryReservationLine` preserve reserve/release/consume history.
- `ShopInventory` stores integer `quantity_on_hand` and `quantity_reserved`; the latter must reconcile to active reservation lines.
- Sale/service payment settlement, reservation consumption, stock movements and payment transaction persistence must be atomic and idempotent.
- Reservation expiry/automatic timeout is not enabled until a duration and policy are approved.
- Negative stock is an explicit owner decision for quantity-tracked products. `quantity_on_hand` is signed; `quantity_reserved` remains non-negative; `available_quantity` may be negative. Reservation and transfer dispatch only reserve/dispatch available physical units; a completed payment may post a negative balance when the sold/used quantity exceeds available stock. Negative stock must produce a visible deficit alert.

## ADR-007 — Inter-shop product transfers

**Decision:** Products can be transferred between Shops belonging to the same Company using an explicit transfer document.

### Lifecycle

1. Create a `StockTransfer` in `DRAFT` with source Shop, destination Shop and integer product lines.
2. At dispatch, require an employee authorization from the source Shop, validate unreserved available stock, post outbound movements and change status to `IN_TRANSIT`.
3. At receipt, require an employee authorization from the destination Shop, record the actual received quantities and post inbound movements to the destination inventory.
4. Support partial receipt with `PARTIALLY_RECEIVED`; finish as `RECEIVED` when all dispatched quantities are accounted for.
5. A transfer can be cancelled without stock movement only before dispatch. After dispatch, it cannot be simply cancelled; a corrective return/reverse transfer must be recorded.

### Consequences

- `StockTransfer` must reference two distinct Shops belonging to the same Company.
- Stock leaves the source only at dispatch and becomes available at the destination only at receipt. In-transit quantities are not available at either Shop.
- Serialized refurbished units are transferred by exact `RefurbishedUnit` identity with quantity one.
- Dispatch and receipt are separate protected actions and are attributed to different employee authorizations when performed by different Shops.
- The initial model assumes one stock pool per Shop. Multiple warehouses/shelves/stock locations inside a Shop are not introduced in this decision.

## ADR-008 — Refurbished categories, technical specifications and unit serialization

**Decision:** The refurbished catalogue supports extensible product categories (for example PCs, consoles and TVs), category-specific technical attributes and an individual record for each physical refurbished unit.

### Consequences

- `ProductCategory` is data, not a hard-coded enum; new categories can be added without a schema migration.
- `ProductAttributeDefinition` defines the specifications expected for a category (for example CPU, RAM, storage, GPU, screen size, resolution or console storage).
- `RefurbishedUnit` represents one physical unit, with its own internal asset code, optional manufacturer serial number, brand/model overrides, condition, notes, current Shop and lifecycle status.
- `RefurbishedUnitAttributeValue` stores values for the category-defined technical attributes. Typed value columns and validation prevent incompatible value types.
- A refurbished product uses `SERIALIZED_UNIT` inventory tracking: exactly one unit per physical record; quantities in sales, reservations, returns and transfers are therefore whole numbers and one for each serialized unit.
- A sale of a serialized refurbished unit references the exact `RefurbishedUnit` sold. Returns and transfers must preserve that identity.

### Still to define

Condition-grade vocabulary, mandatory serial number rules by category, serial-number uniqueness scope, acquisition/source details, commercial guarantee coverage/exclusions and claim procedure, and inspection/disposition after customer returns. The 12-month duration and handover start date are decided.

## ADR-009 — Ireland monetary and cash-rounding baseline

**Decision:** The initial deployment is configured for Ireland with `country_code = IE`, `default_currency_code = EUR` and `time_zone = Europe/Dublin`.

### Consequences

- Monetary values use C# `decimal` and MySQL `DECIMAL(19,2)` for euro amounts. Percentages/rates use a separate higher-precision decimal type. Binary floating point (`double`/`float`) is prohibited for money.
- Each document and financial transaction snapshots its currency code. Multi-currency settlement is out of initial scope; FX conversion must not be inferred.
- Each Shop may opt into Irish cash rounding. It is voluntary, applies only to the final cash-paid bill total, and rounds to the nearest €0.05. Exact-change payment remains possible. Card and other electronic payments are never rounded; individual line prices and the VAT calculation base remain unrounded.
- Persist the signed cash-rounding adjustment separately from invoice value, VAT and actual cash tender. A cash-rounding adjustment is not a price/discount/tax change.
- VAT rates and tax treatments must be configurable/effective-dated and snapshotted per sale/service invoice. Do not hard-code a rate in application code or derive tax from a rounded cash amount.

The Central Bank of Ireland confirms that cash rounding is voluntary, applies only to cash, is performed on the total bill at the nearest five cent, leaves item prices unchanged and does not change VAT on the unrounded amount: https://www.centralbank.ie/consumer-hub/notes-and-coins/rounding.

## ADR-010 — Service deposits recorded at booking

**Decision:** A service order may receive one or more deposits at booking or later. A deposit is a separately tracked amount with a declared purpose; the receipt is recorded immediately using a `DEPOSIT_RECEIPT` PaymentTransaction.

### Consequences

- `ServiceDeposit` is linked to exactly one ServiceOrder and Shop. Supported purposes include `PARTS_PREORDER`, `BOOKING_ADVANCE`, `SECURITY` and `OTHER`.
- The deposit receipt and its tender allocation are recorded at the moment money is received, including when the order is first booked.
- A deposit held as security or a parts reservation is not automatically considered payment of the service invoice. It only reduces `balance_due` when an explicit `DepositApplication` applies it to the order (optionally to a specific service-order line).
- A held deposit can be partially applied, refunded or forfeited. Every disposition is an employee-authorized, auditable operation. A forfeiture or use of a security deposit should support a separate supervisor-approval policy.
- Deposit received, applied, refunded and forfeited amounts must reconcile; the same amount cannot be applied/refunded/forfeited more than once.
- Receiving a deposit does not consume service parts or stock. The service-order reservation rule remains tied to `FIXED`; stock consumption occurs at full settlement, independently of later `PICKED`/collection.

## ADR-011 — Weekly instalment plans for refurbished sales

**Decision:** The system will support a weekly instalment schedule for a Sale of serialized refurbished equipment.

### Consequences

- An `InstallmentPlan` belongs to one refurbished Sale and contains the agreed cash price, amount financed, any disclosed charges, schedule frequency, first due date and installment count. The initial requested cadence is `WEEKLY`.
- `InstallmentSchedule` stores each due date and amount. `InstallmentSettlement` allocates one or more settled PaymentTransactions to schedule rows, allowing partial payment of an instalment or one receipt paying multiple instalments without duplicating the sale value.
- All schedule amounts, due dates and customer-facing terms are snapshotted when the plan is accepted. Missed-payment, late-fee, interest and rescheduling policies are not invented by the application.
- The plan must declare one of two fulfillment models: `LAYAWAY` (unit stays reserved and is handed over only after full settlement) or `CREDIT_SALE` (unit is handed over before all instalments are paid; inventory is consumed at handover and the remaining amount stays receivable). The default supported workflow is `LAYAWAY` until the business confirms otherwise.
- `CREDIT_SALE` must remain feature-gated pending Irish legal/regulatory review. The Central Bank of Ireland describes paying for goods now and by instalments later, including in-store credit and BNPL, as short-term credit and states that specified short-term-credit activities require authorisation and affordability/creditworthiness controls. The exact legal classification of this business's arrangement must be confirmed by qualified Irish counsel/compliance before enabling it: https://www.centralbank.ie/consumer-hub/short-term-credit.

## ADR-012 — Partial and total refunds

**Decision:** Monetary refunds may be partial or total and are recorded as separate positive-magnitude `REFUND` PaymentTransactions linked to the original settled payment transaction.

### Consequences

- Multiple partial refunds may be issued against one original payment until its refundable balance reaches zero.
- A refund amount cannot exceed the unrefunded settled amount of its original transaction; the check must be concurrency-safe.
- Refund tender allocations record the channel used to return money. Default policy should prefer the original tender, but an authorized alternative channel may be allowed by Shop policy and must be audited.
- An optional `PaymentRefundLine` links refunded amounts/quantities to the relevant SaleItem or ServiceOrderItem. An order-level refund without a line allocation is also possible when its reason is documented.
- Financial refunds do not automatically restock products. Physical stock return continues through selected `StockReturnLine` records only.
- Refund approval is configurable by Shop/action/amount. No approval threshold or role is hard-coded; thresholds and approver requirements must be configured before production.

## Decision register

| ID | Decision | Selected value | Status |
|---|---|---|---|
| ADR-001 | Organizational model | One Company, multiple Shops | DECIDED |
| ADR-002 | Persistent session / transaction actor | Shop session; Company-wide employee password and roles across all Shops; fresh employee password for each protected action | DECIDED |
| ADR-003 | Stock control | Product only; integer quantities; balance per Shop; negative quantity balance allowed | DECIDED |
| ADR-004 | Payment structure | PaymentTransaction + PaymentAllocation | DECIDED |
| ADR-005 | Closing uniqueness | One close per Shop/business date | DECIDED |
| ADR-006 | Reservation and stock posting | Explicit optional reservation; consume when Sale/ServiceOrder balance reaches zero; negative quantity balance allowed; selected return lines | DECIDED |
| ADR-007 | Transfer between Shops | Explicit dispatch/in-transit/receipt workflow | DECIDED |
| ADR-008 | Refurbished product modelling | Extensible categories, typed specs, serialized units, private images, 12-month commercial guarantee from handover | DECIDED |
| ADR-009 | Ireland monetary baseline | EUR, Europe/Dublin, decimal currency, optional cash-only €0.05 rounding | DECIDED |
| ADR-010 | Service deposits | Record at booking; purpose and held/applied/refunded/forfeited lifecycle | DECIDED |
| ADR-011 | Refurbished-sale instalments | Weekly schedule; layaway default; credit-sale handover gated by Irish compliance review | DECIDED WITH LEGAL GATE |
| ADR-012 | Refunds | Partial or total, linked to original transaction, capped by refundable balance | DECIDED; approval policy values OPEN |

## Non-decisions — must not be inferred

The following remain open: customer/device visibility; mandatory serial-number/uniqueness rules by category and acquisition/source fields; commercial-guarantee coverage/exclusions; enabled payment methods/provider settlement/fees; refund thresholds/alternative-tender policy; deposit expiry/forfeiture terms; VAT registration/status/rates/pricing/tax posting pending Irish tax review; instalment late-payment policy and any future CREDIT_SALE handover (disabled pending legal review); complete Sale/ServiceOrder transition matrix. Reservation expiry and multiple stock locations are intentionally out of initial scope; one stock pool per Shop and explicit release/consumption are the baseline. Condition grades A/B/C, complaint workflow, role matrix, image limits and daily cash-close formula are now specified in their linked documents.

## ADR-013 — Supplier procurement and parts-waiting service workflow

**Decision:** A ServiceOrder may be placed in `AWAITING_PARTS` when a required product part is not fully available/reserved. The application will provide an auditable procurement tracker for external supplier purchases, including source/order links, expected delivery dates, tracking details and partial receipts.

### Workflow

1. During booking or diagnosis, add the required part as a product `ServiceOrderItem` and create a `ServicePartRequirement` with whole-unit quantity and a `blocks_work` flag.
2. Reserve any available quantity in the current Shop immediately. If the requirement is not fully reserved and blocks work, move the ServiceOrder to `AWAITING_PARTS` from `CREATED` (including directly after booking/diagnosis, before repair starts) or from `IN_PROGRESS`.
3. Create one or more `ServiceProcurementRequest` rounds for the ServiceOrder and add request lines against the relevant requirements. A later-discovered part creates a new request round rather than overwriting earlier purchasing history. The buyer then creates or reuses a `SupplierPurchase`, captures supplier/source name, external order reference, purchase/order URL, product listing URL, ordered date, expected delivery date, tracking reference/URL and notes, and links purchased quantities to request lines through `ServicePartProcurementAllocation` (as refined by ADR-014). A single supplier purchase may consolidate requests for multiple service orders in the same Shop.
4. Update expected delivery dates, tracking links and order status through append-only `SupplierPurchaseEvent` records. Display delays as derived overdue state; do not overwrite history.
5. On partial/full delivery, an employee records the actual accepted quantities. The system posts `PURCHASE_RECEIPT_IN` to the Shop stock ledger. Quantities allocated to request lines are reserved against their linked service requirements atomically; excess quantities remain available Shop stock.
6. When all blocking requirements are fully reserved, an employee-authorized resume action changes `AWAITING_PARTS` to `IN_PROGRESS`. Receiving stock alone does not silently restart the repair.
7. The repair then follows ADR-006: `FIXED` keeps reservations; when the balance reaches zero, stock is consumed atomically. The later `PICKED` event records physical handover only.

### Data model and consequences

- `Supplier` is a reusable Company-level source directory; `SupplierPurchase` is Shop-scoped. Supplier is nullable for a one-off source.
- `SupplierPurchaseLine` stores ordered and actually received quantities, product link and unit cost. The allocation target was refined by ADR-014 to `ServiceProcurementRequestLine`, enabling a separate queue and repeated sourcing rounds per ServiceOrder.
- `SupplierPurchaseEvent` stores the audit timeline for ETA, tracking, status and receipt changes with employee/action authorization.
- Purchase and tracking URLs are external reference links only. The initial scope does not scrape websites or integrate supplier APIs.
- Supplier accounts payable, supplier payment settlement, supplier returns and procurement tax postings are outside the initial scope.
- An overdue purchase is derived from expected delivery date and open purchase status. Never auto-cancel or auto-reassign stock solely because an ETA passes.

### Required safeguards

- Only `PRODUCT` items can be procured as service parts; quantity fields are positive integers.
- Actual accepted receipt quantity cannot exceed outstanding ordered quantity. Duplicate receipt requests are idempotent.
- Allocated received quantities cannot exceed received purchase-line quantity or the outstanding requirement quantity.
- Inventory receipt movements and requirement reservations are committed atomically.
- A service order remains `AWAITING_PARTS` until all blocking requirements are reserved. `ServiceOrderEvent` records the state transition; `SupplierPurchaseEvent` records procurement changes.
- Cancelled requirements release unconsumed reservations. If the purchased goods already arrived, they remain in Shop stock unless a separately approved supplier-return process is implemented.


## ADR-014 — Service parts purchase queue with request rounds and reference images

**Decision:** The application will provide a dedicated Shop-scoped **Service Parts Purchase Queue**. Each queue request belongs to exactly one ServiceOrder and represents one purchasing/sourcing round. A ServiceOrder may have multiple procurement requests created at different moments in its lifecycle. Each request contains one or more request lines linked to durable ServicePartRequirement records and supports multiple private reference images per line.

### Workflow and consequences

1. The technician creates or confirms a `ServicePartRequirement` against a product ServiceOrderItem. Available stock is reserved; the shortage is identified.
2. When purchasing is needed, the application creates a `ServiceProcurementRequest` with a sequential request number within the ServiceOrder. The same ServiceOrder may receive another request later when diagnosis or repair reveals additional parts.
3. `ServiceProcurementRequestLine` records the whole-unit sourcing quantity, part number and compatibility notes. `ServiceProcurementRequestImage` stores metadata for multiple reference photos (part, label, connector, damage, etc.).
4. Buyers work from a Shop-wide queue that is searchable by ServiceOrder ID and filterable by status, priority, needed-by date, part and assigned buyer. Every request detail links back to its ServiceOrder.
5. One SupplierPurchase may cover request lines from several ServiceOrders in the same Shop. A request line may be split across multiple supplier purchases. `ServicePartProcurementAllocation` therefore links SupplierPurchaseLine to ServiceProcurementRequestLine; the durable service requirement is reached through the request line.
6. Accepted receipts create stock movements and reservations atomically. The queue's request/line statuses are derived from order allocations, accepted receipts and cancellations; they are independent from ServiceOrder and SupplierPurchase statuses.

### Data and security consequences

- Add `ServiceProcurementRequest`, `ServiceProcurementRequestLine`, `ServiceProcurementRequestImage` and `ServiceProcurementRequestEvent`. A requirement may be referenced by multiple request lines across separate sourcing rounds, but the aggregate open requests/allocations may not exceed its outstanding shortage.
- Image bytes are stored in private object/file storage, not in MySQL. Persist storage key, original file metadata, MIME/content type, size, digest, role/caption, upload actor and timestamp. Issue short-lived access URLs only after Shop/permission validation.
- Upload, view, deletion, request edits, assignment, supplier-order linkage and receipt are Shop-scoped. Mutating actions require fresh employee authentication and one-use EmployeeActionAuthorization; the ShopSession continues to contain no employee identity.
- Cancelling a procurement request cancels only that sourcing round. It does not cancel the ServicePartRequirement, supplier purchase history or other request rounds.
- No automatic supplier site scraping, marketplace APIs, supplier accounts payable or supplier-return workflow is introduced by this decision.

### Still to define

Image limits are specified in [Image Upload Policy](image-upload-policy.md). MANAGER/TECHNICIAN permissions are specified in [Employee Access and Roles](employee-access-and-roles.md). Buyer assignment policy and supplier quote-comparison are not required for the initial service-parts workflow; purchase approval is not required.


## ADR-015 — Company-wide employee password and role names across all Shops

**Decision:** Each employee has one Company-wide account authenticated by employee code + password. The same employee credentials work in every Shop belonging to the Company. The persistent ShopSession still identifies only the active Shop; each protected operation requires a fresh password challenge from the acting employee. `MANAGER` has full access across all Shops. `TECHNICIAN` has partial access; the detailed first-release action matrix in [Employee Access and Roles](employee-access-and-roles.md) is a proposal pending owner review.

### Roles

- `MANAGER`: full operational, commercial, financial, staff, Shop and Company configuration access across all Shops.
- `TECHNICIAN` (proposed first-release scope): service-order creation/update and technical workflow, diagnosis/notes, part requirements, procurement requests/images and operational supplier-purchase/receipt capture for service parts, Shop stock availability/reservations, refurbished inspection/condition and complaint technical evidence. Excluded in this proposal: sales, prices/discounts, payment/deposit/refund, cash, stock adjustment/transfer, staff administration, configuration, approval or complaint closure.
- Roles are Company-wide, not duplicated per Shop. The role/action permission matrix is enforced server-side and never trusted from React.

### Consequences

- `ShopEmployee` is removed from the default model. `EmployeeRoleAssignment` records Company-wide roles.
- `EmployeeActionAuthorization` validates employee active state, Company ownership against the current ShopSession and permission for the action; it does not require a per-Shop employee account.
- Every operation stores the employee who authorized it. The ShopSession contains no employee identity and never reuses the prior actor.

## ADR-016 — Negative quantity stock is permitted; deduction on full settlement

**Decision:** Quantity-tracked Product balances may become negative. Stock is deducted only when settled payments reduce the Sale or ServiceOrder balance to zero. Partial payment does not deduct stock. For ServiceOrder, `FIXED` retains the reservation; full financial settlement consumes reserved parts before any later `PICKED`/collection state.

### Consequences

- `ShopInventory.quantity_on_hand` is a signed integer. `quantity_reserved` remains non-negative. `available_quantity = quantity_on_hand - quantity_reserved` may be negative.
- Reservation operations allocate only currently positive available stock. A payment settlement may still complete if the deduction creates a negative quantity balance.
- Negative balance is a stock deficit/backorder and must be visible in inventory, purchasing and manager reports. Do not clamp it to zero.
- Serialized refurbished devices remain individually identified physical units and cannot be negative or represented by phantom units.
- Transfer dispatch still requires actual positive available quantity; negative stock cannot be transferred.

## ADR-017 — Company-level VAT feature flag and tax configuration

**Decision:** Add a Company-level `vat_enabled` boolean feature flag and a configurable, effective-dated tax model. The owner has instructed that the application add no VAT amount for now: `vat_enabled=false` and `setup_status=NOT_REVIEWED`. VAT registration status, applicable rates and price-display policy remain unknown; the software setting is not a legal determination.

### Consequences

- `vat_enabled` is a Company-level enable/disable flag and defaults to `false`; `setup_status` starts at `NOT_REVIEWED`.
- While `vat_enabled=false`, the application does not calculate, add or collect a VAT amount. The receipt/invoice must not describe the Company as legally exempt merely because the feature is disabled.
- `CompanyTaxConfiguration` stores the flag and a `setup_status` (`NOT_REVIEWED`/`READY`). The initial state is NOT_REVIEWED.
- `CompanyTaxCode` and `CompanyTaxRate` store classifications and effective-dated rates only for a reviewed configuration; the application contains no hard-coded VAT rate.
- Once the Irish tax position is reviewed, a manager may enable VAT with the approved tax codes/rates. For enabled transactions, line tax treatment/base/rate/amount are snapshotted at transaction time. Changing configuration affects only future transactions.
- Production tax-document finalization requires the tax setup to be reviewed and marked READY. Setting `vat_enabled=false` is not proof that VAT is legally not chargeable; the business must document the applicable status/reason with an Irish tax professional.

## ADR-018 — Twelve-month commercial guarantee for refurbished units

**Decision:** Each refurbished unit sold by the Company has a 12-month commercial guarantee. The guarantee period starts on the recorded customer handover/collection date, not on stock reservation, deposit receipt or sale quote.

### Consequences

- Create a `RefurbishedUnitWarranty` record linked to the exact sold `RefurbishedUnit` and `SaleItem`.
- Snapshot `duration_months=12`, start/end timestamps and the customer-facing terms at handover.
- Calculate expiry as 12 calendar months from the handover date.
- The commercial guarantee is additional to statutory Irish consumer rights; it must not be worded or implemented as a cap on statutory remedies. Condition grades A/B/C, functional inspection and a CustomerComplaintCase workflow are specified in [Refurbished Condition Grades and Customer Complaint Workflow](refurbished-condition-and-complaints.md). Coverage/exclusions remain subject to business/legal review. The claim workflow must not auto-reject a statutory-rights case when the 12-month commercial guarantee expires.
- Refurbished units support multiple private images with audited upload/view/delete operations.

## ADR-019 — Supplier purchases do not require a second approval

**Decision:** Creating, linking, updating or receiving supplier purchases for service parts does not require a separate manager approval or `BusinessActionApproval` workflow.

### Consequences

- The employee who performs the action must still authenticate using their Company-wide password in the current Shop session.
- Purchase and receipt records retain employee/action authorization attribution and append-only history.
- Procurement screens do not show an approval state or block a purchase pending a second person.
- Financial approval policies for refunds, deposit forfeiture or other explicitly configured actions remain independent and are not removed by this decision.


## ADR-020 — Refurbished-unit image uploads and purchase approval policy confirmed

**Decision:** Refurbished physical units support multiple image uploads, in addition to the existing multiple reference-image uploads on service-part procurement request lines. Supplier purchases for service parts do not require a second manager approval.

### Consequences

- `RefurbishedUnitImage` and `ServiceProcurementRequestImage` store private file metadata in MySQL and file bytes in private object storage.
- Uploads are multiple-file, Shop-authorized operations with content/MIME/signature validation, server-generated storage keys and audit attribution. File size/count and retention limits remain configurable and must be set before production.
- Creating, updating and receiving supplier purchases requires the acting employee's own password challenge and a role permission, but no separate approval from another employee.


## Product-owner decision update — November 2026

The owner has now confirmed the following implementation choices in addition to the previous ADRs:

- Employee challenge is by password; one Company-wide Employee account/password and Company-wide `MANAGER` / `TECHNICIAN` roles work across all Shops. The exact permission matrix remains a separate review item.
- Quantity-tracked Product stock may become negative. Sale and ServiceOrder stock is consumed when the respective balance reaches zero through settled payments; partial payments do not consume stock. A later `PICKED`/collection event does not trigger a second deduction.
- VAT has a preimplemented Company-level enable/disable feature flag, defaulting to disabled with `setup_status=NOT_REVIEWED`. The flag is not a VAT registration/exemption decision and cannot replace Irish tax review.
- Refurbished units carry a 12-month commercial guarantee from customer handover, additional to Irish statutory consumer rights.
- Supplier purchases for service parts do not need a second approval. The acting employee still authenticates each committed operation and is recorded.
- Multiple private image uploads are supported for service procurement request lines and refurbished physical units.


## Product-owner confirmations — v12 (2026-11-05)

The owner reconfirmed and clarified the following implementation rules:

- **Stock posting:** quantity-tracked stock is deducted only when the Sale/ServiceOrder outstanding balance reaches zero through settled payments. Partial payments do not deduct. Quantity-tracked on-hand may become negative. A ServiceOrder consumes its reserved parts at full settlement while remaining `FIXED`; `PICKED` is only the later physical handover event.
- **Employee access:** one Company-wide employee account and password works in every Shop. The persistent session is still only the Shop. Every protected operation requires the individual employee password; roles are Company-wide `MANAGER` and `TECHNICIAN`. No Shop-specific employee membership is needed.
- **VAT:** implement a Company-level boolean enable/disable feature flag. Default `false`; keep legal registration/status, rate and price-display choices separate and pending professional review.
- **Refurbished guarantee:** every refurbished unit sold receives a 12-month commercial guarantee from documented customer handover, without limiting statutory consumer rights.
- **Supplier purchases:** no separate manager approval is required for service-parts procurement. The employee performing the purchase/receipt still authenticates and is audited.
- **Images:** allow multiple private image uploads for service procurement request lines and refurbished physical units.


## Product-owner confirmations — v13 (2026-11-05)

The owner confirmed the core rules for stock settlement, cross-Shop employee authentication, VAT feature flag, 12-month refurbished commercial guarantee, no second purchase approval and multiple image uploads. The v14 decisions supersede the earlier unresolved wording: the MANAGER/TECHNICIAN baseline, A/B/C grades and complaint process, initial upload limits and daily cash-close formula are now documented. Guarantee terms, image storage retention, VAT legal status and payment-provider/accounting details remain open.


See the [consolidated product-owner decision register](product-owner-decisions.md) for confirmed choices and remaining questions.


## ADR-021 — MANAGER full access and TECHNICIAN partial access

**Decision:** `MANAGER` has full access across all Shops. The product owner specified that `TECHNICIAN` has partial access. The detailed first-release allow/deny matrix is a proposed interpretation of that requirement and must be reviewed before coding fine-grained authorization policies.

The proposed matrix and employee-code + password challenge are specified in [Employee Access and Roles](employee-access-and-roles.md). A supplier purchase for service parts does not require a second manager approval, but the acting role and employee authorization are still checked.

## ADR-022 — Daily cash close formula and immutable close

**Decision:** Each Shop has one cash drawer and one `DailyClosing` per business date. The daily record is created in `OPEN` state with a physically counted opening float and closed once. Expected physical cash is opening float + settled cash receipts net of change + cash-in - cash-out - cash refunds/deposit refunds - safe drops. Electronic tenders are reported separately. Variance is counted cash minus expected cash. Closed records are immutable; post-close corrections are separate Manager-authorized records.

See [Cash Closing Policy](cash-closing-policy.md). Accounting and external settlement mapping still requires finance/accountant validation.

## ADR-023 — Refurbished condition grades and customer complaint case workflow

**Decision:** Use cosmetic condition grades A (Excellent), B (Very Good), C (Good). Functional testing is an independent status; only inspection-passed units can become available for standard refurbished sale. Introduce `CustomerComplaintCase` with evidence, events, inspection and a recorded remedy. The case process must keep the 12-month commercial guarantee separate from Irish statutory consumer rights.

See [Refurbished Condition Grades and Customer Complaint Workflow](refurbished-condition-and-complaints.md). Grade definitions are Company commercial labels, not an official legal standard.

## ADR-024 — Initial image upload limits and secure processing

**Decision:** Initial cap is 10 MiB per image, 10 images and 100 MiB aggregate per target record/line. Accept JPEG/JPG, PNG and WebP; permit HEIC/HEIF only after safe client conversion to JPEG. Validate file content/signature and dimensions, re-encode, remove EXIF/GPS, store privately and audit all mutations.

See [Image Upload Policy](image-upload-policy.md). Limits are configurable and can be tuned from observed usage; OWASP secure-upload guidance is followed.

## ADR-025 — VAT disabled in application until tax setup is reviewed

**Decision:** The initial `CompanyTaxConfiguration` uses `vat_enabled=false`, so the application adds no VAT amount while disabled. `setup_status=NOT_REVIEWED` remains. This is not a legal exemption or determination that the Company has no VAT obligation. Before production tax-document finalization, the Company must document the correct status and either a reviewed disabled basis or a reviewed enabled tax configuration.


## ADR-026 — CASH/CARD-only tender and manual external-terminal capture

**Decision:** v1 supports only `CASH` and `CARD`. Card transactions are processed using the Shop's external physical card terminal. The application records the approved card allocation and an optional terminal reference; no processor/acquirer integration is selected or implemented.

**Terminology:** the payment method is the customer's tender (`CARD`); a payment processor/acquirer is the external provider behind the terminal that authorizes the card payment and settles funds to the merchant.

### Consequences

- Seed only `CASH` and `CARD` as enabled v1 `PaymentMethod` values.
- Mark a CARD allocation settled only after the terminal confirms approval.
- Never store PAN, CVV, PIN or card credentials.
- Processor fees, payout matching and chargeback APIs are deferred.

## ADR-027 — Manager-only partial/total refunds to original tender

**Decision:** All refunds require one fresh `MANAGER` employee password challenge, regardless of amount. A second Manager approver is not required. Partial and full refunds are supported; original tender is the v1 default and alternative tender is disabled.

### Consequences

- Lock the original settled transaction/allocations and enforce cumulative refund caps.
- A split CASH/CARD payment can be refunded using allocations tied to the original tender components, each bounded by that component's refundable remainder.
- Card refund is carried out on the original external terminal/provider and recorded with its confirmation/reference.
- TECHNICIAN cannot execute refunds. Refunds never restock inventory automatically; physical returns use separate StockReturn records.

## ADR-028 — Deposit no-auto-expiry and cancellation controls

**Decision:** Service deposits remain valid while the linked booking/service order is active. No automatic expiry or forfeiture runs. Ninety days without customer response raises `REVIEW_REQUIRED` and a contact task only.

### Consequences

- Snapshot deposit terms and customer's acknowledgement before receipt.
- Return unconsumed balances when the Company cannot provide the agreed service/item or misses the agreed delivery and no new date is agreed, subject to consumer law.
- Customer change-of-mind cancellation follows the written, fair, accepted terms. A special-order non-refundable cost must be disclosed beforehand and evidenced by actual non-recoverable supplier cost.
- Forfeiture is Manager-only, manually authorized, reasoned and evidenced; no blanket automatic forfeiture.
- See CCPC [Deposits guidance](https://www.ccpc.ie/consumer-advice/consumer-rights/buying-goods/deposits/).

## ADR-029 — MySQL-only binary image storage

**Decision:** All service procurement, refurbished-unit and complaint images are stored in MySQL. `stored_image.content` uses `MEDIUMBLOB`; a compact thumbnail uses `BLOB`; owner-specific attachment tables reference the common record. No external object storage or filesystem path is used.

### Consequences

- Full image limit is 10 MiB, fitting within MySQL MEDIUMBLOB capacity (16,777,215 bytes); thumbnail is capped at 60 KiB in BLOB.
- Default EF Core list projections exclude binary columns; authorized endpoints stream thumbnails/full-size content.
- Soft deletion is audited. No automatic physical purge runs in v1; exact GDPR retention schedule remains open.
- MySQL backups/restores include all image data; capacity and restore times must be load-tested.
- See [MySQL Image Storage Architecture](mysql-image-storage.md) and [Image Upload Policy](image-upload-policy.md).

## ADR-030 — Refurbished-unit internal asset code and manufacturer serial

**Decision:** Every physical refurbished unit has a required Company-unique `asset_code`. Manufacturer `serial_number` is captured whenever present; it is optional only where no manufacturer serial exists. Populated normalized serial numbers are unique within the Company.

## ADR-031 — Weekly instalment arrears are status-only in v1

**Decision:** Weekly schedules remain; a missed due date changes the row to `OVERDUE`. Late fees, debt collection, schedule renegotiation and default enforcement are out of v1 scope by owner instruction. LAYAWAY is the enabled default; CREDIT_SALE remains blocked pending Irish regulatory review.


## ADR-032 — OAuth 2.0 / OpenID Connect for the Shop API session

**Decision:** Protect the ASP.NET Core API with OAuth 2.0 access tokens. Use OpenID Connect for Shop sign-in, Authorization Code + PKCE (`S256`) for the React SPA, and JWT Bearer validation for `/api/v1/**`. OpenIddict Server with EF Core persistence is the default self-hosted authorization-server implementation direction.

### Identity boundary

- The token principal is the Shop access credential/Shop context, not an employee. `sub` identifies the stable Shop access principal; the access token carries `company_id`, `shop_id` and `shop_session_id`.
- The access token contains no EmployeeId, employee code, employee name or MANAGER/TECHNICIAN role.
- The API resolves the operational Shop from verified token claims and confirms the referenced ShopSession is active. Client-supplied ShopId is never an authorization source.
- Employee code/password remains a separate, fresh, one-use action authorization for protected business commands; OAuth does not replace actor attribution.

### Required safeguards

- Authorization Code + PKCE S256; exact redirect URI matching, state and nonce.
- Short-lived asymmetric-signed JWS JWT access tokens; API validates signature, issuer, audience, lifetime, `jti`, client and `shop.api` scope. Initial access-token lifetime is 10 minutes and configurable. OpenIddict access-token encryption is disabled for this JWS resource-server profile; authorization codes and refresh tokens remain protected by OpenIddict.
- If refresh tokens are enabled for the public SPA client, rotate on every use, detect reuse, revoke the token family and bind it to the ShopSession.
- Revoke ShopSession and OpenIddict authorization/refresh grants on Shop logout; API requests referencing a revoked session fail even if a JWT has not yet expired.
- React stores tokens in memory only. No localStorage/sessionStorage/IndexedDB, browser logs or analytics. Never send an ID token to the API.
- No Implicit grant, Resource Owner Password Credentials grant, browser client secret or home-grown JWT implementation.
- Shop password is entered at the interactive authorization endpoint, not at the OAuth token endpoint.

### Consequences

- The API can be called only with a valid bearer access token and required scope/policy. OpenAPI documents OAuth2 Authorization Code with PKCE.
- ShopSession remains a domain/operational session independent from access-token lifetime and never captures the last employee who acted.
- OpenIddict authorization/token tables are added through provider-managed EF Core migrations. Shop and Employee password hashes remain logically separate.
- Provider package version, signing-certificate lifecycle, refresh-token enablement and ShopSession idle/absolute timeout values are implementation/deployment tasks.



## ADR-033 — Repository layout for backend and frontend

**Status:** APPROVED — repository structure selected by the project owner; physical project creation is deferred to Phase 2.

**Decision:** Use Option B for the repository layout. Place the .NET solution at the repository root, backend projects under `src/`, the React application under `frontend/pchousestore-web/`, backend and frontend tests under `tests/`, and infrastructure assets under `infrastructure/`.

```text
pchousestore-web/
├── PcHouseStore.sln
├── src/
│   ├── PcHouseStore.Api/
│   ├── PcHouseStore.Application/
│   ├── PcHouseStore.Domain/
│   └── PcHouseStore.Infrastructure/
├── frontend/
│   └── pchousestore-web/
├── tests/
│   ├── PcHouseStore.UnitTests/
│   ├── PcHouseStore.IntegrationTests/
│   └── frontend/
├── infrastructure/
├── specs/
├── .github/
├── AGENTS.md
└── README.md
```

### Consequences

- `PcHouseStore.sln` is the root solution for the backend and backend test projects.
- Backend projects are organized under `src/`; the React + TypeScript + Vite application is under `frontend/pchousestore-web/`.
- Backend unit and integration tests and frontend tests are organized under `tests/`.
- Infrastructure configuration and deployment assets are organized under `infrastructure/`.
- This ADR records repository organization only. The solution and physical application/test projects are created in Phase 2 — Technical Foundation. It does not authorize starting application implementation during Phase 1.
- Existing approved SDD v19 product and security decisions remain unchanged.


## ADR-034 — Simple modular monolith baseline

**Status:** DECIDED at the project owner's request to settle the Phase 1 architecture baseline.

**Decision:** Keep the first release as one modular monolith. The business capability areas listed in the solution architecture are its modules. Each module owns its use cases, domain rules and database tables. A module may expose application operations for other modules to call; other modules must not access its tables directly.

### Layers and dependencies

- **Domain** owns business entities, value objects and invariants and has no dependency on other application projects.
- **Application** owns use cases, validation, authorization and orchestration; it depends on Domain.
- **Infrastructure** owns EF Core persistence and external adapters; it depends on Application and Domain.
- **API** owns HTTP endpoints/contracts, authentication and dependency composition; it depends on Application and Infrastructure and is the composition root.
- The frontend calls the backend through versioned HTTP API contracts and does not reference backend projects.

### Module communication and persistence

- Modules call another module only through its published Application contract, synchronously in-process.
- A use case spanning modules is orchestrated by Application. If it must be atomic, it runs in one database transaction.
- The initial release uses one MySQL database and one EF Core DbContext, with entity configuration grouped by owning module.
- Do not introduce a message broker, distributed transaction coordinator or cross-module event framework in the initial release.

### Minimum development practices

- Keep persistence entities out of API contracts; use explicit DTOs.
- Use asynchronous I/O and propagate CancellationToken. Centralize validation and exception-to-Problem-Details handling.
- Use xUnit for backend tests and Vitest with React Testing Library for frontend tests. Cover domain/application behavior with unit tests and API/persistence behavior with integration tests.
- Run .NET formatting and frontend lint/format checks in CI. Add no architectural tooling without a concrete need.

### Deferred decisions

- Refresh tokens are disabled for the v1 public SPA client. Re-enabling them requires a later ADR with rotation and reuse detection.
- Set and validate ShopSession idle/absolute timeouts and signing-certificate deployment/rotation before production; these do not change the Shop-only token identity boundary.
- Select hosting/deployment technology during deployment planning and the report-generation library when report requirements are specified. Neither blocks the Phase 2 foundation.
- GDPR image retention, VAT/legal configuration, role-matrix refinement and other product decisions remain governed by their existing specifications and ADRs; this ADR does not resolve them.

### Consequences

- Module boundaries are enforced by code ownership and application contracts, with a single database transaction available to straightforward cross-module use cases.
- The architecture remains deployable as one application without introducing distributed systems infrastructure.
- Phase 2 can create the approved solution and project structure using these rules. This ADR does not authorize implementing feature workflows before their approved specifications.
